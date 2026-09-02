package app.memodo.data

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject

/**
 * JSON 备份/恢复（与 Windows 端快照格式一致：format 3 快照）。
 * 导出：tasks+memos 全量（含墓碑）。
 * 导入：LWW 合并（仅当本地无此 uuid 或远端 updated_at 更新时覆盖）。
 */
object BackupService {

    suspend fun exportJson(context: Context): String = withContext(Dispatchers.IO) {
        val db = AppDatabase.get(context)
        val deviceId = WebDavSync.deviceId(context)
        val out = JSONObject()
            .put("format", 3)
            .put("device_id", deviceId)
            .put("exported_at", System.currentTimeMillis())
        out.put("tasks", JSONArray().apply {
            db.taskDao().listAll().forEach { put(WebDavSync.taskJson(it)) }
        })
        out.put("memos", JSONArray().apply {
            db.memoDao().listAll().forEach { put(WebDavSync.memoJson(it)) }
        })
        out.toString(2)
    }

    /** 返回导入条数。 */
    suspend fun importJson(context: Context, json: String): Int = withContext(Dispatchers.IO) {
        val db = AppDatabase.get(context)
        val obj = try { JSONObject(json) } catch (_: Exception) { return@withContext 0 }
        var count = 0

        obj.optJSONArray("tasks")?.let { arr ->
            for (i in 0 until arr.length()) {
                val o = arr.getJSONObject(i)
                val id = o.getString("id")
                val remote = WebDavSync.taskFromJson(o)
                val local = db.taskDao().getById(id)
                // LWW：本地不存在，或远端 updated_at 更新则覆盖
                if (local == null || remote.updatedAt > local.updatedAt) {
                    db.taskDao().upsert(remote)
                    count++
                }
            }
        }
        obj.optJSONArray("memos")?.let { arr ->
            for (i in 0 until arr.length()) {
                val o = arr.getJSONObject(i)
                val id = o.getString("id")
                val remote = WebDavSync.memoFromJson(o)
                val local = db.memoDao().getById(id)
                if (local == null || remote.updatedAt > local.updatedAt) {
                    db.memoDao().upsert(remote)
                    count++
                }
            }
        }
        count
    }
}