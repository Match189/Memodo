package app.memodo.ui

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.glance.appwidget.updateAll
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import app.memodo.data.MemoItem
import app.memodo.data.TaskItem
import app.memodo.MainActivity
import app.memodo.widget.MemodoWidget
import app.memodo.widget.WidgetPrefs
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MemodoRoot(vm: MainViewModel = viewModel()) {
    var index by remember { mutableStateOf(0) }
    val titles = listOf("待办", "备忘", "图钉板", "设置")
    Scaffold(
        bottomBar = {
            NavigationBar {
                titles.forEachIndexed { i, t ->
                    NavigationBarItem(
                        selected = i == index,
                        onClick = { index = i },
                        label = { Text(t) },
                        icon = { Icon(Icons.Default.Check, null) },
                    )
                }
            }
        }
    ) { inner ->
        Box(Modifier.padding(inner)) {
            when (index) {
                0 -> TaskListView(vm)
                1 -> MemoListView(vm)
                2 -> BoardScreen(vm)
                3 -> SettingsView()
            }
        }
    }
}

@Composable
fun TaskListView(vm: MainViewModel) {
    val tasks by vm.tasks.collectAsStateWithLifecycle(emptyList())
    var input by remember { mutableStateOf("") }
    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            OutlinedTextField(
                value = input, onValueChange = { input = it },
                modifier = Modifier.weight(1f), label = { Text("新待办") },
                singleLine = true,
            )
            Spacer(Modifier.width(8.dp))
            Button(onClick = { if (input.isNotBlank()) { vm.addTask(input); input = "" } }) { Text("添加") }
        }
        Spacer(Modifier.height(12.dp))
        LazyColumn {
            items(tasks) { t ->
                ListItem(
                    headlineContent = { Text(t.title, textDecoration = if (t.completed) TextDecoration.LineThrough else null) },
                    leadingContent = {
                        Checkbox(checked = t.completed, onCheckedChange = { vm.toggleTask(t) })
                    },
                    trailingContent = {
                        IconButton(onClick = { vm.deleteTask(t.id) }) {
                            Icon(Icons.Default.Delete, null)
                        }
                    },
                )
            }
        }
    }
}

@Composable
fun MemoListView(vm: MainViewModel) {
    val memos by vm.memos.collectAsStateWithLifecycle(emptyList())
    var title by remember { mutableStateOf("") }
    var content by remember { mutableStateOf("") }
    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row {
            OutlinedTextField(value = title, onValueChange = { title = it },
                modifier = Modifier.weight(1f), label = { Text("标题") })
            Spacer(Modifier.width(8.dp))
            OutlinedTextField(value = content, onValueChange = { content = it },
                modifier = Modifier.weight(1f), label = { Text("内容") })
        }
        Spacer(Modifier.height(8.dp))
        Button(onClick = { vm.addMemo(title, content); title = ""; content = "" }) { Text("添加") }
        Spacer(Modifier.height(12.dp))
        LazyColumn {
            items(memos) { m -> MemoCardItem(m) { vm.deleteMemo(m.id) } }
        }
    }
}

@Composable
fun MemoCardItem(m: MemoItem, onDelete: () -> Unit) {
    ListItem(
        headlineContent = { Text(m.title.ifBlank { "无标题" }) },
        supportingContent = { Text(m.content, style = MaterialTheme.typography.bodySmall) },
        trailingContent = { IconButton(onClick = onDelete) { Icon(Icons.Default.Delete, null) } },
    )
}

@Composable
fun SettingsView() {
    val ctx = LocalContext.current
    var maxItems by remember { mutableStateOf(WidgetPrefs.maxItems(ctx).toFloat()) }
    var showCompleted by remember { mutableStateOf(WidgetPrefs.showCompleted(ctx)) }
    val scope = rememberCoroutineScope()

    fun refreshWidget() = scope.launch { MemodoWidget().updateAll(ctx) }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Text("设置", style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(12.dp))

        // 小组件显示设置（Flutter android_widget_settings 移植：最大条数/显示已完成）
        OutlinedCard {
            Column(Modifier.padding(16.dp)) {
                Text("桌面小组件", style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                Text("最大显示条数：${maxItems.toInt()}")
                Slider(
                    value = maxItems,
                    onValueChange = { maxItems = it },
                    onValueChangeFinished = {
                        WidgetPrefs.set(ctx, maxItems = maxItems.toInt())
                        refreshWidget()
                    },
                    valueRange = 4f..30f,
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("显示已完成", modifier = Modifier.weight(1f))
                    Switch(
                        checked = showCompleted,
                        onCheckedChange = {
                            showCompleted = it
                            WidgetPrefs.set(ctx, showCompleted = it)
                            refreshWidget()
                        },
                    )
                }
            }
        }
        Spacer(Modifier.height(12.dp))
        Text(
            "同步：桌面端已接入 WebDAV 快照；手机端 WebDAV 通道在下一轮接入。",
            style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
