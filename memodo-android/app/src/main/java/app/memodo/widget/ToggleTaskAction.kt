package app.memodo.widget

import android.content.Context
import androidx.glance.GlanceId
import androidx.glance.action.ActionParameters
import androidx.glance.action.actionParametersOf
import androidx.glance.appwidget.action.ActionCallback
import androidx.glance.appwidget.updateAll
import app.memodo.data.Repo

/** Widget 内勾选完成（蓝图 §24 快速完成）：直写 Room → 推进 updated_at → 刷新组件。 */
class ToggleTaskAction : ActionCallback {
    override suspend fun onAction(
        context: Context,
        glanceId: GlanceId,
        parameters: ActionParameters,
    ) {
        val id = parameters[KEY_TASK_ID] ?: return
        val done = parameters[KEY_DONE] ?: true
        try {
            if (Repo.get(context).getTask(id) != null) {
                Repo.get(context).setTaskDone(id, done)
            }
        } catch (e: Exception) {
            // 数据异常不崩溃组件
        }
        MemodoWidget().updateAll(context)
    }

    companion object {
        val KEY_TASK_ID = ActionParameters.Key<String>("taskId")
        val KEY_DONE = ActionParameters.Key<Boolean>("done")
    }
}
