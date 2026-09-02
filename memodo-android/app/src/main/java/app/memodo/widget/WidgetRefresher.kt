package app.memodo.widget

import android.content.Context
import android.util.Log
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch

object WidgetRefresher {
    private const val TAG = "MemodoWidget"
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    @Volatile private var watching = false

    /**
     * 常驻监听 Room 数据流（推送式刷新）：任何来源的写入——app 内操作、卡片勾选、
     * 同步引擎、备份导入——事务提交后 Room invalidation 立即发射，150ms 内重绘卡片。
     * 经典 RemoteViews 渲染是同步 binder 调用，无 WorkManager 队列，即时上屏。
     * 幂等：进程内只启动一次。
     */
    fun startWatching(context: Context) {
        if (watching) return
        synchronized(this) {
            if (watching) return
            watching = true
            val appCtx = context.applicationContext
            scope.launch {
                combine(
                    RepoFlowHolder.repo(appCtx).observeTasks(),
                    RepoFlowHolder.repo(appCtx).observeMemos(),
                ) { _, _ -> }
                    .collectLatest {
                        // collectLatest + delay：密集写入（同步/批量导入）合并为一次重绘
                        delay(150)
                        try {
                            MemodoWidgetReceiver.renderAll(appCtx)
                        } catch (e: Exception) {
                            Log.e(TAG, "watch push failed", e)
                        }
                    }
            }
        }
    }

    /** 立即重绘全部卡片实例（同步 binder 调用，即时生效）。 */
    suspend fun refreshAll(context: Context) {
        try {
            MemodoWidgetReceiver.renderAll(context)
        } catch (e: Exception) {
            Log.e(TAG, "renderAll failed", e)
        }
    }

    /** Repo 单例访问（避免在 object 初始化时持有 context）。 */
    private object RepoFlowHolder {
        fun repo(context: Context) = app.memodo.data.Repo.get(context)
    }
}
