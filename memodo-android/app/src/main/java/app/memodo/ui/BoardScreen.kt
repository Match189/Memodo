package app.memodo.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.memodo.R

/**
 * 图钉板 · Android（用户裁定 v2）：板面 = **全部未完成待办 + 未隐藏备忘** 的 Adaptive Grid。
 * 待办：勾选完成 → 从板面移除；备忘：眼睛切换 显示/隐藏。
 * 排序：更新时间倒序（手机端为排列视图，无自由坐标，§23/§25）。
 */
@Composable
fun BoardScreen(vm: MainViewModel) {
    val tasks by vm.tasks.collectAsStateWithLifecycle(emptyList())
    val memos by vm.memos.collectAsStateWithLifecycle(emptyList())
    val ctx = LocalContext.current

    // 板面内容自动跟随数据：未完成待办 + 未隐藏备忘
    val openTasks = tasks.filter { !it.completed && it.deletedAt == null }.sortedByDescending { it.updatedAt }
    val visibleMemos = memos.filter { it.showOnBoard && it.deletedAt == null }.sortedByDescending { it.updatedAt }
    val hiddenMemoCount = memos.count { !it.showOnBoard }
    val doneTaskCount = tasks.count { it.completed }

    // 自定义背景图 URI（app_settings 存字符串，Robolectric 不适用直接存）
    val bgUri = remember {
        ctx.getSharedPreferences("app_settings", android.content.Context.MODE_PRIVATE)
            .getString("board_bg_uri", "") ?: ""
    }
    var boardBg by remember { mutableStateOf<android.graphics.Bitmap?>(null) }
    LaunchedEffect(bgUri) {
        // 降采样解码（防大图 OOM）+ IO 线程（防主线程卡顿）
        boardBg = kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
            if (bgUri.isBlank()) return@withContext null
            try {
                val resolver = ctx.contentResolver
                val uri = android.net.Uri.parse(bgUri)
                // 先读边界
                val bounds = android.graphics.BitmapFactory.Options().apply { inJustDecodeBounds = true }
                resolver.openInputStream(uri)?.use { android.graphics.BitmapFactory.decodeStream(it, null, bounds) }
                // 目标最长边 ~2048px，计算 inSampleSize
                var sample = 1
                var w = bounds.outWidth; var h = bounds.outHeight
                if (w > 0 && h > 0) {
                    while (w / 2 >= 2048 || h / 2 >= 2048) { w /= 2; h /= 2; sample *= 2 }
                }
                val opts = android.graphics.BitmapFactory.Options().apply { inSampleSize = sample }
                resolver.openInputStream(uri)?.use { android.graphics.BitmapFactory.decodeStream(it, null, opts) }
            } catch (_: Exception) { null }
        }
    }

    Box(Modifier.fillMaxSize()) {
        // 背景层：自定义图片铺满（缩放裁剪）或软木色
        if (boardBg != null) {
            Image(
                bitmap = boardBg!!.asImageBitmap(),
                contentDescription = null,
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
            )
        } else {
            CorkTextureBackground(Modifier.fillMaxSize())
        }
        Column(
            Modifier
                .fillMaxSize()
                .padding(top = 8.dp),
        ) {
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(stringResource(R.string.board_title), color = Color(0xFF6B4A2F), style = MaterialTheme.typography.titleMedium)
            Text(
                when {
                    doneTaskCount > 0 && hiddenMemoCount > 0 ->
                        stringResource(R.string.board_subtitle_full, openTasks.size, visibleMemos.size, doneTaskCount, hiddenMemoCount)
                    doneTaskCount > 0 ->
                        stringResource(R.string.board_subtitle_done, openTasks.size, visibleMemos.size, doneTaskCount)
                    hiddenMemoCount > 0 ->
                        stringResource(R.string.board_subtitle_hidden, openTasks.size, visibleMemos.size, hiddenMemoCount)
                    else ->
                        stringResource(R.string.board_subtitle, openTasks.size, visibleMemos.size)
                },
                color = Color(0xFF6B4A2F).copy(alpha = 0.7f),
                style = MaterialTheme.typography.bodySmall,
            )
        }

        if (openTasks.isEmpty() && visibleMemos.isEmpty()) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(
                    stringResource(R.string.board_empty),
                    color = Color(0x886B4A2F),
                    textAlign = TextAlign.Center,
                )
            }
        } else {
            LazyVerticalGrid(
                columns = GridCells.Adaptive(minSize = 160.dp),
                contentPadding = PaddingValues(12.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier.fillMaxSize(),
            ) {
                items(openTasks, key = { "t" + it.id }) { t ->
                    NoteCard(
                        color = Color(0xFFFFF9C4),
                        pinTint = Color(0xFFE85A4F),
                        title = t.title,
                        done = false,
                        onToggle = { vm.toggleTask(t) },
                        createdAt = t.createdAt,
                    )
                }
                items(visibleMemos, key = { "m" + it.id }) { m ->
                    NoteCard(
                        color = Color(0xFFE3F2FD),
                        pinTint = Color(0xFF4A90E2),
                        title = m.title.ifBlank { stringResource(R.string.untitled) },
                        body = m.content.takeIf { it.isNotBlank() },
                        done = false,
                        onToggle = { vm.toggleMemoShow(m) },
                        toggleIsHide = true,
                        createdAt = m.createdAt,
                    )
                }
            }
        }
        }
    }
}

