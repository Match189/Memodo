package app.memodo

import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.mutableStateOf
import androidx.lifecycle.lifecycleScope
import app.memodo.ui.MemodoRoot
import app.memodo.ui.theme.MemodoTheme
import kotlinx.coroutines.launch
import java.util.Locale

class MainActivity : ComponentActivity() {

    companion object {
        private const val PREFS = "app_settings"
        private const val KEY_LANG = "language"
        private const val KEY_DARK = "dark_mode"

        /** 桌面卡片唤起时指定打开的底部 Tab：0=待办 1=备忘 */
        const val EXTRA_OPEN_TAB = "open_tab"

        fun getLanguage(ctx: Context): String =
            ctx.getSharedPreferences(PREFS, MODE_PRIVATE)
                .getString(KEY_LANG, "") ?: ""

        fun setLanguage(ctx: Context, lang: String) {
            ctx.getSharedPreferences(PREFS, MODE_PRIVATE).edit()
                .putString(KEY_LANG, lang).apply()
        }

        /** 深色模式：system（跟随系统，默认）/ on / off */
        fun getDarkMode(ctx: Context): String =
            ctx.getSharedPreferences(PREFS, MODE_PRIVATE)
                .getString(KEY_DARK, "system") ?: "system"

        fun setDarkModeStatic(ctx: Context, mode: String) {
            ctx.getSharedPreferences(PREFS, MODE_PRIVATE).edit()
                .putString(KEY_DARK, mode).apply()
        }
    }

    /** 当前底部 Tab；widget 唤起/onNewIntent 时更新，Compose 侧随 initialTab 重置 */
    private val openTab = mutableStateOf(0)

    /** 深色模式（Compose 侧订阅，切换即时重绘）；onCreate 才能读 prefs（构造期 base context 未 attach） */
    private val darkMode = mutableStateOf("system")

    /** 系统分享进来的草稿文本（分享 → 保存为备忘） */
    private val sharedText = mutableStateOf<String?>(null)

    override fun attachBaseContext(newBase: Context) {
        super.attachBaseContext(applyLocale(newBase))
    }

    private fun applyLocale(base: Context): Context {
        val saved = getLanguage(base)
        if (saved.isEmpty()) return base // follow system
        val locale = Locale(saved)
        Locale.setDefault(locale)
        val config = base.resources.configuration
        config.setLocale(locale)
        return base.createConfigurationContext(config)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        openTab.value = intent?.getIntExtra(EXTRA_OPEN_TAB, 0) ?: 0
        darkMode.value = getDarkMode(this)
        readSharedText(intent)
        enableEdgeToEdge()
        setContent {
            MemodoTheme(darkPreference = darkMode.value) {
                MemodoRoot(initialTab = if (sharedText.value != null) 1 else openTab.value, sharedText = sharedText.value)
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        // 已在前台时再次从卡片唤起：切到指定 Tab，不重建实例
        openTab.value = intent.getIntExtra(EXTRA_OPEN_TAB, 0)
        if (readSharedText(intent)) {
            sharedText.value = pendingShared
            pendingShared = null
        }
    }

    private var pendingShared: String? = null

    /** 从 ACTION_SEND 提取文本。 */
    private fun readSharedText(intent: Intent?): Boolean {
        if (intent?.action == android.content.Intent.ACTION_SEND &&
            intent.type == "text/plain"
        ) {
            val text = intent.getStringExtra(android.content.Intent.EXTRA_TEXT)
            if (!text.isNullOrBlank()) {
                pendingShared = text
                return true
            }
        }
        return false
    }

    /** 切换深色模式（设置页调用），即时重绘。 */
    fun setDarkMode(mode: String) {
        setDarkModeStatic(this, mode)
        darkMode.value = mode
    }

    /** 切换语言后调用，立即重建 Activity，并同步刷新桌面卡片文案。 */
    fun switchLanguage(lang: String) {
        setLanguage(this, lang)
        lifecycleScope.launch {
            app.memodo.widget.WidgetRefresher.refreshAll(applicationContext)
        }
        recreate()
    }
}
