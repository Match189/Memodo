package app.memodo.data

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * 定时自动同步：用 AlarmManager（不引 WorkManager 依赖），
 * 应用启动/间隔修改时调用 schedule()，到期触发 SyncReceiver 静默同步。
 */
object SyncScheduler {
    private const val ACTION_SYNC = "app.memodo.action.SYNC"

    fun schedule(context: Context) {
        val prefs = context.getSharedPreferences("sync_settings", Context.MODE_PRIVATE)
        val mode = prefs.getString("mode", "webdav") ?: "webdav"
        if (mode == "local") { cancel(context); return }
        if (mode == "webdav" && (prefs.getString("url", "").isNullOrBlank() || prefs.getString("user", "").isNullOrBlank())) {
            cancel(context); return
        }
        // server 的 url 存在 sync_server（ServerSync.PREFS），不在 sync_settings
        if (mode == "server" && ServerSync.url(context).isNullOrBlank()) { cancel(context); return }

        val minutes = prefs.getInt("interval_minutes", 30).coerceIn(1, 120)
        val intervalMs = maxOf(minutes * 60_000L, 15 * 60_000L)  // setInexactRepeating 下限 ~15min
        val am = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        val pi = pending(context)
        // 从启动时刻起按间隔重复（RTC 唤醒不需要；在途时由 Receiver 去重）
        am.setInexactRepeating(AlarmManager.RTC, System.currentTimeMillis() + intervalMs, intervalMs, pi)
    }

    fun cancel(context: Context) {
        val am = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        am.cancel(pending(context))
    }

    private fun pending(context: Context): PendingIntent {
        val intent = Intent(context, SyncReceiver::class.java).setAction(ACTION_SYNC)
        return PendingIntent.getBroadcast(context, 0, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
    }
}

class SyncReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != "app.memodo.action.SYNC") return
        val result = goAsync()
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        scope.launch {
            try {
                val prefs = context.getSharedPreferences("sync_settings", Context.MODE_PRIVATE)
                val mode = prefs.getString("mode", "webdav") ?: "webdav"
                when (mode) {
                    "webdav" -> if (!prefs.getString("url", "").isNullOrBlank())
                        WebDavSync.run(context)
                    "server" -> ServerSync.run(context) // run() 内部自校验 url/user
                }
            } catch (_: Exception) {
                // 离线静默，下个周期重试
            } finally {
                result.finish()
            }
        }
    }
}

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED) SyncScheduler.schedule(context)
    }
}