@Composable
private fun NoteCard(
    color: Color,
    pinTint: Color,
    title: String,
    body: String? = null,
    done: Boolean,
    onToggle: () -> Unit,
    toggleIsHide: Boolean = false,
    createdAt: Long = 0,
    context: android.content.Context = LocalContext.current,
) {
    Surface(
        shape = RoundedCornerShape(4.dp),
        color = color,
        tonalElevation = 2.dp,
        shadowElevation = 3.dp,
    ) {
        Column(Modifier.padding(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                // 待办=圆形勾 / 备忘=眼睛斜线（用户裁定）
                if (!toggleIsHide) {
                    Checkbox(checked = done, onCheckedChange = { onToggle() })
                } else {
                    IconButton(onClick = onToggle, modifier = Modifier.size(28.dp)) {
                        Icon(
                            Icons.Filled.VisibilityOff,
                            contentDescription = stringResource(R.string.memo_hide_desc),
                            tint = pinTint,
                        )
                    }
                }
                Spacer(Modifier.width(4.dp))
                Text(
                    title,
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier.weight(1f),
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            if (!body.isNullOrBlank()) {
                Spacer(Modifier.height(4.dp))
                Text(body, style = MaterialTheme.typography.bodySmall, color = Color(0xFF6B5B49),
                    maxLines = 3, overflow = TextOverflow.Ellipsis)
            }
            if (createdAt > 0) {
                Text(formatRelativeTime(createdAt, context), style = MaterialTheme.typography.labelSmall,
                    color = Color(0x886B5B49), modifier = Modifier.align(Alignment.End))
            }
        }
    }
}

internal fun formatRelativeTime(ms: Long, context: android.content.Context? = null): String {
    if (context != null) {
        val fmt = context.getSharedPreferences("app_settings", android.content.Context.MODE_PRIVATE)
            .getString("time_format", "relative") ?: "relative"
        if (fmt == "absolute") {
            val sdf = java.text.SimpleDateFormat("yyyy/MM/dd HH:mm", java.util.Locale.getDefault())
            return sdf.format(java.util.Date(ms))
        }
    }
    val diff = System.currentTimeMillis() - ms
    val mins = diff / 60_000
    val res = context?.resources
    if (res != null) {
        if (mins < 1) return res.getString(R.string.time_just_now)
        if (mins < 60) return res.getString(R.string.time_minutes_ago, mins)
        val hours = mins / 60
        if (hours < 24) return res.getString(R.string.time_hours_ago, hours)
        val days = hours / 24
        if (days < 30) return res.getString(R.string.time_days_ago, days)
    } else {
        if (mins < 1) return "刚刚"
        if (mins < 60) return "${mins}分钟前"
        val hours = mins / 60
        if (hours < 24) return "${hours}h"
        val days = hours / 24
        if (days < 30) return "${days}d"
    }
    val sdf = java.text.SimpleDateFormat("MM/dd", java.util.Locale.getDefault())
    return sdf.format(java.util.Date(ms))
}

/**
 * 软木纹理背景 v2（与 Windows CorkTexture 同构，种子固定防闪烁）：
 * 四段对角渐变 + 柔光池 + 软木颗粒斑 + 细砂噪点 + 图钉点阵。纯图形、语言中立。
 */
@Composable
private fun CorkTextureBackground(modifier: Modifier = Modifier) {
    // 细砂噪点：位置(x,y 比例) + 半径
    val speckles = remember {
        val r = java.util.Random(20260829)
        List(220) { Triple(r.nextDouble(), r.nextDouble(), 0.9f + r.nextFloat() * 1.5f) }
    }
    // 软木颗粒斑：亮斑随机铺；暗斑只落四角（远离主光池）
    val flecks = remember {
        val r = java.util.Random(987654)
        val corners = listOf(0.07 to 0.07, 0.93 to 0.09, 0.08 to 0.93, 0.92 to 0.91)
        List(14) { i ->
            if (i % 2 == 0) {
                Triple(r.nextDouble(), r.nextDouble(), 20f + r.nextFloat() * 34f) to Color(0x30FFF6E0)
            } else {
                val (cx, cy) = corners[(i / 2) % corners.size]
                Triple(cx + (r.nextDouble() - 0.5) * 0.12, cy + (r.nextDouble() - 0.5) * 0.12,
                    20f + r.nextFloat() * 34f) to Color(0x066B4A2F)
            }
        }
    }
    val density = androidx.compose.ui.platform.LocalDensity.current.density
    Canvas(modifier) {
        // 1. 四段对角渐变（左上亮 → 右下深）
        drawRect(Brush.linearGradient(
            listOf(Color(0xFFF7EDD9), Color(0xFFEAD3AC), Color(0xFFD9B38C), Color(0xFFC9A176)),
            start = Offset.Zero, end = Offset(size.width, size.height),
        ))
        // 2. 柔光池：左上主光 + 右下补光
        val r1 = size.minDimension * 0.95f
        drawCircle(Brush.radialGradient(
            listOf(Color(0x66FFF3DC), Color(0x00FFF3DC)),
            center = Offset.Zero, radius = r1),
            radius = r1, center = Offset.Zero)
        val r2 = size.minDimension * 0.8f
        drawCircle(Brush.radialGradient(
            listOf(Color(0x33FFF3DC), Color(0x00FFF3DC)),
            center = Offset(size.width, size.height), radius = r2),
            radius = r2, center = Offset(size.width, size.height))
        // 3. 软木颗粒斑
        flecks.forEach { (p, c) ->
            drawCircle(c, radius = p.third * density,
                center = Offset((p.first * size.width).toFloat(), (p.second * size.height).toFloat()))
        }
        // 4. 细砂噪点
        speckles.forEach { (x, y, r) ->
            drawCircle(Color(0x1A000000), radius = r * density,
                center = Offset((x * size.width).toFloat(), (y * size.height).toFloat()))
        }
        // 5. 图钉点阵（含蓄）
        val step = 50f * density
        var gx = step / 2f
        while (gx < size.width) {
            var gy = step / 2f
            while (gy < size.height) {
                drawCircle(Color(0x0A6B4A2F), radius = 1.6f * density, center = Offset(gx, gy))
                gy += step
            }
            gx += step
        }
    }
}
