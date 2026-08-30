package app.memodo.widget

import android.content.Context
import android.widget.Toast
import androidx.glance.appwidget.updateAll

/**
 * 统一刷新路由（解决用户反馈：勾选状态不同步、两卡数据不同步）。
 * 所有数据变更后调用 refreshAll，确保三套卡片 + App 侧即时一致。
 */
object WidgetRefresher {
    suspend fun refreshAll(context: Context) {
        MemodoWidget().updateAll(context)
        BoardWidget().updateAll(context)
        MemosWidget().updateAll(context)
        // 桌面组件经 RemoteViews IPC 更新需约300ms（Glance 架构限制），
        // Toast 即时反馈让用户确认同步指令已发出。
        android.os.Handler(android.os.Looper.getMainLooper()).post {
            Toast.makeText(context, "卡片已同步", Toast.LENGTH_SHORT).show()
        }
    }

    /** ToggleTaskAction 复用的构造器 */
    fun toggleTaskAction(taskId: String, done: Boolean) =
        androidx.glance.appwidget.action.actionRunCallback<ToggleTaskAction>(
            androidx.glance.action.actionParametersOf(
                ToggleTaskAction.KEY_TASK_ID to taskId,
                ToggleTaskAction.KEY_DONE to done,
            )
        )
}
