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
import androidx.glance.appwidget.action.actionStartActivity
import androidx.glance.appwidget.provideContent
import androidx.glance.background
import androidx.glance.layout.Column
import androidx.glance.layout.Row
import androidx.glance.layout.Spacer
import androidx.glance.layout.fillMaxWidth
import androidx.glance.layout.height
import androidx.glance.layout.padding
import androidx.glance.text.FontWeight
import androidx.glance.text.Text
import androidx.glance.text.TextStyle
import app.memodo.MainActivity
import app.memodo.data.Repo
import kotlinx.coroutines.flow.first

class BoardWidget : GlanceAppWidget() {
    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val repo = Repo.get(context)
        val allTasks: List<app.memodo.data.TaskItem> = try { repo.observeTasks().first() } catch (e: Exception) { emptyList() }
        val allMemos: List<app.memodo.data.MemoItem> = try { repo.memosAll() } catch (e: Exception) { emptyList() }
        val openTasks = allTasks.filter { !it.completed }.sortedByDescending { it.updatedAt }
        val visMemos = allMemos.filter { it.showOnBoard }.sortedByDescending { it.updatedAt }
        val done = allTasks.count { it.completed }
        provideContent {
            GlanceTheme { BoardContent(context, openTasks, visMemos, done) }
        }
    }

    @Composable
    private fun BoardContent(context: Context, openTasks: List<app.memodo.data.TaskItem>, visMemos: List<app.memodo.data.MemoItem>, done: Int) {
        val size = LocalSize.current
        val maxTodos = if (size.width >= 280.dp && size.height >= 200.dp) 10 else 5
        val maxMemos = if (size.width >= 280.dp && size.height >= 200.dp) 8 else 4
        Column(modifier = GlanceModifier.fillMaxWidth().background(GlanceTheme.colors.background).padding(12.dp)) {
            Row(modifier = GlanceModifier.fillMaxWidth()) {
                Text("📌 钉板", style = TextStyle(fontSize = 14.sp, fontWeight = FontWeight.Bold),
                    modifier = GlanceModifier.clickable(actionStartActivity(android.content.Intent(context, MainActivity::class.java))))
                Spacer(GlanceModifier.defaultWeight())
                Text("待办 ${openTasks.size} · 备忘 ${visMemos.size}${if (done > 0) " · 已完成 $done" else ""}",
                    style = TextStyle(fontSize = 10.sp))
            }
            Spacer(GlanceModifier.height(8.dp))
            if (openTasks.isEmpty() && visMemos.isEmpty()) Text("板面是空的", style = TextStyle(fontSize = 12.sp))
            else {
                openTasks.take(maxTodos).forEach { t ->
                    Row(modifier = GlanceModifier.padding(vertical = 1.dp).fillMaxWidth()) {
                        Text(if (t.completed) "✓ 已完成" else "○", style = TextStyle(fontSize = 14.sp),
                            modifier = GlanceModifier.clickable(WidgetRefresher.toggleTaskAction(t.id, !t.completed)))
                        Text("  ${t.title}", style = TextStyle(fontSize = 12.sp), modifier = GlanceModifier.defaultWeight())
                    }
                }
                Spacer(GlanceModifier.height(4.dp))
                visMemos.take(maxMemos).forEach { m ->
                    Text("• ${m.title.ifBlank { "无标题" }}", style = TextStyle(fontSize = 12.sp, fontWeight = FontWeight.Bold),
                        modifier = GlanceModifier.clickable(actionStartActivity(android.content.Intent(context, MainActivity::class.java))))
                }
                val over = openTasks.size - maxTodos; val overM = visMemos.size - maxMemos
                if (over > 0 || overM > 0) Text("…${over} 条待办 / ${overM} 条备忘", style = TextStyle(fontSize = 10.sp))
            }
        }
    }
}
class BoardWidgetReceiver : GlanceAppWidgetReceiver() { override val glanceAppWidget: GlanceAppWidget = BoardWidget() }
