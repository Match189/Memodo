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

class MemosWidget : GlanceAppWidget() {
    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val memos = try { Repo.get(context).memosAll().sortedByDescending { it.updatedAt } }
        catch (e: Exception) { emptyList<app.memodo.data.MemoItem>() }
        provideContent {
            GlanceTheme {
                Column(modifier = GlanceModifier.fillMaxWidth().background(GlanceTheme.colors.background).padding(12.dp)) {
                    Text("📌 备忘", style = TextStyle(fontSize = 14.sp, fontWeight = FontWeight.Bold),
                        modifier = GlanceModifier.clickable(actionStartActivity(android.content.Intent(context, MainActivity::class.java))))
                    Spacer(GlanceModifier.height(8.dp))
                    if (memos.isEmpty()) Text("暂无备忘", style = TextStyle(fontSize = 12.sp))
                    else memos.take(8).forEach { m ->
                        Text("• ${m.title.ifBlank { "无标题" }}", style = TextStyle(fontSize = 12.sp, fontWeight = FontWeight.Bold))
                        if (m.content.isNotBlank()) Text(m.content, style = TextStyle(fontSize = 11.sp), maxLines = 2, modifier = GlanceModifier.padding(start = 10.dp))
                        Spacer(GlanceModifier.height(4.dp))
                    }
                }
            }
        }
    }
}
class MemosWidgetReceiver : GlanceAppWidgetReceiver() { override val glanceAppWidget: GlanceAppWidget = MemosWidget() }
