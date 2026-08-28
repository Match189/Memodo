package com.example.todolist

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.view.View
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetPlugin
import es.antonborri.home_widget.HomeWidgetProvider
import org.json.JSONArray
import org.json.JSONObject

/**
 * 今日待办桌面小组件。
 *
 * 数据不直接读 SQLite（小组件运行时 Flutter 引擎可能没起来），而是读
 * Flutter 侧通过 home_widget 推送过来的 JSON 快照。
 */
class TodayWidgetProvider : HomeWidgetProvider() {

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray,
        widgetData: SharedPreferences,
    ) {
        for (id in appWidgetIds) {
            appWidgetManager.updateAppWidget(id, buildViews(context, widgetData))
        }
    }

    private fun buildViews(context: Context, prefs: SharedPreferences): RemoteViews {
        val views = RemoteViews(context.packageName, R.layout.widget_today)

        // 点标题区打开应用
        val open = PendingIntent.getActivity(
            context,
            0,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        views.setOnClickPendingIntent(R.id.widget_header, open)

        val counts = JSONObject(prefs.getString("widget_counts", "{}") ?: "{}")
        val taskCount = counts.optInt("tasks", 0)
        val memoCount = counts.optInt("memos", 0)
        views.setTextViewText(
            R.id.widget_subtitle,
            "$taskCount 条待办 · $memoCount 条备忘",
        )

        val tasks = JSONArray(prefs.getString("widget_tasks", "[]") ?: "[]")
        if (tasks.length() == 0) {
            views.setViewVisibility(R.id.widget_list, View.GONE)
            views.setViewVisibility(R.id.widget_empty, View.VISIBLE)
        } else {
            views.setViewVisibility(R.id.widget_list, View.VISIBLE)
            views.setViewVisibility(R.id.widget_empty, View.GONE)
            views.setRemoteAdapter(
                R.id.widget_list,
                Intent(context, TodayWidgetService::class.java),
            )
        }
        return views
    }

    companion object {
        /** 让所有已添加的小组件立刻重绘（Flutter 侧推送数据后调用）。 */
        fun refreshAll(context: Context) {
            val manager = AppWidgetManager.getInstance(context)
            val ids = manager.getAppWidgetIds(
                ComponentName(context, TodayWidgetProvider::class.java),
            )
            val provider = TodayWidgetProvider()
            for (id in ids) {
                manager.updateAppWidget(
                    id,
                    provider.buildViews(context, HomeWidgetPlugin.getData(context)),
                )
            }
        }
    }
}
