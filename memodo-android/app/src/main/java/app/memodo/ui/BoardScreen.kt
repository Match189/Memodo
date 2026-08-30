package app.memodo.ui

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
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle

/**
 * 图钉板 · Android（用户裁定 v2）：板面 = **全部未完成待办 + 未隐藏备忘** 的 Adaptive Grid。
 * 待办：勾选完成 → 从板面移除；备忘：眼睛切换 显示/隐藏。
 * 排序：更新时间倒序（手机端为排列视图，无自由坐标，§23/§25）。
 */
@Composable
fun BoardScreen(vm: MainViewModel) {
    val tasks by vm.tasks.collectAsStateWithLifecycle(emptyList())
    val memos by vm.memos.collectAsStateWithLifecycle(emptyList())

    // 板面内容自动跟随数据：未完成待办 + 未隐藏备忘
    val openTasks = tasks.filter { !it.completed }.sortedByDescending { it.updatedAt }
    val visibleMemos = memos.filter { it.showOnBoard }.sortedByDescending { it.updatedAt }
    val hiddenMemoCount = memos.count { !it.showOnBoard }
    val doneTaskCount = tasks.count { it.completed }

    Column(
        Modifier
            .fillMaxSize()
            .background(Color(0xFFC9A66B)) // Cork（设计稿）
            .padding(top = 8.dp),
    ) {
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text("📌 钉板", color = Color(0xFF6B4A2F), style = MaterialTheme.typography.titleMedium)
            Text(
                buildString {
                    append("待办 ${openTasks.size} · 备忘 ${visibleMemos.size}")
                    if (doneTaskCount > 0) append(" · 已完成 $doneTaskCount")
                    if (hiddenMemoCount > 0) append(" · 已隐藏 $hiddenMemoCount")
                },
                color = Color(0xFF6B4A2F).copy(alpha = 0.7f),
                style = MaterialTheme.typography.bodySmall,
            )
        }

        if (openTasks.isEmpty() && visibleMemos.isEmpty()) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(
                    "板面是空的\n去待办/备忘页添加内容",
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
                    )
                }
                items(visibleMemos, key = { "m" + it.id }) { m ->
                    NoteCard(
                        color = Color(0xFFE3F2FD),
                        pinTint = Color(0xFF4A90E2),
                        title = m.title.ifBlank { "无标题" },
                        body = m.content.takeIf { it.isNotBlank() },
                        done = false,
                        onToggle = { vm.toggleMemoShow(m) },
                        toggleIsHide = true,
                    )
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
                            contentDescription = "不在钉板显示",
                            tint = pinTint,
                        )
                    }
                }
                Spacer(Modifier.width(4.dp))
                Text(
                    title,
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier.weight(1f),
                )
            }
            if (!body.isNullOrBlank()) {
                Spacer(Modifier.height(4.dp))
                Text(body, style = MaterialTheme.typography.bodySmall, color = Color(0xFF6B5B49))
            }
        }
    }
}
