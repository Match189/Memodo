package app.memodo.widget

import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.graphics.Typeface
import android.os.Bundle
import android.text.SpannableString
import android.text.style.StyleSpan
import android.widget.RemoteViews
import app.memodo.MainActivity
import app.memodo.R
import app.memodo.data.Repo
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch

/**
 * 桌面小组件（4×3）：经典 RemoteViews 实现。
 * 内容区为 ListView（RemoteViewsService 驱动，可滚动显示全部条目）；
 * 行点击经模板广播分发：checkbox=切换完成，行区域=唤起 App 对应页。
 * 不走 Glance（其 update 全部经 WorkManager REPLACE 队列，存在秒级延迟与连点互斥）。
 */
class MemodoWidgetReceiver : AppWidgetProvider() {

    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        pushRender(context)
    }

    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            ACTION_TOGGLE_TASK -> {
                val pending = goAsync()
                scope.launch {
                    try {
                        val id = intent.getStringExtra(EXTRA_TASK_ID) ?: return@launch
                        val done = intent.getBooleanExtra(EXTRA_DONE, true)
                        Repo.get(context).setTaskDone(id, done)
                        renderAll(context)
                    } catch (_: Exception) {
                    } finally {
                        pending.finish()
                    }
                }
                return
            }
            ACTION_ROW_CLICK -> {
                when (intent.getStringExtra(FILL_KIND)) {
                    KIND_TOGGLE -> {
                        val pending = goAsync()
                        scope.launch {
                            try {
                                val id = intent.getStringExtra(EXTRA_TASK_ID) ?: return@launch
                                Repo.get(context).setTaskDone(id, intent.getBooleanExtra(EXTRA_DONE, true))
                                renderAll(context)
                            } catch (_: Exception) {
                            } finally {
                                pending.finish()
                            }
                        }
                    }
                    KIND_OPEN -> {
                        // 行区域唤起 App（PendingIntent.send 不受后台启动限制）
                        try {
                            val tab = intent.getIntExtra(EXTRA_TAB, 0)
                            val pi = android.app.PendingIntent.getActivity(
                                context, 3000 + tab,
                                Intent(context, MainActivity::class.java).apply {
                                    putExtra(MainActivity.EXTRA_OPEN_TAB, tab)
                                    flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP
                                },
                                android.app.PendingIntent.FLAG_UPDATE_CURRENT or android.app.PendingIntent.FLAG_IMMUTABLE,
                            )
                            pi.send()
                        } catch (_: Exception) { }
                    }
                }
                return
            }
            ACTION_SWITCH_TAB -> {
                WidgetPrefs.setTab(context, intent.getIntExtra(EXTRA_TAB, 0))
                pushRender(context)
                return
            }
        }
        super.onReceive(context, intent)
    }

    private fun pushRender(context: Context) {
        val pending = goAsync()
        scope.launch {
            try {
                renderAll(context)
            } finally {
                pending.finish()
            }
        }
    }

    companion object {
        private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

        const val ACTION_TOGGLE_TASK = "app.memodo.widget.TOGGLE_TASK"
        const val ACTION_SWITCH_TAB = "app.memodo.widget.SWITCH_TAB"
        const val ACTION_ROW_CLICK = "app.memodo.widget.ROW_CLICK"
        const val EXTRA_TASK_ID = "taskId"
        const val EXTRA_DONE = "done"
        const val EXTRA_TAB = "tab"
        const val FILL_KIND = "fill_kind"
        const val KIND_TOGGLE = "toggle"
        const val KIND_OPEN = "open"

        /** 构建 Tab 行头 + 绑定滚动列表适配器，并通知数据刷新。 */
        suspend fun renderAll(context: Context) {
            val ctx = context.applicationContext
            val mgr = AppWidgetManager.getInstance(ctx) ?: return
            val ids = mgr.getAppWidgetIds(ComponentName(ctx, MemodoWidgetReceiver::class.java))
            if (ids.isEmpty()) return

            // 卡片不经过 Activity，需手动按 App 内语言设置包装，文案才能跟随语言选项
            val lctx = localized(ctx)
            val repo = Repo.get(ctx)
            val allTasks = try { repo.observeTasks().first() } catch (_: Exception) { emptyList<app.memodo.data.TaskItem>() }
            val done = allTasks.count { it.completed }
            val tab = WidgetPrefs.tab(ctx)

            for (id in ids) {
                try {
                    val rv = buildHeader(lctx, tab, done, allTasks.size)
                    // 滚动列表：由 MemodoWidgetService 按当前 tab 提供数据
                    rv.setRemoteAdapter(
                        R.id.widget_list,
                        Intent(ctx, MemodoWidgetService::class.java),
                    )
                    rv.setEmptyView(R.id.widget_list, R.id.widget_empty)
                    rv.setTextViewText(
                        R.id.widget_empty,
                        if (tab == 0) lctx.getString(R.string.widget_todo_empty) else lctx.getString(R.string.widget_memo_empty),
                    )
                    // 行点击模板：ListView 必须用 setPendingIntentTemplate（setOnClickListener 会崩），
                    // 各行 setOnClickFillInIntent 填充 kind/tab/taskId extras。
                    // 模板必须 MUTABLE——IMMUTABLE 会丢弃 fill-in extras（点击无响应的根因）
                    rv.setPendingIntentTemplate(
                        R.id.widget_list,
                        android.app.PendingIntent.getBroadcast(
                            ctx, 4000,
                            Intent(ctx, MemodoWidgetReceiver::class.java).setAction(ACTION_ROW_CLICK),
                            android.app.PendingIntent.FLAG_UPDATE_CURRENT or android.app.PendingIntent.FLAG_MUTABLE,
                        ),
                    )
                    // Tab 点击
                    rv.setOnClickPendingIntent(R.id.tab_tasks, tabPendingIntent(lctx, 0))
                    rv.setOnClickPendingIntent(R.id.tab_memos, tabPendingIntent(lctx, 1))

                    mgr.updateAppWidget(id, rv)
                    mgr.notifyAppWidgetViewDataChanged(id, R.id.widget_list)
                } catch (_: Exception) { }
            }
        }

        private fun buildHeader(context: Context, tab: Int, done: Int, total: Int): RemoteViews {
            val rv = RemoteViews(context.packageName, R.layout.widget_memodo)

            // Tab 选中态
            val (onTab, offTab) = if (tab == 0) R.id.tab_tasks to R.id.tab_memos else R.id.tab_memos to R.id.tab_tasks
            rv.setInt(onTab, "setBackgroundResource", R.drawable.widget_tab_on)
            rv.setTextColor(onTab, 0xB3000000.toInt())
            rv.setTextViewText(onTab, bold(label(context, onTab)))
            rv.setInt(offTab, "setBackgroundResource", android.R.color.transparent)
            rv.setTextColor(offTab, 0x8A000000.toInt())
            rv.setTextViewText(offTab, label(context, offTab))

            // 进度（仅待办 Tab）
            rv.setTextViewText(
                R.id.widget_progress,
                if (tab == 0 && total > 0) context.getString(R.string.widget_progress, done, total) else "",
            )
            return rv
        }

        private fun label(context: Context, viewId: Int): String =
            context.getString(if (viewId == R.id.tab_tasks) R.string.widget_tab_tasks else R.string.widget_tab_memos)

        private fun bold(text: String): CharSequence =
            SpannableString(text).apply { setSpan(StyleSpan(Typeface.BOLD), 0, text.length, 0) }

        private fun tabPendingIntent(context: Context, tab: Int) =
            android.app.PendingIntent.getBroadcast(
                context,
                if (tab == 0) 1 else 2,
                Intent(context, MemodoWidgetReceiver::class.java).apply {
                    action = ACTION_SWITCH_TAB
                    putExtra(EXTRA_TAB, tab)
                },
                android.app.PendingIntent.FLAG_UPDATE_CURRENT or android.app.PendingIntent.FLAG_IMMUTABLE,
            )

        /** 按应用内语言偏好包装 Context（空=跟随系统）。 */
        private fun localized(context: Context): Context {
            val lang = MainActivity.getLanguage(context)
            if (lang.isEmpty()) return context
            return try {
                val config = android.content.res.Configuration(context.resources.configuration)
                config.setLocale(java.util.Locale(lang))
                context.createConfigurationContext(config)
            } catch (_: Exception) { context }
        }
    }
}

