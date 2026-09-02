package app.memodo.data

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.Credentials
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit
import app.memodo.R

/**
 * 自建服务器同步（设计稿 Phase 1 + 蓝图 §45/§47）：
 * JWT 登录 → push 全量变更（LWW 服务端裁决）→ pull cursor 增量。
 * 协议与 memodo-server 一致；webdav 快照的 JSON 构造/解析在此复用。
 */
object ServerSync {
    private const val PREFS = "sync_server"
    private val JSON = "application/json".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .build()

    fun url(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("url", "") ?: ""

    fun user(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("user", "") ?: ""

    // 密码经 SecretStore（AndroidKeyStore AES-GCM）加密落盘；首次读取旧明文自动迁移
    fun pass(ctx: Context) = SecretStore.get(ctx, "server_pass")

    fun cursor(ctx: Context): Long =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("cursor", 0)

    fun save(ctx: Context, url: String, user: String, pass: String) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
            .putString("url", url.trim().trimEnd('/'))
            .putString("user", user.trim())
            .apply()
        SecretStore.put(ctx, "server_pass", pass)
    }

    fun lastSyncAt(ctx: Context): Long =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("lastSyncAt", 0)

    /** 注册新账号（服务端 /auth/register）。返回 null=成功，否则为错误提示。 */
    suspend fun register(context: Context, url: String, email: String, password: String): String? =
        withContext(Dispatchers.IO) {
            try {
                val req = JSONObject().put("email", email).put("password", password)
                val (code, _) = httpJson("$url/auth/register", "POST", req, token = null)
                when {
                    code == 409 -> context.getString(R.string.err_register_conflict)
                    code !in 200..299 -> context.getString(R.string.sync_login_fail, code)
                    else -> null
                }
            } catch (e: Exception) { e.message }
        }



    suspend fun run(context: Context): WebDavSync.Result = withContext(Dispatchers.IO) {
        SyncStatus.markSyncing()
        val url = url(context)
        val user = user(context)
        val pass = pass(context)
        if (url.isEmpty() || user.isEmpty()) {
            SyncStatus.markIdle()
            return@withContext WebDavSync.Result(false, context.getString(R.string.sync_fill_server))
        }

        try {
            val db = AppDatabase.get(context)

            // ---- 登录（JWT）----
            val loginReq = JSONObject().put("email", user).put("password", pass)
            val (loginCode, loginResp) = httpJson("$url/auth/login", "POST", loginReq, token = null)
            if (loginCode !in 200..299) return@withContext WebDavSync.Result(false, context.getString(R.string.sync_login_fail, loginCode))
            val token = JSONObject(loginResp ?: "{}").optString("access_token")
            if (token.isEmpty()) return@withContext WebDavSync.Result(false, context.getString(R.string.sync_login_no_token))

            // ---- pull 游标增量（先拉后推：若先 push，其他设备刚写入的较小 seq 行会被本设备随后推高的游标跳过）----
            val e2eePass = SyncCrypto.passphrase(context)
            val sp = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            // A4 口令指纹：口令变化（含清除）→ 游标归零全量重拉，
            // 否则曾因口令错误跳过的行（游标已越过）永远不会被重新拉到
            val fp = SyncCrypto.fingerprint(e2eePass)
            if (sp.getString("e2ee_pass_fp", null) != fp)
                sp.edit().putString("e2ee_pass_fp", fp).putLong("cursor", 0).apply()
            var pulled = 0
            var undecryptable = 0
            var cursor = cursor(context)
            while (true) {
                val (pullCode, pullBody) = httpJson("$url/sync/pull?cursor=$cursor&limit=500", "GET", null, token)
                if (pullCode !in 200..299) return@withContext WebDavSync.Result(false, context.getString(R.string.sync_pull_fail, pullCode))
                val resp = JSONObject(pullBody ?: "{}")
                val arr = resp.optJSONArray("items") ?: JSONArray()
                for (i in 0 until arr.length()) {
                    val item = arr.getJSONObject(i)
                    // E2EE：data 为密文字符串（口令不对 → 跳过该行）；未启用加密时期的明文行原样
                    val data = SyncCrypto.openRow(item.opt("data"), e2eePass)
                    if (data == null) { undecryptable++; continue }
                    when (item.optString("entity")) {
                        "tasks" -> {
                            val remote = WebDavSync.taskFromJson(data)
                            val local = db.taskDao().getById(remote.id)
                            // LWW 与 Windows 端同构：新者胜，平局按 device_id 字典序决胜（§19）
                            if (local == null || WebDavSync.prefer(remote.updatedAt, local.updatedAt,
                                    item.optString("device_id"), WebDavSync.deviceId(context))) {
                                db.taskDao().upsert(remote)
                                pulled++
                            }
                        }
                        "memos" -> {
                            val remote = WebDavSync.memoFromJson(data)
                            val local = db.memoDao().getById(remote.id)
                            if (local == null || WebDavSync.prefer(remote.updatedAt, local.updatedAt,
                                    item.optString("device_id"), WebDavSync.deviceId(context))) {
                                db.memoDao().upsert(remote)
                                pulled++
                            }
                        }
                    }
                }
                val next = resp.optLong("cursor", cursor)
                sp.edit().putLong("cursor", next).apply()
                if (arr.length() == 0 || next == cursor) break
                cursor = next
            }
            // A3：全部行都解不开（口令不对/本机未设口令）→ 明确报错并中止，不继续 push
            if (undecryptable > 0 && pulled == 0)
                return@withContext WebDavSync.Result(false, context.getString(R.string.sync_e2ee_fail))
                    .also { SyncStatus.markFail(it.message) }

            // ---- push 增量变更（含墓碑）：只推上次同步后有变更的行（首推 lastPushAt=0 自然全量）----
            val now = System.currentTimeMillis()
            val since = sp.getLong("lastPushAt", 0)
            val items = JSONArray()
            db.taskDao().listAll().forEach {
                if (it.updatedAt > since) items.put(JSONObject()
                    .put("entity", "tasks")
                    .put("entity_id", it.id)
                    .put("data", SyncCrypto.sealRow(WebDavSync.taskJson(it), e2eePass))
                    .put("updated_at", it.updatedAt)
                    .put("deleted_at", it.deletedAt ?: JSONObject.NULL)
                    .put("device_id", WebDavSync.deviceId(context)))
            }
            db.memoDao().listAll().forEach {
                if (it.updatedAt > since) items.put(JSONObject()
                    .put("entity", "memos")
                    .put("entity_id", it.id)
                    .put("data", SyncCrypto.sealRow(WebDavSync.memoJson(it), e2eePass))
                    .put("updated_at", it.updatedAt)
                    .put("deleted_at", it.deletedAt ?: JSONObject.NULL)
                    .put("device_id", WebDavSync.deviceId(context)))
            }
            var pushedCount = 0
            if (items.length() > 0) {
                val (pushCode, _) = httpJson("$url/sync/push", "POST",
                    JSONObject().put("items", items), token)
                if (pushCode !in 200..299) return@withContext WebDavSync.Result(false, context.getString(R.string.sync_push_fail, pushCode))
                pushedCount = items.length()
            }

            sp.edit()
                .putLong("lastSyncAt", System.currentTimeMillis())
                .putLong("lastPushAt", now)
                .apply()

            WebDavSync.Result(true, context.getString(R.string.sync_ok_server, pushedCount, pulled))
                .also { SyncStatus.markOk(it.message) }
        } catch (e: Exception) {
            WebDavSync.Result(false, e.message ?: context.getString(R.string.sync_fail))
                .also { SyncStatus.markFail(it.message) }
        }
    }

    private fun httpJson(url: String, method: String, body: JSONObject?, token: String?): Pair<Int, String?> {
        val builder = Request.Builder().url(url)
        if (token != null) builder.header("Authorization", "Bearer $token")
        val req = if (body != null) {
            builder.method("POST", body.toString().toRequestBody(JSON))
        } else {
            builder.method(method, null)
        }
        client.newCall(req.build()).execute().use { resp ->
            val text = try { resp.body?.string() } catch (e: Exception) { null }
            return resp.code to text
        }
    }
}
