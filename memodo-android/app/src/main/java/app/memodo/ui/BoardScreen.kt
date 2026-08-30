package app.memodo.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.PushPin
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.memodo.data.CardItem
import app.memodo.data.MemoItem
import app.memodo.data.TaskItem

/**
 * 图钉板 · Android（蓝图 §23）：**不做无限画布**，用 Adaptive Grid + 卡片。
 * 数据与 Windows 同源（cards 引用 tasks/memos，或 idea/checklist 内联）；
 * 排序用 cards.sort（随同步传播），提供上移/下移微调。
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BoardScreen(vm: MainViewModel) {
    val cards by vm.cards.collectAsStateWithLifecycle(emptyList())
    val tasks by vm.tasks.collectAsStateWithLifecycle(emptyList())
    val memos by vm.memos.collectAsStateWithLifecycle(emptyList())

    var showPicker by remember { mutableStateOf(false) }
    val taskById = remember(tasks) { tasks.associateBy { it.id } }
    val memoById = remember(memos) { memos.associateBy { it.id } }
    val pinnedUuids = remember(cards) { cards.map { it.refUuid }.toSet() }

    Scaffold(
        containerColor = Color(0xFFE8D5BC), // Cork 底（Hybrid 主题，§17）
        floatingActionButton = {
            FloatingActionButton(onClick = { showPicker = true }) {
                Icon(Icons.Default.Add, contentDescription = "钉卡片")
            }
        }
    ) { inner ->
        if (cards.isEmpty()) {
            Box(Modifier.fillMaxSize().padding(inner), contentAlignment = Alignment.Center) {
                Text(
                    "还没有钉住的卡片\n点右下角 + 把待办/备忘钉上来",
                    color = Color(0x886B4A2F)
                )
            }
        } else {
            LazyVerticalGrid(
                columns = GridCells.Adaptive(minSize = 160.dp),
                contentPadding = PaddingValues(12.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier.fillMaxSize().padding(inner),
            ) {
                items(cards, key = { it.id }) { card ->
                    GridCard(
                        card = card,
                        task = taskById[card.refUuid],
                        memo = memoById[card.refUuid],
                        onToggle = { t -> vm.toggleTask(t) },
                        onUnpin = { vm.unpin(card.id) },
                        onMoveUp = { vm.moveCard(card, -1) },
                        onMoveDown = { vm.moveCard(card, +1) },
                    )
                }
            }
        }
    }

    if (showPicker) {
        PinPicker(
            tasks = tasks.filter { it.id !in pinnedUuids },
            memos = memos.filter { it.id !in pinnedUuids },
            onPinTask = { vm.pinTodo(it.id); showPicker = false },
            onPinMemo = { vm.pinMemo(it.id); showPicker = false },
            onDismiss = { showPicker = false },
        )
    }
}

@Composable
private fun GridCard(
    card: CardItem,
    task: TaskItem?,
    memo: MemoItem?,
    onToggle: (TaskItem) -> Unit,
    onUnpin: () -> Unit,
    onMoveUp: () -> Unit,
    onMoveDown: () -> Unit,
) {
    Surface(
        shape = RoundedCornerShape(4.dp), // 设计文档：便签小圆角
        color = noteTint(card.noteColor) ?: tint(card.color),
        tonalElevation = 2.dp,
        shadowElevation = 3.dp,
    ) {
        Column(Modifier.padding(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Default.PushPin, contentDescription = null,
                    tint = pinTint(card.color), modifier = Modifier.size(16.dp),
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    when {
                        card.refType == "todo" -> task?.title ?: "(已删除待办)"
                        card.refType == "memo" -> memo?.title?.ifBlank { "无标题" } ?: "(已删除备忘)"
                        else -> card.title.ifBlank { "新卡片" }
                    },
                    style = MaterialTheme.typography.titleSmall,
                    textDecoration = if (task?.completed == true) TextDecoration.LineThrough else null,
                    modifier = Modifier.weight(1f),
                )
                IconButton(onClick = onUnpin, modifier = Modifier.size(24.dp)) {
                    Icon(Icons.Default.Close, "取消钉", tint = Color(0xFFB00020), modifier = Modifier.size(15.dp))
                }
            }
            if (card.refType == "memo" && !memo?.content.isNullOrBlank()) {
                Spacer(Modifier.height(4.dp))
                Text(memo!!.content, style = MaterialTheme.typography.bodySmall, color = Color(0xFF6B5B49))
            }
            if (card.refType == "idea" && card.content.isNotBlank()) {
                Spacer(Modifier.height(4.dp))
                Text(card.content, style = MaterialTheme.typography.bodySmall, color = Color(0xFF6B5B49))
            }
            if (card.refType == "checklist" && card.content.isNotBlank()) {
                Spacer(Modifier.height(4.dp))
                card.content.split('\n').filter { it.isNotBlank() }.take(6).forEach { line ->
                    Text(
                        if (line.startsWith("☑")) "☑ ${line.drop(1).trim()}" else "☐ ${line.dropWhile { it == '☐' }.trim()}",
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
            }
            if (card.refType == "todo" && task != null) {
                Spacer(Modifier.height(4.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = task.completed, onCheckedChange = { onToggle(task) })
                    Text("完成", style = MaterialTheme.typography.bodySmall)
                }
            }
            // 网格排序微调（§23 grid/order）
            Row(horizontalArrangement = Arrangement.End, modifier = Modifier.fillMaxWidth()) {
                IconButton(onClick = onMoveUp, modifier = Modifier.size(24.dp)) {
                    Icon(Icons.Default.ArrowUpward, "上移", modifier = Modifier.size(14.dp))
                }
                IconButton(onClick = onMoveDown, modifier = Modifier.size(24.dp)) {
                    Icon(Icons.Default.ArrowDownward, "下移", modifier = Modifier.size(14.dp))
                }
            }
        }
    }
}

/** 便签纸色（设计文档 5 色）；空 = null 回退图钉色染纸。 */
private fun noteTint(color: String): Color? = when (color) {
    "yellow" -> Color(0xFFFFF9C4)
    "pink" -> Color(0xFFFCE4EC)
    "blue" -> Color(0xFFE3F2FD)
    "green" -> Color(0xFFE8F5E9)
    "orange" -> Color(0xFFFFF3E0)
    else -> null
}

