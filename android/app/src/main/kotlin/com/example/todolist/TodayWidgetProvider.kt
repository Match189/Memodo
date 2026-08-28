package com.example.todolist

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.content.BroadcastReceiver
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.database.sqlite.SQLiteDatabase
import android.util.Log
import android.view.View
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetPlugin
import es.antonborri.home_widget.HomeWidgetProvider
import org.json.JSONArray
import org.json.JSONObject

/**
 * 今日待办桌面小组件（SPD §12/§14）。
 *
 * 数据不直接依赖 Flutter 引擎：Flutter 侧经 home_widget 推送 JSON 快照 +
 * 本机 SQLite 路径；小组件上的勾选由本 Receiver 原生直写数据库，
 * 下次应用打开/同步时按 LWW 自然传播。
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

    override fun onReceive(context: Context, intent: Intent) {
        super.onReceive(context, intent)
        if (intent.action == ACTION_TOGGLE) {
            val uuid = intent.getStringExtra(EXTRA_UUID) ?: return
            toggleTodo(context, uuid)
            refreshAll(context)
        }
    }

    private fun buildViews(context: Context, prefs: SharedPreferences): RemoteViews {
        val views = RemoteViews(context.packageName, R.layout.widget_today)

        // 点标题区打开应用（OPEN_APP）
        val open = PendingIntent.getActivity(
            context,
            0,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        views.setOnClickPendingIntent(R.id.widget_header, open)

        // 列表项点击 → TOGGLE_TODO 广播（RemoteViews 模板 + 每行 fill-in uuid）
        val toggleTemplate = PendingIntent.getBroadcast(
            context,
            1,
            Intent(context, TodayWidgetProvider::class.java).setAction(ACTION_TOGGLE),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        views.setPendingIntentTemplate(R.id.widget_list, toggleTemplate)

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

    /** 原生直写本地库：切换完成状态（软删除的行不可复活）。 */
    private fun toggleTodo(context: Context, uuid: String) {
        val dbPath = HomeWidgetPlugin.getData(context).getString("db_path", null)
        if (dbPath.isNullOrEmpty()) return
        try {
            val db = SQLiteDatabase.openDatabase(dbPath, null, SQLiteDatabase.OPEN_READWRITE)
            db.use {
                it.execSQL(
                    "UPDATE tasks SET done = 1 - done, updated_at = ? WHERE uuid = ? AND deleted = 0",
                    arrayOf<Any>(System.currentTimeMillis(), uuid),
                )
            }
        } catch (e: Exception) {
            Log.e("TodayWidget", "toggle failed", e)
        }
    }

    companion object {
        const val ACTION_TOGGLE = "com.example.todolist.TOGGLE_TODO"
        const val EXTRA_UUID = "uuid"

        /** 让所有已添加的小组件立刻重绘（Flutter 侧推送数据后调用）。 */
        fun refreshAll(context: Context) {
            val manager = AppWidgetManager.getInstance(context)
            val ids = manager.getAppWidgetIds(
                ComponentName(context, TodayWidgetProvider::class.java),
            )
            val provider = TodayWidgetProvider()
            val prefs = HomeWidgetPlugin.getData(context)
            for (id in ids) {
                manager.updateAppWidget(id, provider.buildViews(context, prefs))
            }
        }
    }
}
