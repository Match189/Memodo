package app.memodo.ui

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.glance.appwidget.updateAll
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import app.memodo.data.MemoItem
import app.memodo.data.TaskItem
import app.memodo.data.ServerSync
import app.memodo.data.WebDavSync
import app.memodo.MainActivity
import app.memodo.widget.MemodoWidget
import app.memodo.widget.WidgetPrefs
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

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
            items(memos) { m ->
                ListItem(
                    leadingContent = {
                        Checkbox(checked = m.completed, onCheckedChange = { vm.toggleMemoDone(m) })
                    },
                    headlineContent = {
                        Text(
                            m.title.ifBlank { "无标题" },
                            textDecoration = if (m.completed) TextDecoration.LineThrough else null,
                        )
                    },
                    supportingContent = { Text(m.content, style = MaterialTheme.typography.bodySmall) },
                    trailingContent = {
                        IconButton(onClick = { vm.deleteMemo(m.id) }) { Icon(Icons.Default.Delete, null) }
                    },
                )
            }
        }
    }
}

@Composable
@OptIn(ExperimentalMaterial3Api::class)
fun SettingsView() {
    val ctx = LocalContext.current
    var maxItems by remember { mutableStateOf(WidgetPrefs.maxItems(ctx).toFloat()) }
    var showCompleted by remember { mutableStateOf(WidgetPrefs.showCompleted(ctx)) }
    var syncMode by remember { mutableStateOf(WebDavSync.mode(ctx)) } // local | webdav | server
    var syncUrl by remember { mutableStateOf(WebDavSync.url(ctx)) }
    var syncUser by remember { mutableStateOf(WebDavSync.user(ctx)) }
    var syncPass by remember { mutableStateOf(WebDavSync.pass(ctx)) }
    var serverUrl by remember { mutableStateOf(ServerSync.url(ctx)) }
    var serverUser by remember { mutableStateOf(ServerSync.user(ctx)) }
    var serverPass by remember { mutableStateOf(ServerSync.pass(ctx)) }
    var syncing by remember { mutableStateOf(false) }
    var syncMsg by remember { mutableStateOf("尚未同步") }
    val scope = rememberCoroutineScope()

    fun refreshWidget() = scope.launch { MemodoWidget().updateAll(ctx) }

    fun runSync() {
        syncing = true
        scope.launch {
            val r = when (syncMode) {
                "webdav" -> WebDavSync.run(ctx)
                "server" -> ServerSync.run(ctx)
                else -> WebDavSync.Result(false, "仅本地模式，不同步")
            }
            syncing = false
            syncMsg = r.message
            if (r.ok) refreshWidget()
        }
    }

    Column(
        Modifier.fillMaxSize().padding(16.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("设置", style = MaterialTheme.typography.titleLarge)

        // 同步方式（用户裁定补全：仅本地 / WebDAV / 自建服务器）
        OutlinedCard {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("同步", style = MaterialTheme.typography.titleMedium)
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                    listOf(
                        "local" to "仅本地",
                        "webdav" to "WebDAV",
                        "server" to "自建服务器",
                    ).forEach { (tag, label) ->
                        FilterChip(
                            selected = syncMode == tag,
                            onClick = {
                                syncMode = tag
                                WebDavSync.setMode(ctx, tag)
                            },
                            label = { Text(label) },
                        )
                    }
                }

                when (syncMode) {
                    "webdav" -> {
                        OutlinedTextField(
                            value = syncUrl, onValueChange = { syncUrl = it },
                            label = { Text("服务器地址") }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = syncUser, onValueChange = { syncUser = it },
                            label = { Text("账号") }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = syncPass, onValueChange = { syncPass = it },
                            label = { Text("应用密码") }, singleLine = true,
                            visualTransformation = PasswordVisualTransformation(),
                            modifier = Modifier.fillMaxWidth(),
                        )
                    }
                    "server" -> {
                        OutlinedTextField(
                            value = serverUrl, onValueChange = { serverUrl = it },
                            label = { Text("服务器地址（http(s)://…）") }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = serverUser, onValueChange = { serverUser = it },
                            label = { Text("邮箱") }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = serverPass, onValueChange = { serverPass = it },
                            label = { Text("密码") }, singleLine = true,
                            visualTransformation = PasswordVisualTransformation(),
                            modifier = Modifier.fillMaxWidth(),
                        )
                    }
                    else -> Text(
                        "仅本地模式：数据只保存在手机上，不同步。",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }

                if (syncMode != "local") {
                    Button(
                        onClick = {
                            if (syncMode == "webdav") WebDavSync.save(ctx, syncUrl, syncUser, syncPass)
                            else ServerSync.save(ctx, serverUrl, serverUser, serverPass)
                            runSync()
                        },
                        enabled = !syncing,
                    ) { Text(if (syncing) "同步中…" else "立即同步") }
                }
                Text(syncMsg, style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }

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
    }
}
