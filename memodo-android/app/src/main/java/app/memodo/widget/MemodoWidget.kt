package app.memodo.widget

import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.GlanceTheme
import androidx.glance.LocalContext
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

class MemodoWidget : GlanceAppWidget() {
    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val showDone = WidgetPrefs.showCompleted(context)
        val prefCap = WidgetPrefs.maxItems(context)
        val tasks = try { Repo.get(context).observeTasks().first().filter { showDone || !it.completed } }
        catch (e: Exception) { emptyList<app.memodo.data.TaskItem>() }
        val done = tasks.count { it.completed }
        val progress = if (tasks.isEmpty()) "" else "${done}/${tasks.size} 完成"
        provideContent {
            GlanceTheme {
                val size = androidx.glance.LocalSize.current
                val sizeCap = when { size.width >= 280.dp && size.height >= 280.dp -> 12; size.width >= 280.dp -> 7; else -> 3 }
                Column(modifier = GlanceModifier.fillMaxWidth().background(GlanceTheme.colors.background).padding(12.dp)) {
                    Row(modifier = GlanceModifier.fillMaxWidth()) {
                        Text("📌 念念 · 待办", style = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.Bold),
                            modifier = GlanceModifier.clickable(actionStartActivity(android.content.Intent(context, MainActivity::class.java))))
                        if (progress.isNotEmpty()) Text(progress, style = TextStyle(fontSize = 11.sp), modifier = GlanceModifier.padding(start = 6.dp))
                    }
                    Spacer(GlanceModifier.height(8.dp))
                    if (tasks.isEmpty()) Text("暂无待办", style = TextStyle(fontSize = 13.sp))
                    else tasks.take(minOf(prefCap, sizeCap)).forEach { t ->
                        Row(modifier = GlanceModifier.padding(vertical = 1.dp).fillMaxWidth()) {
                            Text(if (t.completed) "✓ 已完成" else "○", style = TextStyle(fontSize = 14.sp),
                                modifier = GlanceModifier.clickable(WidgetRefresher.toggleTaskAction(t.id, !t.completed)))
                            Text("  ${t.title}", style = TextStyle(fontSize = 12.sp), modifier = GlanceModifier.defaultWeight())
                        }
                    }
                }
            }
        }
    }
}
class MemodoWidgetReceiver : GlanceAppWidgetReceiver() { override val glanceAppWidget: GlanceAppWidget = MemodoWidget() }
