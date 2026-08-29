package app.memodo.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.PushPin
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.memodo.data.CardItem
import app.memodo.data.CardLayoutItem
import app.memodo.data.MemoItem
import app.memodo.data.TaskItem
import kotlinx.coroutines.launch
import kotlin.math.roundToLong

/**
 * 图钉板（任务书 §9/§12-15）。卡片只引用实体（ref_type+ref_uuid），不复制内容。
 * 交互：拖动移动（8px 吸附）、右下角缩放（最小 140x100）、旋转滑杆、钉/取消钉。
 * 布局落盘到 card_layouts(platform="android")，与 Windows 同表不同行。
 */
private data class CardVisual(
    val x: Double, val y: Double, val w: Double, val h: Double, val rot: Double
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BoardScreen(vm: MainViewModel) {
    val board by vm.board.collectAsStateWithLifecycle(null)
    val cards by vm.cards.collectAsStateWithLifecycle(emptyList())
    val tasks by vm.tasks.collectAsStateWithLifecycle(emptyList())
    val memos by vm.memos.collectAsStateWithLifecycle(emptyList())

    var visuals by remember { mutableStateOf<Map<String, CardVisual>>(emptyMap()) }
    var selected by remember { mutableStateOf<String?>(null) }
    var showPicker by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    // 载入/刷新每张卡片的布局
    LaunchedEffect(cards) {
        val map = LinkedHashMap<String, CardVisual>()
        for (c in cards) {
            val lay = vm.getLayout(c.id) ?: CardLayoutItem(cardId = c.id, platform = "android", updatedAt = 0)
            map[c.id] = CardVisual(lay.x, lay.y, lay.width, lay.height, lay.rotation)
        }
        visuals = map
    }

    fun snap(v: CardVisual): CardVisual = v.copy(
        x = (v.x / 8.0).roundToLong() * 8.0,
        y = (v.y / 8.0).roundToLong() * 8.0,
        w = maxOf(140.0, (v.w / 8.0).roundToLong() * 8.0),
        h = maxOf(100.0, (v.h / 8.0).roundToLong() * 8.0),
    )

    fun persist(card: CardItem, v: CardVisual) {
        val s = snap(v)
        visuals = visuals + (card.id to s)
        val lay = CardLayoutItem(
            cardId = card.id, platform = "android",
            x = s.x, y = s.y, width = s.w, height = s.h, rotation = s.rot,
            updatedAt = System.currentTimeMillis(),
        )
        scope.launch { vm.saveLayout(lay) }
    }

    val taskById = remember(tasks) { tasks.associateBy { it.id } }
    val memoById = remember(memos) { memos.associateBy { it.id } }
    val pinnedUuids = remember(cards) { cards.map { it.refUuid }.toSet() }

    Scaffold(
        floatingActionButton = {
            FloatingActionButton(onClick = { showPicker = true }) {
                Icon(Icons.Default.Add, contentDescription = "钉卡片")
            }
        }
    ) { inner ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(inner)
                .background(Color(0xFFD9B38C))
        ) {
            if (board == null) {
                Text("加载中…", Modifier.align(Alignment.Center), color = Color(0x886B4A2F))
            } else if (cards.isEmpty()) {
                Text(
                    "还没有钉住的卡片\n点右下角 + 把待办/备忘钉上来",
                    Modifier.align(Alignment.Center), color = Color(0x886B4A2F)
                )
            } else {
                cards.forEach { card ->
                    val v = visuals[card.id] ?: return@forEach
                    val isSel = selected == card.id
                    val content: (@Composable ColumnScope.() -> Unit) = {
                        when (card.refType) {
                            "todo" -> {
                                val t = taskById[card.refUuid]
                                Text(
                                    t?.title ?: "(已删除待办)",
                                    style = MaterialTheme.typography.titleMedium,
                                    textDecoration = if (t?.completed == true) TextDecoration.LineThrough else null,
                                )
                            }
                            "memo" -> {
                                val m = memoById[card.refUuid]
                                Text(m?.title?.ifBlank { "无标题" } ?: "(已删除备忘)",
                                    style = MaterialTheme.typography.titleSmall)
                                if (m != null && m.content.isNotBlank()) {
                                    Spacer(Modifier.height(4.dp))
                                    Text(m.content, style = MaterialTheme.typography.bodySmall)
                                }
                            }
                        }
                    }
                    Card(
                        modifier = Modifier
                            .offset { IntOffset(v.x.toInt(), v.y.toInt()) }
                            .size(v.w.dp, v.h.dp)
                            .graphicsLayer { rotationZ = v.rot.toFloat() }
                            .pointerInput(card.id) {
                                detectDragGestures(
                                    onDragStart = { selected = card.id },
                                    onDrag = { change, drag ->
                                        change.consume()
                                        val cur = visuals[card.id] ?: return@detectDragGestures
                                        visuals = visuals + (card.id to cur.copy(
                                            x = cur.x + drag.x, y = cur.y + drag.y))
                                    },
                                    onDragEnd = { persist(card, visuals[card.id] ?: return@detectDragGestures) }
                                )
                            },
                        shape = RoundedCornerShape(8.dp),
                        colors = CardDefaults.cardColors(containerColor = Color.White),
                        elevation = CardDefaults.cardElevation(if (isSel) 8.dp else 3.dp),
                    ) {
                        Box(Modifier.fillMaxSize().padding(10.dp)) {
                            Column(Modifier.fillMaxSize()) { content() }
                            if (isSel) {
                                IconButton(
                                    onClick = { vm.unpin(card.id); selected = null },
                                    modifier = Modifier.align(Alignment.TopEnd).size(28.dp)
                                ) {
                                    Icon(Icons.Default.Close, null, tint = Color(0xFFB00020))
                                }
                            }
                            // 右下角缩放手柄
                            Box(
                                modifier = Modifier
                                    .align(Alignment.BottomEnd)
                                    .size(22.dp)
                                    .background(Color(0xFF0080A0), RoundedCornerShape(4.dp))
                                    .pointerInput(card.id) {
                                        detectDragGestures { change, drag ->
                                            change.consume()
                                            val cur = visuals[card.id] ?: return@detectDragGestures
                                            visuals = visuals + (card.id to cur.copy(
                                                w = cur.w + drag.x, h = cur.h + drag.y))
                                        }
                                    }
                            )
                        }
                    }
                }
            }

            // 选中卡片的旋转滑杆
            selected?.let { sid ->
                val sv = visuals[sid] ?: return@let
                val card = cards.firstOrNull { it.id == sid } ?: return@let
                Surface(
                    tonalElevation = 6.dp, shadowElevation = 6.dp,
                    modifier = Modifier.align(Alignment.BottomCenter).fillMaxWidth().padding(12.dp)
                ) {
                    Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.PushPin, null, tint = Color(0xFF0080A0))
                        Slider(
                            value = sv.rot.toFloat(), onValueChange = { rot ->
                                val nv = sv.copy(rot = rot.toDouble())
                                visuals = visuals + (sid to nv)
                                persist(card, nv)
                            },
                            valueRange = -15f..15f, steps = 60,
                            modifier = Modifier.weight(1f)
                        )
                        Text("%.1f°".format(sv.rot), Modifier.width(52.dp))
                    }
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

@OptIn(ExperimentalMaterial3Api::class)
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
            LazyColumn(modifier = Modifier.heightIn(max = 360.dp)) {
                if (tasks.isEmpty() && memos.isEmpty()) {
                    item { Text("没有可钉的内容（待办/备忘均为空或已钉）") }
                }
                items(tasks) { t ->
                    ListItem(
                        headlineContent = { Text(t.title) },
                        leadingContent = { Icon(Icons.Default.PushPin, null) },
                        modifier = Modifier.clickable { onPinTask(t) }
                    )
                }
                items(memos) { m ->
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
