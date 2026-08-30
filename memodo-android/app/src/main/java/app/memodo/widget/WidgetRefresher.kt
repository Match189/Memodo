package app.memodo.widget

import android.content.Context
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
