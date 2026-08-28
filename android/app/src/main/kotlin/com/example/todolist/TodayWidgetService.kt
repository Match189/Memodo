package com.example.todolist

import android.content.Context
import android.content.Intent
import android.widget.RemoteViews
import android.widget.RemoteViewsService
import es.antonborri.home_widget.HomeWidgetPlugin
import org.json.JSONArray
import kotlin.math.min

/**
 * 小组件列表的数据源：读 home_widget 共享存储里的任务 JSON，逐行渲染。
 */
class TodayWidgetService : RemoteViewsService() {
    override fun onGetViewFactory(intent: Intent): RemoteViewsFactory =
        Factory(applicationContext)

    class Factory(private val context: Context) : RemoteViewsFactory {
        private data class Row(val title: String, val done: Boolean)

        private val rows = mutableListOf<Row>()

        override fun onCreate() {}

        override fun onDataSetChanged() {
            rows.clear()
            val prefs = HomeWidgetPlugin.getData(context)
            val arr = JSONArray(prefs.getString("widget_tasks", "[]") ?: "[]")
            for (i in 0 until min(arr.length(), 12)) {
                val item = arr.getJSONObject(i)
                rows.add(Row(item.optString("t"), item.optBoolean("d")))
            }
        }

        override fun onDestroy() {}

        override fun getCount(): Int = rows.size

        override fun getViewAt(position: Int): RemoteViews {
            val row = rows[position]
            val views = RemoteViews(context.packageName, R.layout.widget_task_item)
            views.setTextViewText(
                R.id.task_title,
                (if (row.done) "☑ " else "○ ") + row.title,
            )
            views.setTextColor(
                R.id.task_title,
                if (row.done) 0xFF8A9494.toInt() else 0xFFE6EAEA.toInt(),
            )
            return views
        }

        override fun getLoadingView(): RemoteViews? = null

        override fun getViewTypeCount(): Int = 1

        override fun getItemId(position: Int): Long = position.toLong()

        override fun hasStableIds(): Boolean = false
    }
}
