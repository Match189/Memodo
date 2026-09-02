package app.memodo.data

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.Credentials
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit
import app.memodo.R

/**
 * WebDAV 快照同步（设计稿 Phase 1「手动双向同步」+ 蓝图 §47 LWW+墓碑）。
 * 与 Windows 端共用坚果云上的 memodo/memodo-sync.json：
 * 格式 { format, device_id, exported_at, tasks[], memos[] }，字段 snake_case。
 * 平局（updatedAt 相等）按 device_id 字典序决胜。
 *
 * HTTP 用 OkHttp：MKCOL 不在 HttpURLConnection/内置栈的方法白名单内，
 * 会抛 "Expected one of [...] but was MKCOL"（用户实测），OkHttp 允许自定义方法。
 */
object WebDavSync {
    private const val PREFS = "sync_settings"
    private const val DIR = "memodo"
    private const val FILE = "memodo/memodo-sync.json"
    private val JSON = "application/json".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .build()

    fun url(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString("url", "https://dav.jianguoyun.com/dav/") ?: ""

    fun user(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("user", "") ?: ""

    // 密码经 SecretStore（AndroidKeyStore AES-GCM）加密落盘；首次读取旧明文自动迁移
    fun pass(ctx: Context) = SecretStore.get(ctx, "webdav_pass")

    fun lastSyncAt(ctx: Context): Long =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("lastSyncAt", 0)

    fun save(ctx: Context, url: String, user: String, pass: String) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
            .putString("url", url.trim())
            .putString("user", user.trim())
            .apply()
        SecretStore.put(ctx, "webdav_pass", pass)
    }

    /** 同步方式（用户裁定补全）：local 仅本地 / webdav / server 自建服务器。 */
    fun mode(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("mode", "webdav") ?: "webdav"

    fun setMode(ctx: Context, mode: String) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString("mode", mode).apply()
    }

    fun deviceId(ctx: Context): String {
        val sp = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        var id = sp.getString("device_id", "") ?: ""
        if (id.isEmpty()) {
            id = "and-" + java.util.UUID.randomUUID().toString().replace("-", "").take(8)
            sp.edit().putString("device_id", id).apply()
        }
        return id
    }

    data class Result(val ok: Boolean, val message: String)

    suspend fun run(context: Context): Result = withContext(Dispatchers.IO) {
        SyncStatus.markSyncing()
        val url = url(context).trim()
        val user = user(context).trim()
        val pass = pass(context)
        if (url.isEmpty() || user.isEmpty()) {
            SyncStatus.markIdle()
            return@withContext Result(false, context.getString(R.string.sync_fill_webdav))
        }
        val base = url.trimEnd('/') + "/"

        try {
            // MKCOL 建目录（已存在 405 也算成功）；OkHttp 支持自定义方法
            http(base + DIR, user, pass, "MKCOL")

            // 拉远端快照（404 = 首次同步）
            val (getCode, getBody) = http(base + FILE, user, pass, "GET")
            if (getCode == 404) {
                // 首次同步
            } else if (getCode !in 200..299) {
                return@withContext Result(false, context.getString(R.string.sync_download_fail, getCode))
            }
            // E2EE：云端是密文（或旧明文），先解密再解析；口令不一致 → 终止保护本地
            val e2eePass = SyncCrypto.passphrase(context)
            // A1 混合口令护栏：云端是本格式密文而本机未设口令 → 中止（绝不以明文覆盖云端密文）
            if (getBody != null && SyncCrypto.isEncrypted(String(getBody)) && e2eePass.isEmpty()) {
                return@withContext Result(false, context.getString(R.string.sync_e2ee_no_pass))
                    .also { SyncStatus.markFail(it.message) }
            }
            val remote = getBody?.let {
                val plain = SyncCrypto.tryDecrypt(String(it), e2eePass)
                    ?: return@withContext Result(false, context.getString(R.string.sync_e2ee_fail))
                        .also { SyncStatus.markFail(it.message) }
                try { JSONObject(plain) } catch (e: Exception) { JSONObject() }
            } ?: JSONObject()
            val remoteDevice = remote.optString("device_id", "")
            val myDevice = deviceId(context)

            val db = AppDatabase.get(context)
            val localTasks = db.taskDao().listAll()
            val localMemos = db.memoDao().listAll()

            // ---- LWW 合并 ----
            val remoteTasks = mutableMapOf<String, JSONObject>()
            remote.optJSONArray("tasks")?.let { arr ->
                for (i in 0 until arr.length()) {
                    val o = arr.getJSONObject(i)
                    remoteTasks[o.getString("id")] = o
                }
            }
            val mergedTasks = LinkedHashMap<String, TaskItem>()
            localTasks.forEach { mergedTasks[it.id] = it }
            remoteTasks.forEach { (id, o) ->
                val rt = taskFromJson(o)
                val lt = mergedTasks[id]
                if (lt == null || prefer(rt.updatedAt, lt.updatedAt, remoteDevice, myDevice)) {
                    mergedTasks[id] = rt
                }
            }

            val remoteMemos = mutableMapOf<String, JSONObject>()
            remote.optJSONArray("memos")?.let { arr ->
                for (i in 0 until arr.length()) {
                    val o = arr.getJSONObject(i)
                    remoteMemos[o.getString("id")] = o
                }
            }
            val mergedMemos = LinkedHashMap<String, MemoItem>()
            localMemos.forEach { mergedMemos[it.id] = it }
            remoteMemos.forEach { (id, o) ->
                val rm = memoFromJson(o)
                val lm = mergedMemos[id]
                if (lm == null || prefer(rm.updatedAt, lm.updatedAt, remoteDevice, myDevice)) {
                    mergedMemos[id] = rm
                }
            }

            // ---- 应用到本地（原样落库，保 LWW 时间戳）----
            mergedTasks.values.forEach { db.taskDao().upsert(it) }
            mergedMemos.values.forEach { db.memoDao().upsert(it) }

            // ---- 跨端设置合并（自动同步间隔 LWW）----
            val settingsPrefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            val localSettingsUpdatedAt = settingsPrefs.getLong("interval_updated_at", 0)
            remote.optJSONObject("settings")?.let { rs ->
                val remoteUpdatedAt = rs.optLong("updated_at", 0)
                val remoteMinutes = rs.optInt("auto_sync_minutes", 3)
                if (remoteUpdatedAt > localSettingsUpdatedAt && remoteMinutes in 1..120) {
                    settingsPrefs.edit()
                        .putInt("interval_minutes", remoteMinutes)
                        .putLong("interval_updated_at", remoteUpdatedAt)
                        .apply()
                    SyncScheduler.schedule(context)
                }
            }

            // ---- 上传前冲突重检：重新拉取远端，若已变更则重新合并 ----
            val (_, reGetBody) = http(base + FILE, user, pass, "GET")
            // A1 护栏同样适用于重检响应（期间云端被别的设备换成密文）
            if (reGetBody != null && SyncCrypto.isEncrypted(String(reGetBody)) && e2eePass.isEmpty()) {
                return@withContext Result(false, context.getString(R.string.sync_e2ee_no_pass))
                    .also { SyncStatus.markFail(it.message) }
            }
            val reRemote = reGetBody?.let {
                val plain = SyncCrypto.tryDecrypt(String(it), e2eePass)
                    ?: return@withContext Result(false, context.getString(R.string.sync_e2ee_fail))
                        .also { SyncStatus.markFail(it.message) }
                try { JSONObject(plain) } catch (_: Exception) { null }
            }
            if (reRemote != null && reRemote.optString("device_id", "") != myDevice) {
                // 远端已被其他设备更新：重新拉取本地最新，重新合并
                val freshLocal = db.taskDao().listAll()
                val freshLocalMemos = db.memoDao().listAll()
                val freshMap = LinkedHashMap<String, TaskItem>().apply { freshLocal.forEach { put(it.id, it) } }
                val freshMemoMap = LinkedHashMap<String, MemoItem>().apply { freshLocalMemos.forEach { put(it.id, it) } }
                reRemote.optJSONArray("tasks")?.let { arr ->
                    for (i in 0 until arr.length()) {
                        val o = arr.getJSONObject(i); val id = o.getString("id")
                        val rt = taskFromJson(o)
                        if (freshMap[id] == null || prefer(rt.updatedAt, (freshMap[id]?.updatedAt ?: 0), reRemote.optString("device_id"), myDevice))
                            freshMap[id] = rt
                    }
                }
                reRemote.optJSONArray("memos")?.let { arr ->
                    for (i in 0 until arr.length()) {
                        val o = arr.getJSONObject(i); val id = o.getString("id")
                        val rm = memoFromJson(o)
                        if (freshMemoMap[id] == null || prefer(rm.updatedAt, (freshMemoMap[id]?.updatedAt ?: 0), reRemote.optString("device_id"), myDevice))
                            freshMemoMap[id] = rm
                    }
                }
                freshMap.values.forEach { db.taskDao().upsert(it) }
                freshMemoMap.values.forEach { db.memoDao().upsert(it) }
                // 使用重新合并后的结果上传
                mergedTasks.clear(); mergedTasks.putAll(freshMap)
                mergedMemos.clear(); mergedMemos.putAll(freshMemoMap)
            }

            // ---- 上传合并结果（含墓碑 + 跨端设置）----
            val out = JSONObject()
                .put("format", 3)
                .put("device_id", myDevice)
                .put("exported_at", System.currentTimeMillis())
            out.put("tasks", JSONArray().apply { mergedTasks.values.forEach { put(taskJson(it)) } })
            out.put("memos", JSONArray().apply { mergedMemos.values.forEach { put(memoJson(it)) } })
            val finalMinutes = settingsPrefs.getInt("interval_minutes", 3).coerceIn(1, 120)
            out.put("settings", JSONObject()
                .put("auto_sync_minutes", finalMinutes)
                .put("updated_at", settingsPrefs.getLong("interval_updated_at", 0)))

            // E2EE：启用口令时整包 AES-256-GCM 加密上传，云端只存密文
            val (putCode, _) = http(base + FILE, user, pass, "PUT",
                SyncCrypto.encrypt(out.toString(), e2eePass).toByteArray())
            if (putCode !in 200..299) return@withContext Result(false, context.getString(R.string.sync_upload_fail, putCode))

            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
                .putLong("lastSyncAt", System.currentTimeMillis()).apply()

            Result(true, context.getString(R.string.sync_ok_webdav, mergedTasks.size, mergedMemos.size))
                .also { SyncStatus.markOk(it.message) }
        } catch (e: Exception) {
            Result(false, e.message ?: context.getString(R.string.sync_fail))
                .also { SyncStatus.markFail(it.message) }
        }
    }

    /** 远端时间戳更新，或平局且远端设备号字典序更大（§19）。 */
    fun prefer(remoteTs: Long, localTs: Long, remoteDev: String, localDev: String) =
        remoteTs > localTs || (remoteTs == localTs && remoteDev > localDev)

    // ---------- JSON ----------
    fun taskJson(t: TaskItem) = JSONObject()
        .put("id", t.id).put("title", t.title).put("description", t.description)
        .put("completed", t.completed).put("priority", t.priority)
        .put("due_date", t.dueDate ?: JSONObject.NULL)
        .put("archived_at", t.archivedAt ?: JSONObject.NULL)
        .put("created_at", t.createdAt).put("updated_at", t.updatedAt)
        .put("deleted_at", t.deletedAt ?: JSONObject.NULL)

    fun memoJson(m: MemoItem) = JSONObject()
        .put("id", m.id).put("title", m.title).put("content", m.content)
        .put("completed", m.completed)
        .put("show_on_board", m.showOnBoard)
        .put("archived_at", m.archivedAt ?: JSONObject.NULL)
        .put("created_at", m.createdAt).put("updated_at", m.updatedAt)
        .put("deleted_at", m.deletedAt ?: JSONObject.NULL)

    fun taskFromJson(o: JSONObject) = TaskItem(
        id = o.getString("id"),
        title = o.optString("title", ""),
        description = o.optString("description", ""),
        completed = o.optBoolean("completed", false),
        priority = o.optInt("priority", 0),
        dueDate = if (o.isNull("due_date")) null else o.optLong("due_date"),
        archivedAt = if (o.isNull("archived_at")) null else o.optLong("archived_at"),
        createdAt = o.optLong("created_at"),
        updatedAt = o.optLong("updated_at"),
        deletedAt = if (o.isNull("deleted_at")) null else o.optLong("deleted_at"),
    )

    fun memoFromJson(o: JSONObject) = MemoItem(
        id = o.getString("id"),
        title = o.optString("title", ""),
        content = o.optString("content", ""),
        completed = o.optBoolean("completed", false),
        showOnBoard = o.optBoolean("show_on_board", true),
        archivedAt = if (o.isNull("archived_at")) null else o.optLong("archived_at"),
        createdAt = o.optLong("created_at"),
        updatedAt = o.optLong("updated_at"),
        deletedAt = if (o.isNull("deleted_at")) null else o.optLong("deleted_at"),
    )

    // ---------- HTTP)（OkHttp；支持 MKCOL 等自定义方法） ----------
    private fun http(url: String, user: String, pass: String, method: String, body: ByteArray? = null): Pair<Int, ByteArray?> {
        val builder = Request.Builder().url(url)
            .header("Authorization", Credentials.basic(user, pass))
        val req = if (body != null) {
            builder.method(method, body.toRequestBody(JSON))
        } else {
            builder.method(method, null)
        }
        client.newCall(req.build()).execute().use { resp ->
            val bytes = try { resp.body?.bytes() } catch (e: Exception) { null }
            return resp.code to bytes
        }
    }
}
