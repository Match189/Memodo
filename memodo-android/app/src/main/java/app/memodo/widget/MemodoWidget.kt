package app.memodo.widget

import android.content.Context
import android.content.Intent
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
import androidx.glance.layout.fillMaxSize
import androidx.glance.layout.height
import androidx.glance.layout.padding
import androidx.glance.text.FontWeight
import androidx.glance.text.Text
import androidx.glance.text.TextDecoration
import androidx.glance.text.TextStyle
import app.memodo.MainActivity
import app.memodo.data.Repo
import app.memodo.data.TaskItem
import kotlinx.coroutines.flow.first

class MemodoWidget : GlanceAppWidget() {

    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val tasks = try {
            Repo.get(context).observeTasks().first()
        } catch (e: Exception) {
            emptyList()
        }
        provideContent {
            GlanceTheme {
                WidgetContent(tasks)
            }
        }
    }

    @Composable
    private fun WidgetContent(tasks: List<TaskItem>) {
        val context = LocalContext.current
        Column(
            modifier = GlanceModifier
                .fillMaxSize()
                .background(GlanceTheme.colors.background)
                .clickable(actionStartActivity(Intent(context, MainActivity::class.java)))
                .padding(12.dp)
        ) {
            Text(
                "念念 · 待办",
                style = TextStyle(fontSize = 16.sp, fontWeight = FontWeight.Bold)
            )
            Spacer(GlanceModifier.height(8.dp))
            if (tasks.isEmpty()) {
                Text("暂无待办", style = TextStyle(fontSize = 13.sp))
            } else {
                tasks.take(8).forEach { t ->
                    Row(modifier = GlanceModifier.padding(vertical = 2.dp)) {
                        Text(
                            (if (t.completed) "✓ " else "○ ") + t.title,
                            style = TextStyle(
                                fontSize = 13.sp,
                                textDecoration = if (t.completed) TextDecoration.LineThrough else null
                            )
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