@Composable
private fun tint(color: String): Color = when (color) {
    "yellow" -> Color(0xFFFDF3D0)
    "blue" -> Color(0xFFE3EEFA)
    "green" -> Color(0xFFE6F4EA)
    else -> Color(0xFFFDF8EC) // Paper/Cream 默认
}

private fun pinTint(color: String): Color = when (color) {
    "yellow" -> Color(0xFFB8860B)
    "blue" -> Color(0xFF2F7FD6)
    "green" -> Color(0xFF3EA65B)
    else -> Color(0xFFD64545)
}

@Composable
private fun PinPicker(
    tasks: List<TaskItem>,
    memos: List<MemoItem>,
    onPinTask: (TaskItem) -> Unit,
    onPinMemo: (MemoItem) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onDismiss) { Text("取消") } },
        title = { Text("钉到图钉板") },
        text = {
            Column(Modifier.heightIn(max = 360.dp)) {
                if (tasks.isEmpty() && memos.isEmpty()) {
                    Text("没有可钉的内容（待办/备忘均为空或已钉）")
                }
                tasks.forEach { t ->
                    ListItem(
                        headlineContent = { Text(t.title) },
                        leadingContent = { Icon(Icons.Default.PushPin, null) },
                        modifier = Modifier.clickable { onPinTask(t) }
                    )
                }
                memos.forEach { m ->
                    ListItem(
                        headlineContent = { Text(m.title.ifBlank { "无标题" }) },
                        supportingContent = if (m.content.isNotBlank()) ({ Text(m.content) }) else null,
                        leadingContent = { Icon(Icons.Default.PushPin, null) },
                        modifier = Modifier.clickable { onPinMemo(m) }
                    )
                }
            }
        }
    )
}
