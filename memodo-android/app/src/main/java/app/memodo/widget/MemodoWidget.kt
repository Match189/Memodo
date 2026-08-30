package app.memodo.widget

import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.GlanceTheme
import androidx.glance.LocalContext
import androidx.glance.LocalSize
import androidx.glance.action.clickable
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver
import androidx.glance.appwidget.action.actionRunCallback
import androidx.glance.appwidget.action.actionStartActivity
import androidx.glance.appwidget.provideContent
import androidx.glance.background
import androidx.glance.layout.Column
import androidx.glance.layout.Row
import androidx.glance.layout.Spacer
import androidx.glance.layout.fillMaxSize
import androidx.glance.layout.fillMaxWidth
import androidx.glance.layout.height
import androidx.glance.layout.padding
import androidx.glance.layout.width
import androidx.glance.text.FontWeight
import androidx.glance.text.Text
import androidx.glance.text.TextDecoration
import androidx.glance.text.TextStyle
import androidx.glance.action.actionParametersOf
import app.memodo.MainActivity
import app.memodo.data.Repo
import app.memodo.data.TaskItem
import kotlinx.coroutines.flow.first

/**
 * Home Widget（蓝图 §24）：快速查看 + 快速完成。
 * 尺寸自适应：LocalSize 决定显示条数；同一 GlanceAppWidget 供 2×2 / 4×2 / 4×4 三个 provider 复用。
 */
class MemodoWidget : GlanceAppWidget() {

    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val showDone = WidgetPrefs.showCompleted(context)
        val prefCap = WidgetPrefs.maxItems(context)
        val tasks = try {
            Repo.get(context).observeTasks().first()
                .filter { showDone || !it.completed }
        } catch (e: Exception) {
            emptyList()
        }
        // 设计文档：头部进度「3/5 完成」
        val done = tasks.count { it.completed }
        val progress = if (tasks.isEmpty()) "" else "${done}/${tasks.size} 完成"
        provideContent {
            GlanceTheme {
                WidgetContent(tasks, prefCap, progress)
            }
        }
    }

    @Composable
    private fun WidgetContent(tasksAll: List<TaskItem>, prefCap: Int, progress: String) {
        val context = LocalContext.current
        val size = LocalSize.current
        val sizeCap = when {
            size.width >= 280.dp && size.height >= 280.dp -> 12   // 4×4
            size.width >= 280.dp -> 7                             // 4×2
            else -> 3                                             // 2×2
        }
        val tasks = tasksAll.take(minOf(prefCap, sizeCap))

        Column(
            modifier = GlanceModifier
                .fillMaxSize()
                .background(GlanceTheme.colors.background)
                .padding(12.dp)
        ) {
            Row(modifier = GlanceModifier.fillMaxWidth()) {
                Text(
                    "📌 念念 · 待办",
                    style = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.Bold),
                    modifier = GlanceModifier.defaultWeight()
                        .clickable(actionStartActivity(android.content.Intent(context, MainActivity::class.java))),
                )
                if (progress.isNotEmpty()) {
                    Text(
                        progress,
                        style = TextStyle(fontSize = 11.sp),
                        modifier = GlanceModifier.padding(start = 6.dp),
                    )
                }
            }
            Spacer(GlanceModifier.height(8.dp))
            if (tasks.isEmpty()) {
                Text("暂无待办", style = TextStyle(fontSize = 13.sp))
            } else {
                tasks.forEach { t ->
                    Row(modifier = GlanceModifier.padding(vertical = 2.dp)) {
                        // 自绘圆勾（规避 Glance CheckBox 首帧只渲染一条的缺陷）：
                        // 点击文本触发 ToggleTaskAction，语义与 CheckBox 等价
                        Text(
                            text = if (t.completed) "✓" else "○",
                            style = TextStyle(
                                fontSize = 15.sp,
                                color = if (t.completed)
                                    GlanceTheme.colors.primary
                                else GlanceTheme.colors.onSurface,
                            ),
                            modifier = GlanceModifier.clickable(
                                actionRunCallback<ToggleTaskAction>(
                                    actionParametersOf(
                                        ToggleTaskAction.KEY_TASK_ID to t.id,
                                        ToggleTaskAction.KEY_DONE to !t.completed,
                                    )
                                )
                            ),
                        )
                        Spacer(GlanceModifier.width(6.dp))
                        Text(
                            t.title,
                            style = TextStyle(
                                fontSize = 13.sp,
                                textDecoration = if (t.completed) TextDecoration.LineThrough else null,
                            ),
                            modifier = GlanceModifier.defaultWeight()
                                .clickable(actionStartActivity(android.content.Intent(context, MainActivity::class.java))),
                        )
                    }
                }
            }
        }
    }
}

class MemodoWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = MemodoWidget()
}

class MemodoWidgetLargeReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = MemodoWidget()
}
