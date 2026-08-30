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

    fun pass(ctx: Context) =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("pass", "") ?: ""

    fun cursor(ctx: Context): Long =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("cursor", 0)

    fun save(ctx: Context, url: String, user: String, pass: String) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
            .putString("url", url.trim().trimEnd('/'))
            .putString("user", user.trim())
            .putString("pass", pass)
            .apply()
    }

    fun lastSyncAt(ctx: Context): Long =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("lastSyncAt", 0)



    suspend fun run(context: Context): WebDavSync.Result = withContext(Dispatchers.IO) {
        val url = url(context)
        val user = user(context)
        val pass = pass(context)
        if (url.isEmpty() || user.isEmpty()) return@withContext WebDavSync.Result(false, "请先填写服务器地址与账号")

        try {
            val db = AppDatabase.get(context)

            // ---- 登录（JWT）----
            val loginReq = JSONObject().put("email", user).put("password", pass)
            val (loginCode, loginResp) = httpJson("$url/auth/login", "POST", loginReq, token = null)
            if (loginCode !in 200..299) return@withContext WebDavSync.Result(false, "登录失败 HTTP $loginCode")
            val token = JSONObject(loginResp ?: "{}").optString("access_token")
            if (token.isEmpty()) return@withContext WebDavSync.Result(false, "登录响应缺少 token")

            // ---- push 全量变更（含墓碑）----
            val now = System.currentTimeMillis()
            val items = JSONArray()
            db.taskDao().listAll().forEach {
                items.put(JSONObject()
                    .put("entity", "tasks")
                    .put("entity_id", it.id)
                    .put("data", WebDavSync.taskJson(it))
                    .put("updated_at", it.updatedAt)
                    .put("deleted_at", it.deletedAt ?: JSONObject.NULL)
                    .put("device_id", WebDavSync.deviceId(context)))
            }
            db.memoDao().listAll().forEach {
                items.put(JSONObject()
                    .put("entity", "memos")
                    .put("entity_id", it.id)
                    .put("data", WebDavSync.memoJson(it))
                    .put("updated_at", it.updatedAt)
                    .put("deleted_at", it.deletedAt ?: JSONObject.NULL)
                    .put("device_id", WebDavSync.deviceId(context)))
            }
            val (pushCode, _) = httpJson("$url/sync/push", "POST",
                JSONObject().put("items", items), token)
            if (pushCode !in 200..299) return@withContext WebDavSync.Result(false, "push 失败 HTTP $pushCode")

            // ---- pull 游标增量 ----
            var cursor = cursor(context)
            var pulled = 0
            while (true) {
                val (pullCode, pullBody) = httpJson("$url/sync/pull?cursor=$cursor&limit=500", "GET", null, token)
                if (pullCode !in 200..299) return@withContext WebDavSync.Result(false, "pull 失败 HTTP $pullCode")
                val resp = JSONObject(pullBody ?: "{}")
                val arr = resp.optJSONArray("items") ?: JSONArray()
                for (i in 0 until arr.length()) {
                    val item = arr.getJSONObject(i)
                    val data = item.optJSONObject("data") ?: continue
                    when (item.optString("entity")) {
                        "tasks" -> db.taskDao().upsert(WebDavSync.taskFromJson(data))
                        "memos" -> db.memoDao().upsert(WebDavSync.memoFromJson(data))
                    }
                    pulled++
                }
                val next = resp.optLong("cursor", cursor)
                context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
                    .putLong("cursor", next).apply()
                if (arr.length() == 0 || next == cursor) break
                cursor = next
            }

            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
                .putLong("lastSyncAt", System.currentTimeMillis()).apply()

            WebDavSync.Result(true, "同步完成：推送 ${items.length()} 条，拉取 $pulled 条")
        } catch (e: Exception) {
            WebDavSync.Result(false, e.message ?: "同步失败")
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
