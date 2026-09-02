package app.memodo.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

// Memodo 品牌橙（与 Windows 端 tint 一致，DESIGN_APPLE.md §1.1）
private val Orange = Color(0xFFD4763B)
private val OrangeDark = Color(0xFFE89A62)

// iOS 灰阶（分组底/卡面/发丝线）
private val BgLight = Color(0xFFF2F2F7)
private val BgDark = Color(0xFF1C1C1E)
private val SurfaceLight = Color(0xFFFFFFFF)
private val SurfaceDark = Color(0xFF2C2C2E)

private val LightColors = lightColorScheme(
    primary = Orange,
    onPrimary = Color.White,
    primaryContainer = Color(0xFFFFDCC7),
    onPrimaryContainer = Color(0xFF331304),
    secondary = Color(0xFF775948),
    background = BgLight,
    onBackground = Color(0xFF1C1C1E),
    surface = SurfaceLight,
    onSurface = Color(0xFF1C1C1E),
    surfaceVariant = Color(0xFFF3EDF7),
    outline = Color(0xFF857371),
    error = Color(0xFFB3261E),
)

private val DarkColors = darkColorScheme(
    primary = OrangeDark,
    onPrimary = Color(0xFF3B1D00),
    primaryContainer = Color(0xFF552F0B),
    onPrimaryContainer = Color(0xFFFFDCC7),
    secondary = Color(0xFFE7C0AB),
    background = BgDark,
    onBackground = Color(0xFFF5EFF1),
    surface = SurfaceDark,
    onSurface = Color(0xFFF5EFF1),
    surfaceVariant = Color(0xFF483F41),
    outline = Color(0xFF9F8D8F),
    error = Color(0xFFF2B8B5),
)

// 圆角阶（§1.3）：列表容器 16 / 卡 12
private val MemodoShapes = Shapes(
    small = RoundedCornerShape(8.dp),
    medium = RoundedCornerShape(12.dp),
    large = RoundedCornerShape(16.dp),
)

/**
 * darkPreference: "system" 跟随系统（默认）/ "on" 强制深色 / "off" 强制浅色。
 */
@Composable
fun MemodoTheme(
    darkPreference: String = "system",
    content: @Composable () -> Unit
) {
    val darkTheme = when (darkPreference) {
        "on" -> true
        "off" -> false
        else -> isSystemInDarkTheme()
    }
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        shapes = MemodoShapes,
        content = content,
    )
}
