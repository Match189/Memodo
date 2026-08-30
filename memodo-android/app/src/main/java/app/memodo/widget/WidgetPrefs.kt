package app.memodo.widget

import android.content.Context

/**
 * 小组件显示设置（Flutter android_widget_settings.dart 移植）：
 * 最大条数（4-30，默认 12）/ 显示已完成（默认 false）。存 SharedPreferences，本机态不进同步协议。
 */
object WidgetPrefs {
    private const val FILE = "widget_settings"
    private const val KEY_MAX = "maxItems"
    private const val KEY_SHOW_DONE = "showCompleted"

    fun maxItems(ctx: Context): Int =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getInt(KEY_MAX, 12).coerceIn(4, 30)

    fun showCompleted(ctx: Context): Boolean =
        ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).getBoolean(KEY_SHOW_DONE, false)

    fun set(ctx: Context, maxItems: Int? = null, showCompleted: Boolean? = null) {
        val sp = ctx.getSharedPreferences(FILE, Context.MODE_PRIVATE).edit()
        if (maxItems != null) sp.putInt(KEY_MAX, maxItems.coerceIn(4, 30))
        if (showCompleted != null) sp.putBoolean(KEY_SHOW_DONE, showCompleted)
        sp.apply()
    }
}
