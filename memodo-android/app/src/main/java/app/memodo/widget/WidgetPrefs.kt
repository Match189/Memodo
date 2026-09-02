package app.memodo.widget

import android.content.Context

/**
 * 桌面小组件本机偏好：最大条数 / 显示已完成 / 当前 Tab。
 * 存 SharedPreferences，本机态不进同步协议。
 */
object WidgetPrefs {
    private const val FILE = "widget_settings"
    private const val KEY_MAX = "maxItems"
    private const val KEY_SHOW_DONE = "showCompleted"
    private const val KEY_TAB = "current_tab"

    fun maxItems(ctx: Context): Int =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getInt(KEY_MAX, 12).coerceIn(4, 30)

    fun showCompleted(ctx: Context): Boolean =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getBoolean(KEY_SHOW_DONE, false)

    /** 卡片当前 Tab：0=待办 1=备忘 */
    fun tab(ctx: Context): Int =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getInt(KEY_TAB, 0)

    fun setTab(ctx: Context, tab: Int) {
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit().putInt(KEY_TAB, tab).apply()
    }

    fun set(ctx: Context, maxItems: Int? = null, showCompleted: Boolean? = null) {
        val sp = ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit()
        if (maxItems != null) sp.putInt(KEY_MAX, maxItems.coerceIn(4, 30))
        if (showCompleted != null) sp.putBoolean(KEY_SHOW_DONE, showCompleted)
        sp.apply()
    }
}
