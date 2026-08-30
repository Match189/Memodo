package app.memodo.data

import android.content.Context
import android.util.Base64
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

/**
 * WebDAV 快照同步（设计稿 Phase 1「手动双向同步」+ 蓝图 §47 LWW+墓碑）。
 * 与 Windows 端共用坚果云上的 memodo/memodo-sync.json：
 * 格式 { format, device_id, exported_at, tasks[], memos[] }，字段 snake_case。
 * 平局（updatedAt 相等）按 device_id 字典序决胜。
 */
object WebDavSync {
    private const val PREFS = "sync_settings"
    private const val DIR = "memodo"
    private const val FILE = "memodo/memodo-sync.json"

    fun url(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString("url", "https://dav.jianguoyun.com/dav/") ?: ""

    fun user(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("user", "") ?: ""

    fun pass(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("pass", "") ?: ""

    fun lastSyncAt(ctx: Context): Long =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("lastSyncAt", 0)

    fun save(ctx: Context, url: String, user: String, pass: String) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
            .putString("url", url.trim())
            .putString("user", user.trim())
            .putString("pass", pass)
            .apply()
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
        val url = url(context).trim()
        val user = user(context).trim()
        val pass = pass(context)
        if (url.isEmpty() || user.isEmpty()) return@withContext Result(false, "请先填写 WebDAV 地址与账号")
        val base = url.trimEnd('/') + "/"

        try {
            // MKCOL 建目录（已存在 405 也算成功）
            http(base + DIR, user, pass, "MKCOL")

            // 拉远端快照（404 = 首次同步）
            val (getCode, getBody) = http(base + FILE, user, pass, "GET")
            if (getCode != 404 && getCode !in 200..299) return@withContext Result(false, "下载快照失败 HTTP $getCode")
            val remote = getBody?.let {
                try { JSONObject(String(it)) } catch (e: Exception) { JSONObject() }
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

            // ---- 上传合并结果（含墓碑）----
            val out = JSONObject()
                .put("format", 3)
                .put("device_id", myDevice)
                .put("exported_at", System.currentTimeMillis())
            out.put("tasks", JSONArray().apply { mergedTasks.values.forEach { put(taskJson(it)) } })
            out.put("memos", JSONArray().apply { mergedMemos.values.forEach { put(memoJson(it)) } })

            val (putCode, _) = http(base + FILE, user, pass, "PUT", out.toString().toByteArray())
            if (putCode !in 200..299) return@withContext Result(false, "上传快照失败 HTTP $putCode")

            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
                .putLong("lastSyncAt", System.currentTimeMillis()).apply()

            Result(true, "同步完成：待办 ${mergedTasks.size} 条 / 备忘 ${mergedMemos.size} 条")
        } catch (e: Exception) {
            Result(false, e.message ?: "同步失败")
        }
    }

    /** 远端时间戳更新，或平局且远端设备号字典序更大（§19）。 */
    private fun prefer(remoteTs: Long, localTs: Long, remoteDev: String, localDev: String) =
        remoteTs > localTs || (remoteTs == localTs && remoteDev > localDev)

    // ---------- JSON ----------
    private fun taskJson(t: TaskItem) = JSONObject()
        .put("id", t.id).put("title", t.title).put("description", t.description)
        .put("completed", t.completed).put("priority", t.priority)
        .put("due_date", t.dueDate ?: JSONObject.NULL)
        .put("created_at", t.createdAt).put("updated_at", t.updatedAt)
        .put("deleted_at", t.deletedAt ?: JSONObject.NULL)

    private fun memoJson(m: MemoItem) = JSONObject()
        .put("id", m.id).put("title", m.title).put("content", m.content)
        .put("created_at", m.createdAt).put("updated_at", m.updatedAt)
        .put("deleted_at", m.deletedAt ?: JSONObject.NULL)

    private fun taskFromJson(o: JSONObject) = TaskItem(
        id = o.getString("id"),
        title = o.optString("title", ""),
        description = o.optString("description", ""),
        completed = o.optBoolean("completed", false),
        priority = o.optInt("priority", 0),
        dueDate = if (o.isNull("due_date")) null else o.optLong("due_date"),
        createdAt = o.optLong("created_at"),
        updatedAt = o.optLong("updated_at"),
        deletedAt = if (o.isNull("deleted_at")) null else o.optLong("deleted_at"),
    )

    private fun memoFromJson(o: JSONObject) = MemoItem(
        id = o.getString("id"),
        title = o.optString("title", ""),
        content = o.optString("content", ""),
        createdAt = o.optLong("created_at"),
        updatedAt = o.optLong("updated_at"),
        deletedAt = if (o.isNull("deleted_at")) null else o.optLong("deleted_at"),
    )

    // ---------- HTTP ----------
    private fun http(url: String, user: String, pass: String, method: String, body: ByteArray? = null): Pair<Int, ByteArray?> {
        val conn = URL(url).openConnection() as HttpURLConnection
        conn.requestMethod = method
        conn.connectTimeout = 15000
        conn.readTimeout = 15000
        val auth = Base64.encodeToString("$user:$pass".toByteArray(), Base64.NO_WRAP)
        conn.setRequestProperty("Authorization", "Basic $auth")
        if (body != null) {
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json")
            conn.setFixedLengthStreamingMode(body.size)
            conn.outputStream.use { it.write(body) }
        }
        val code = conn.responseCode
        val stream = if (code in 200..299) conn.inputStream else conn.errorStream
        val bytes = try { stream?.readBytes() } catch (e: Exception) { null }
        conn.disconnect()
        return code to bytes
    }
}
