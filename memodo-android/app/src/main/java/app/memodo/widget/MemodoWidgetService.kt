package app.memodo.widget

import android.content.Context
import android.content.Intent
import android.widget.RemoteViews
import android.widget.RemoteViewsService
import app.memodo.R
import app.memodo.data.Repo
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking

/**
 * 桌面卡片滚动列表数据源：待办（未完成在前/完成隐藏）或备忘（钉板显示优先）。
 * tab 从 WidgetPrefs 动态读取（工厂实例被系统缓存，extras 变化不会重建工厂）。
 * 行点击用 fill-in intent 汇入 ListView 模板广播（MemodoWidgetReceiver.ACTION_ROW_CLICK）。
 */
class MemodoWidgetService : RemoteViewsService() {
    override fun onGetViewFactory(intent: Intent): RemoteViewsFactory =
        MemodoListFactory(this)
}

class MemodoListFactory(private val context: Context) : RemoteViewsService.RemoteViewsFactory {

    private data class Row(
        val id: String,
        val title: String,
        val content: String,
        val completed: Boolean,
        val dueDate: Long?,
    )

    private var rows: List<Row> = emptyList()
    private var isTaskTab = true

    override fun onCreate() {}

    override fun onDataSetChanged() {
        isTaskTab = WidgetPrefs.tab(context) == 0
        rows = runBlocking {
            try {
                if (isTaskTab) {
                    // 只显示未完成：深度使用时已完成条目会大量堆积，卡片保持精简
                    Repo.get(context).observeTasks().first()
                        .filter { !it.completed }
                        .map { Row(it.id, it.title, "", it.completed, it.dueDate) }
                } else {
                    Repo.get(context).memosAll()
                        .filter { it.deletedAt == null && it.archivedAt == null && it.showOnBoard }
                        .sortedByDescending { it.updatedAt }
                        .map { Row(it.id, it.title.ifBlank { context.getString(R.string.untitled) }, it.content, false, null) }
                }
            } catch (_: Exception) { emptyList() }
        }
    }

    override fun onDestroy() { rows = emptyList() }

    override fun getCount(): Int = rows.size

    override fun getViewAt(position: Int): RemoteViews {
        val row = rows.getOrNull(position) ?: return emptyView()
        return if (isTaskTab) taskView(row) else memoView(row)
    }

    private fun fill(kind: String, tab: Int = -1, taskId: String? = null, done: Boolean = false): Intent =
        Intent().apply {
            putExtra(MemodoWidgetReceiver.FILL_KIND, kind)
            if (tab >= 0) putExtra(MemodoWidgetReceiver.EXTRA_TAB, tab)
            if (taskId != null) putExtra(MemodoWidgetReceiver.EXTRA_TASK_ID, taskId)
            putExtra(MemodoWidgetReceiver.EXTRA_DONE, done)
        }

    private fun taskView(row: Row): RemoteViews {
        val rv = RemoteViews(context.packageName, R.layout.widget_task_row)
        rv.setTextViewText(R.id.task_title, row.title)
        rv.setImageViewResource(
            R.id.task_check,
            if (row.completed) R.drawable.ic_widget_check_on else R.drawable.ic_widget_check_off,
        )
        // checkbox：切换完成（fill-in 汇入模板广播）
        rv.setOnClickFillInIntent(
            R.id.task_check,
            fill(MemodoWidgetReceiver.KIND_TOGGLE, taskId = row.id, done = !row.completed),
        )
        // 整行其余区域：唤起 App 待办页
        rv.setOnClickFillInIntent(
            R.id.task_row_root,
            fill(MemodoWidgetReceiver.KIND_OPEN, tab = 0),
        )
        return rv
    }

    private fun memoView(row: Row): RemoteViews {
        val rv = RemoteViews(context.packageName, R.layout.widget_memo_row)
        rv.setTextViewText(R.id.memo_title, row.title)
        rv.setTextViewText(R.id.memo_content, row.content)
        // 整行：唤起 App 备忘页
        rv.setOnClickFillInIntent(
            R.id.memo_row_root,
            fill(MemodoWidgetReceiver.KIND_OPEN, tab = 1),
        )
        return rv
    }

    private fun emptyView(): RemoteViews =
        RemoteViews(context.packageName, R.layout.widget_empty_row)

    override fun getLoadingView(): RemoteViews? = null

    // task_row / memo_row / empty_row（越界兜底）共 3 种布局类型，必须 >= 实际产出
    override fun getViewTypeCount(): Int = 3

    override fun getItemId(position: Int): Long =
        rows.getOrNull(position)?.id?.hashCode()?.toLong() ?: position.toLong()

    override fun hasStableIds(): Boolean = true
}
