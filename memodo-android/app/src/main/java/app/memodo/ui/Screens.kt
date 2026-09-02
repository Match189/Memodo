package app.memodo.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Cloud
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.EditCalendar
import androidx.compose.material.icons.filled.EditNote
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.PushPin
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import app.memodo.data.BackupService
import app.memodo.data.MemoItem
import app.memodo.data.SyncCrypto
import app.memodo.data.SyncStatus
import app.memodo.data.TaskItem
import app.memodo.data.ServerSync
import app.memodo.data.SyncScheduler
import app.memodo.data.WebDavSync
import app.memodo.data.Repo
import app.memodo.MainActivity
import kotlinx.coroutines.flow.first
import androidx.compose.ui.res.stringResource
import app.memodo.R
import app.memodo.widget.WidgetRefresher
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** 到期时间短格式（列表/卡片徽标用）。 */
private fun formatDue(ms: Long): String =
    SimpleDateFormat("MM-dd", Locale.getDefault()).format(Date(ms))

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MemodoRoot(vm: MainViewModel = viewModel(), initialTab: Int = 0, sharedText: String? = null) {
    var index by remember(initialTab) { mutableStateOf(initialTab) }
    val titles = listOf(stringResource(R.string.nav_todo), stringResource(R.string.nav_memo), stringResource(R.string.nav_board), stringResource(R.string.nav_settings))
    val icons = listOf(Icons.Default.Check, Icons.Default.EditNote, Icons.Default.PushPin, Icons.Default.Settings)
    val snackbarHostState = remember { SnackbarHostState() }
    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            // 主界面同步状态指示器（点按立即同步）
            SyncStatusBar(vm)
        },
        bottomBar = {
            NavigationBar {
                titles.forEachIndexed { i, t ->
                    NavigationBarItem(
                        selected = i == index,
                        onClick = { index = i },
                        label = { Text(t) },
                        icon = { Icon(icons[i], null) },
                    )
                }
            }
        }
    ) { inner ->
        Box(Modifier.padding(inner)) {
            when (index) {
                0 -> TaskListView(vm, snackbarHostState)
                1 -> MemoListView(vm, snackbarHostState, sharedText)
                2 -> BoardScreen(vm)
                3 -> SettingsView()
            }
        }
    }
}

/** 顶栏同步状态：绿勾=已同步 / 旋转云=同步中 / 灰叉云=未同步。点击立即同步。 */
@Composable
private fun SyncStatusBar(vm: MainViewModel) {
    val state by SyncStatus.state.collectAsStateWithLifecycle()
    val ctx = LocalContext.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 6.dp)
            .clickable { vm.syncNow() },
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Spacer(Modifier.weight(1f))
        val (icon, tint, label) = when (state) {
            SyncStatus.State.SYNCING -> Triple(Icons.Default.Cloud, MaterialTheme.colorScheme.primary, null)
            SyncStatus.State.OK -> Triple(Icons.Default.CloudDone, Color(0xFF2E7D32), null)
            SyncStatus.State.FAIL -> Triple(Icons.Default.CloudOff, MaterialTheme.colorScheme.error, null)
            else -> Triple(Icons.Default.CloudOff, MaterialTheme.colorScheme.onSurfaceVariant, null)
        }
        Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.height(16.dp))
        if (state == SyncStatus.State.SYNCING) {
            Spacer(Modifier.width(4.dp))
            Text(stringResource(R.string.sync_state_syncing), style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TaskListView(vm: MainViewModel, snackbarHostState: SnackbarHostState) {
    val tasks by vm.tasks.collectAsStateWithLifecycle(emptyList())
    var input by remember { mutableStateOf("") }
    var editingTask by remember { mutableStateOf<app.memodo.data.TaskItem?>(null) }
    val scope = rememberCoroutineScope()
    val ctx = LocalContext.current
    // 已完成分组折叠
    var showCompleted by remember { mutableStateOf(true) }

    val open = tasks.filter { !it.completed }
    val done = tasks.filter { it.completed }

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            OutlinedTextField(
                value = input, onValueChange = { input = it },
                modifier = Modifier.weight(1f), label = { Text(stringResource(R.string.new_task_hint)) },
                singleLine = true,
            )
            Spacer(Modifier.width(8.dp))
            Button(onClick = { if (input.isNotBlank()) { vm.addTask(input); input = "" } }) { Text(stringResource(R.string.add)) }
        }
        Spacer(Modifier.height(12.dp))
        LazyColumn {
            items(open, key = { it.id }) { t ->
                SwipeTaskRow(
                    completed = t.completed,
                    title = t.title,
                    onDelete = { vm.deleteTask(t.id); scope.launch {
                        snackbarHostState.showSnackbar(ctx.getString(R.string.deleted), ctx.getString(R.string.undo), true, SnackbarDuration.Long)
                            .let { if (it == SnackbarResult.ActionPerformed) vm.undoDeleteTask() }
                    } },
                    onToggle = { vm.toggleTask(t) },
                ) {
                    TaskRowContent(t, onToggle = { vm.toggleTask(t) }) { editingTask = t }
                }
            }
            // 已完成折叠分组
            if (done.isNotEmpty()) {
                item(key = "done_header") {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable { showCompleted = !showCompleted }
                            .padding(vertical = 8.dp),
                    ) {
                        Icon(
                            if (showCompleted) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        Spacer(Modifier.width(4.dp))
                        Text(
                            stringResource(R.string.completed_section, done.size),
                            style = MaterialTheme.typography.titleSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }
            if (showCompleted) {
                items(done, key = { it.id }) { t ->
                    SwipeTaskRow(
                        completed = t.completed,
                        title = t.title,
                        onDelete = { vm.deleteTask(t.id); scope.launch {
                            snackbarHostState.showSnackbar(ctx.getString(R.string.deleted), ctx.getString(R.string.undo), true, SnackbarDuration.Long)
                                .let { if (it == SnackbarResult.ActionPerformed) vm.undoDeleteTask() }
                        } },
                        onToggle = { vm.toggleTask(t) },
                    ) {
                        TaskRowContent(t, onToggle = { vm.toggleTask(t) }) { editingTask = t }
                    }
                }
            }
        }
    }

    // 编辑待办对话框（标题 + 到期时间）
    editingTask?.let { task ->
        var editTitle by remember(task) { mutableStateOf(task.title) }
        var editDue by remember(task) { mutableStateOf(task.dueDate) }
        var showDuePicker by remember(task) { mutableStateOf(false) }
        AlertDialog(
            onDismissRequest = { editingTask = null },
            title = { Text(stringResource(R.string.edit_task)) },
            text = {
                Column {
                    OutlinedTextField(value = editTitle, onValueChange = { editTitle = it },
                        label = { Text(stringResource(R.string.memo_title_hint)) }, singleLine = true)
                    Spacer(Modifier.height(8.dp))
                    // 到期时间行
                    OutlinedCard {
                        Row(
                            modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 6.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Icon(Icons.Default.EditCalendar, contentDescription = null,
                                tint = MaterialTheme.colorScheme.primary)
                            Spacer(Modifier.width(8.dp))
                            Text(
                                text = editDue?.let { formatDue(it) } ?: stringResource(R.string.due_date),
                                color = if (editDue == null) MaterialTheme.colorScheme.onSurfaceVariant
                                else MaterialTheme.colorScheme.onSurface,
                                modifier = Modifier.weight(1f),
                            )
                            TextButton(onClick = { showDuePicker = true }) {
                                Text(if (editDue == null) stringResource(R.string.add) else stringResource(R.string.save))
                            }
                            if (editDue != null) {
                                TextButton(onClick = { editDue = null }) { Text(stringResource(R.string.cancel)) }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    if (editTitle.isNotBlank()) {
                        vm.updateTaskFull(task.copy(title = editTitle.trim(), dueDate = editDue))
                    }
                    editingTask = null
                }) { Text(stringResource(R.string.save)) }
            },
            dismissButton = {
                TextButton(onClick = { editingTask = null }) { Text(stringResource(R.string.cancel)) }
            },
        )
        // 到期日期选择器（组合内条件渲染）
        if (showDuePicker) {
            val dueState = rememberDatePickerState(
                initialSelectedDateMillis = editDue ?: System.currentTimeMillis(),
            )
            DatePickerDialog(
                onDismissRequest = { showDuePicker = false },
                confirmButton = {
                    TextButton(onClick = {
                        editDue = dueState.selectedDateMillis
                        showDuePicker = false
                    }) { Text(stringResource(R.string.save)) }
                },
                dismissButton = {
                    TextButton(onClick = { showDuePicker = false }) { Text(stringResource(R.string.cancel)) }
                },
            ) { DatePicker(state = dueState) }
        }
    }
}

/** 待办行内容：checkbox + 标题 + 时间/到期徽标。 */
@Composable
private fun TaskRowContent(t: TaskItem, onToggle: () -> Unit, onClick: () -> Unit) {
    val overdue = t.dueDate != null && t.dueDate < System.currentTimeMillis() && !t.completed
    ListItem(
        headlineContent = {
            Text(t.title, textDecoration = if (t.completed) TextDecoration.LineThrough else null)
        },
        supportingContent = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                if (t.dueDate != null && !t.completed) {
                    Text(
                        text = (if (overdue) stringResource(R.string.due_overdue) + " " else "") + formatDue(t.dueDate),
                        style = MaterialTheme.typography.labelSmall,
                        color = if (overdue) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary,
                    )
                    Spacer(Modifier.width(8.dp))
                }
                Text(formatRelativeTime(t.createdAt, LocalContext.current), style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        },
        leadingContent = {
            Checkbox(checked = t.completed, onCheckedChange = { onToggle() })
        },
        trailingContent = {
            IconButton(onClick = onClick) {
                Icon(Icons.Default.EditNote, contentDescription = stringResource(R.string.edit_task))
            }
        },
        modifier = Modifier.clickable { onClick() },
    )
}

/**
 * 滑动行通用骨架：右滑→onSwipeRight，左滑→弹出确认删除。
 * 用 confirmValueChange 官方拦截模式：动作在确认回调里执行，返回 false 让行自动弹回原位，
 * 无 LaunchedEffect/reset 竞态（旧实现双触发 delete、第二次滑动卡死即源于此）。
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SwipeRowShell(
    confirmDeleteTitle: String,
    onDelete: () -> Unit,
    onSwipeRight: () -> Unit,
    rightBg: Color,
    rightIcon: androidx.compose.ui.graphics.vector.ImageVector,
    content: @Composable () -> Unit,
) {
    var pendingDelete by remember { mutableStateOf<String?>(null) }
    // 防抖：confirmValueChange 在手势 settle 与 snap-back 两条路径可能各回调一次，
    // toggle 类动作会被执行两次（切过去又切回来）。300ms 内视为同一次手势。
    var lastSwipeRightAt by remember { mutableStateOf(0L) }
    val dismissState = rememberSwipeToDismissBoxState(
        initialValue = SwipeToDismissBoxValue.Settled,
        positionalThreshold = { it * 0.45f },
        confirmValueChange = { value ->
            when (value) {
                SwipeToDismissBoxValue.StartToEnd -> {
                    val now = System.currentTimeMillis()
                    if (now - lastSwipeRightAt > 300) {
                        lastSwipeRightAt = now
                        onSwipeRight()
                    }
                    false
                }
                SwipeToDismissBoxValue.EndToStart -> { pendingDelete = confirmDeleteTitle; false } // 弹确认框
                else -> false
            }
        },
    )

    SwipeToDismissBox(
        state = dismissState,
        backgroundContent = {
            val direction = dismissState.dismissDirection
            val (bg, alignment, icon) = when (direction) {
                SwipeToDismissBoxValue.StartToEnd ->
                    Triple(rightBg, Alignment.CenterStart, rightIcon)
                else ->
                    Triple(MaterialTheme.colorScheme.error, Alignment.CenterEnd, Icons.Default.Delete)
            }
            Box(
                Modifier.fillMaxSize().background(bg).padding(horizontal = 20.dp),
                contentAlignment = alignment,
            ) {
                Icon(icon, contentDescription = null, tint = Color.White)
            }
        },
        enableDismissFromStartToEnd = true,
        enableDismissFromEndToStart = true,
    ) {
        content()
    }

    // 删除确认对话框
    pendingDelete?.let { name ->
        AlertDialog(
            onDismissRequest = { pendingDelete = null },
            title = { Text(stringResource(R.string.confirm_delete)) },
            text = { Text(stringResource(R.string.confirm_delete_msg, name)) },
            confirmButton = {
                TextButton(onClick = { pendingDelete = null; onDelete() }) {
                    Text(stringResource(R.string.confirm_delete), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { pendingDelete = null }) { Text(stringResource(R.string.cancel)) }
            },
        )
    }
}

/** 待办滑动行：右滑→切换完成/未完成；左滑→确认删除。 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SwipeTaskRow(
    completed: Boolean,
    title: String,
    onDelete: () -> Unit,
    onToggle: () -> Unit,
    content: @Composable () -> Unit,
) {
    SwipeRowShell(
        confirmDeleteTitle = title,
        onDelete = onDelete,
        onSwipeRight = onToggle,
        rightBg = Color(0xFF2E7D32),
        rightIcon = Icons.Default.Check,
        content = content,
    )
}

/** 备忘滑动行：右滑→切换钉板显隐（眼睛）；左滑→确认删除。 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SwipeMemoRow(
    showOnBoard: Boolean,
    title: String,
    onDelete: () -> Unit,
    onToggleShow: () -> Unit,
    content: @Composable () -> Unit,
) {
    SwipeRowShell(
        confirmDeleteTitle = title,
        onDelete = onDelete,
        onSwipeRight = onToggleShow,
        rightBg = MaterialTheme.colorScheme.primary,
        rightIcon = Icons.Default.Visibility,
        content = content,
    )
}

@Composable
fun MemoListView(vm: MainViewModel, snackbarHostState: SnackbarHostState, sharedDraft: String? = null) {
    val memos by vm.memos.collectAsStateWithLifecycle(emptyList())
    // 系统分享接入：分享文本 → 首行作标题、其余作内容
    // 注意 split(limit=2) 对单行文本返回长度 1 的列表，解构取 [1] 会越界崩溃（真机已复现）
    val draft = remember(sharedDraft) {
        sharedDraft?.split('\n', limit = 2)?.let { parts ->
            (parts[0].trim().take(60)) to parts.getOrElse(1) { "" }.trim()
        }
    }
    var title by remember { mutableStateOf(draft?.first ?: "") }
    var content by remember { mutableStateOf(draft?.second ?: "") }
    var editingMemo by remember { mutableStateOf<app.memodo.data.MemoItem?>(null) }
    val scope = rememberCoroutineScope()
    val ctx = LocalContext.current

    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            OutlinedTextField(
                value = title, onValueChange = { title = it },
                modifier = Modifier.weight(1f), label = { Text(stringResource(R.string.memo_title_hint)) },
                singleLine = true,
            )
            Spacer(Modifier.width(8.dp))
            Button(onClick = { vm.addMemo(title, content); title = ""; content = "" }) { Text(stringResource(R.string.add)) }
        }
        Spacer(Modifier.height(12.dp))
        val sortedMemos = remember(memos) {
            memos.partition { it.showOnBoard }.let { (on, off) ->
                on.sortedByDescending { it.updatedAt } + off.sortedByDescending { it.updatedAt }
            }
        }
        LazyColumn {
            items(sortedMemos, key = { it.id }) { m ->
                SwipeMemoRow(
                    showOnBoard = m.showOnBoard,
                    title = m.title.ifBlank { ctx.getString(R.string.untitled) },
                    onDelete = { vm.deleteMemo(m.id); scope.launch {
                        snackbarHostState.showSnackbar(ctx.getString(R.string.deleted), ctx.getString(R.string.undo), true, SnackbarDuration.Long)
                            .let { if (it == SnackbarResult.ActionPerformed) vm.undoDeleteMemo() }
                    } },
                    onToggleShow = { vm.toggleMemoShow(m) },
                ) {
                    ListItem(
                        leadingContent = {
                            IconButton(onClick = { vm.toggleMemoShow(m) }) {
                                Icon(
                                    if (m.showOnBoard) Icons.Default.Visibility else Icons.Default.VisibilityOff,
                                    contentDescription = if (m.showOnBoard) stringResource(R.string.memo_hide_desc) else stringResource(R.string.memo_show_desc),
                                    tint = if (m.showOnBoard) MaterialTheme.colorScheme.primary
                                    else MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        },
                        headlineContent = {
                            Text(
                                m.title.ifBlank { stringResource(R.string.untitled) },
                                color = if (m.showOnBoard) MaterialTheme.colorScheme.onSurface
                                else MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        },
                        supportingContent = {
                            Column {
                                if (m.content.isNotBlank()) Text(m.content, style = MaterialTheme.typography.bodySmall)
                                Text(formatRelativeTime(m.createdAt, LocalContext.current), style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        },
                        trailingContent = {
                            IconButton(onClick = { editingMemo = m }) {
                                Icon(Icons.Default.EditNote, contentDescription = stringResource(R.string.edit_memo))
                            }
                        },
                        modifier = Modifier.clickable { editingMemo = m },
                    )
                }
            }
        }
    }

    // 编辑备忘对话框
    editingMemo?.let { memo ->
        var editTitle by remember(memo) { mutableStateOf(memo.title) }
        var editContent by remember(memo) { mutableStateOf(memo.content) }
        AlertDialog(
            onDismissRequest = { editingMemo = null },
            title = { Text(stringResource(R.string.edit_memo)) },
            text = {
                Column {
                    OutlinedTextField(value = editTitle, onValueChange = { editTitle = it },
                        label = { Text(stringResource(R.string.memo_title_hint)) }, singleLine = true,
                        modifier = Modifier.fillMaxWidth())
                    Spacer(Modifier.height(8.dp))
                    OutlinedTextField(value = editContent, onValueChange = { editContent = it },
                        label = { Text(stringResource(R.string.memo_content_hint)) },
                        modifier = Modifier.fillMaxWidth(), minLines = 3)
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    vm.updateMemo(memo.id, editTitle.trim(), editContent.trim())
                    editingMemo = null
                }) { Text(stringResource(R.string.save)) }
            },
            dismissButton = {
                TextButton(onClick = { editingMemo = null }) { Text(stringResource(R.string.cancel)) }
            },
        )
    }
}

@Composable
@OptIn(ExperimentalMaterial3Api::class)
fun SettingsView() {
    val ctx = LocalContext.current
    var syncMode by remember { mutableStateOf(WebDavSync.mode(ctx)) } // local | webdav | server
    var syncUrl by remember { mutableStateOf(WebDavSync.url(ctx)) }
    var syncUser by remember { mutableStateOf(WebDavSync.user(ctx)) }
    var syncPass by remember { mutableStateOf(WebDavSync.pass(ctx)) }
    var serverUrl by remember { mutableStateOf(ServerSync.url(ctx)) }
    var serverUser by remember { mutableStateOf(ServerSync.user(ctx)) }
    var serverPass by remember { mutableStateOf(ServerSync.pass(ctx)) }
    var syncing by remember { mutableStateOf(false) }
    var syncMsg by remember { mutableStateOf(ctx.getString(R.string.sync_not_synced)) }
    val scope = rememberCoroutineScope()

    fun refreshWidget() = scope.launch { WidgetRefresher.refreshAll(ctx) }

    fun runSync() {
        syncing = true
        scope.launch {
            val r = when (syncMode) {
                "webdav" -> WebDavSync.run(ctx)
                "server" -> ServerSync.run(ctx)
                else -> WebDavSync.Result(false, ctx.getString(R.string.sync_local_hint))
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
        Text(stringResource(R.string.settings_title), style = MaterialTheme.typography.titleLarge)

        // 同步方式（用户裁定补全：仅本地 / WebDAV / 自建服务器）
        OutlinedCard {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(stringResource(R.string.sync_section), style = MaterialTheme.typography.titleMedium)
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                    listOf(
                        "local" to stringResource(R.string.sync_local),
                        "webdav" to "WebDAV",
                        "server" to stringResource(R.string.sync_server),
                    ).forEach { (tag, label) ->
                        FilterChip(
                            selected = syncMode == tag,
                            onClick = {
                                syncMode = tag
                                WebDavSync.setMode(ctx, tag)
                                SyncScheduler.schedule(ctx)
                            },
                            label = { Text(label) },
                        )
                    }
                }

                when (syncMode) {
                    "webdav" -> {
                        OutlinedTextField(
                            value = syncUrl, onValueChange = { syncUrl = it; WebDavSync.save(ctx, it, syncUser, syncPass) },
                            label = { Text(stringResource(R.string.sync_url_hint)) }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = syncUser, onValueChange = { syncUser = it; WebDavSync.save(ctx, syncUrl, it, syncPass) },
                            label = { Text(stringResource(R.string.sync_account_hint)) }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = syncPass, onValueChange = { syncPass = it; WebDavSync.save(ctx, syncUrl, syncUser, it) },
                            label = { Text(stringResource(R.string.sync_pass_hint)) }, singleLine = true,
                            visualTransformation = PasswordVisualTransformation(),
                            modifier = Modifier.fillMaxWidth(),
                        )
                    }
                    "server" -> {
                        OutlinedTextField(
                            value = serverUrl, onValueChange = { serverUrl = it; ServerSync.save(ctx, it, serverUser, serverPass) },
                            label = { Text(stringResource(R.string.sync_url_hint_server)) }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = serverUser, onValueChange = { serverUser = it; ServerSync.save(ctx, serverUrl, it, serverPass) },
                            label = { Text(stringResource(R.string.sync_email_hint)) }, singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                        OutlinedTextField(
                            value = serverPass, onValueChange = { serverPass = it; ServerSync.save(ctx, serverUrl, serverUser, it) },
                            label = { Text(stringResource(R.string.sync_pass_hint_server)) }, singleLine = true,
                            visualTransformation = PasswordVisualTransformation(),
                            modifier = Modifier.fillMaxWidth(),
                        )
                        var regMsg by remember { mutableStateOf("") }
                        OutlinedButton(
                            onClick = {
                                scope.launch {
                                    val err = ServerSync.register(ctx, serverUrl.trim(), serverUser.trim(), serverPass)
                                    regMsg = err ?: ctx.getString(R.string.register_ok)
                                }
                            },
                            modifier = Modifier.fillMaxWidth(),
                        ) { Text(stringResource(R.string.register)) }
                        if (regMsg.isNotEmpty()) {
                            Text(regMsg, style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    else -> Text(
                        stringResource(R.string.sync_local_hint),
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
                    ) { Text(if (syncing) stringResource(R.string.syncing) else stringResource(R.string.sync_now)) }
                }
                Text(syncMsg, style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)

                // E2EE 口令：两渠道共用一把（与 Windows 端 SyncPassphrase 同语义）
                if (syncMode != "local") {
                    var passOn by remember { mutableStateOf(SyncCrypto.passphrase(ctx).isNotEmpty()) }
                    var e2eeText by remember { mutableStateOf("") }
                    OutlinedTextField(
                        value = e2eeText,
                        onValueChange = { e2eeText = it },
                        label = { Text(stringResource(R.string.sync_passphrase)) },
                        supportingText = { Text(stringResource(R.string.sync_passphrase_hint)) },
                        visualTransformation = PasswordVisualTransformation(),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(onClick = {
                            if (e2eeText.isEmpty()) return@OutlinedButton
                            SyncCrypto.setPassphrase(ctx, e2eeText)
                            e2eeText = ""
                            passOn = true
                        }) { Text(stringResource(R.string.save)) }
                        OutlinedButton(onClick = {
                            SyncCrypto.setPassphrase(ctx, "")
                            e2eeText = ""
                            passOn = false
                        }) { Text(stringResource(R.string.bg_reset)) }
                    }
                    Text(
                        if (passOn) stringResource(R.string.sync_passphrase_on)
                        else stringResource(R.string.sync_passphrase_off),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        // 自动同步间隔
        if (syncMode != "local") {
            OutlinedCard {
                Column(Modifier.padding(16.dp)) {
                    Text(stringResource(R.string.settings_interval), style = MaterialTheme.typography.titleMedium)
                    Spacer(Modifier.height(4.dp))
                    val syncPrefs = ctx.getSharedPreferences("sync_settings", android.content.Context.MODE_PRIVATE)
                    var intervalText by remember { mutableStateOf(
                        syncPrefs.getInt("interval_minutes", 30).toString()) }
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OutlinedTextField(
                            value = intervalText,
                            onValueChange = { intervalText = it.filter { c -> c.isDigit() }.take(3) },
                            label = { Text(stringResource(R.string.settings_minutes)) },
                            singleLine = true,
                            keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(
                                keyboardType = androidx.compose.ui.text.input.KeyboardType.Number),
                            modifier = Modifier.weight(1f),
                        )
                        Spacer(Modifier.width(8.dp))
                        Button(onClick = {
                            val minutes = intervalText.toIntOrNull()?.coerceIn(1, 120) ?: 30
                            intervalText = minutes.toString()
                            syncPrefs.edit()
                                .putInt("interval_minutes", minutes)
                                .putLong("interval_updated_at", System.currentTimeMillis())
                                .apply()
                            SyncScheduler.schedule(ctx)
                        }) { Text(stringResource(R.string.save)) }
                    }
                }
            }
        }

        // 数据备份
        OutlinedCard {
            Column(Modifier.padding(16.dp)) {
                Text(stringResource(R.string.sec_data), style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                var backupMsg by remember { mutableStateOf("") }
                // 导出：SAF 选择保存位置
                val exportLauncher = androidx.activity.compose.rememberLauncherForActivityResult(
                    androidx.activity.result.contract.ActivityResultContracts.CreateDocument("application/json")
                ) { uri ->
                    if (uri != null) {
                        scope.launch {
                            val json = BackupService.exportJson(ctx)
                            try {
                                ctx.contentResolver.openOutputStream(uri)?.use { it.write(json.toByteArray()) }
                                backupMsg = ctx.getString(R.string.backup_export_ok, json.length)
                            } catch (e: Exception) {
                                backupMsg = ctx.getString(R.string.backup_export_fail, e.message ?: "")
                            }
                        }
                    }
                }
                // 导入：SAF 选择文件
                val importLauncher = androidx.activity.compose.rememberLauncherForActivityResult(
                    androidx.activity.result.contract.ActivityResultContracts.OpenDocument()
                ) { uri ->
                    if (uri != null) {
                        scope.launch {
                            try {
                                val text = ctx.contentResolver.openInputStream(uri)?.bufferedReader()?.use { it.readText() } ?: ""
                                val count = BackupService.importJson(ctx, text)
                                backupMsg = ctx.getString(R.string.backup_import_ok, count)
                                WidgetRefresher.refreshAll(ctx)
                            } catch (e: Exception) {
                                backupMsg = ctx.getString(R.string.backup_import_fail, e.message ?: "")
                            }
                        }
                    }
                }
                OutlinedButton(
                    onClick = { exportLauncher.launch("memodo-backup-${System.currentTimeMillis() / 1000}.json") },
                    modifier = Modifier.fillMaxWidth(),
                ) { Text(stringResource(R.string.backup_export)) }
                Spacer(Modifier.height(4.dp))
                OutlinedButton(
                    onClick = {
                        importLauncher.launch(arrayOf("application/json", "text/plain", "application/octet-stream"))
                    },
                    modifier = Modifier.fillMaxWidth(),
                ) { Text(stringResource(R.string.backup_import)) }
                if (backupMsg.isNotEmpty()) {
                    Spacer(Modifier.height(4.dp))
                    Text(backupMsg, style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }

        // 深色模式
        OutlinedCard {
            Column(Modifier.padding(16.dp)) {
                Text(stringResource(R.string.dark_mode), style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                val currentDark = MainActivity.getDarkMode(ctx)
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    listOf(
                        "system" to stringResource(R.string.dark_follow_system),
                        "on" to stringResource(R.string.dark_on),
                        "off" to stringResource(R.string.dark_off),
                    ).forEach { (tag, label) ->
                        FilterChip(
                            selected = currentDark == tag,
                            onClick = { (ctx as? MainActivity)?.setDarkMode(tag) },
                            label = { Text(label) },
                        )
                    }
                }
            }
        }

        // 语言切换
        OutlinedCard {
            Column(Modifier.padding(16.dp)) {
                Text(stringResource(R.string.language_section), style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                val currentLang = MainActivity.getLanguage(ctx)
                val isZh = currentLang.isEmpty() || currentLang == "zh"
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(
                        selected = isZh,
                        onClick = { (ctx as? MainActivity)?.switchLanguage("zh") },
                        label = { Text("中文") },
                    )
                    FilterChip(
                        selected = !isZh,
                        onClick = { (ctx as? MainActivity)?.switchLanguage("en") },
                        label = { Text("English") },
                    )
                }
            }
        }

        // 时间格式
        OutlinedCard {
            Column(Modifier.padding(16.dp)) {
                Text(stringResource(R.string.time_format), style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                val timePrefs = ctx.getSharedPreferences("app_settings", android.content.Context.MODE_PRIVATE)
                var timeFormat by remember { mutableStateOf(timePrefs.getString("time_format", "relative") ?: "relative") }
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(
                        selected = timeFormat == "relative",
                        onClick = {
                            timeFormat = "relative"
                            timePrefs.edit().putString("time_format", "relative").apply()
                        },
                        label = { Text(stringResource(R.string.time_relative)) },
                    )
                    FilterChip(
                        selected = timeFormat == "absolute",
                        onClick = {
                            timeFormat = "absolute"
                            timePrefs.edit().putString("time_format", "absolute").apply()
                        },
                        label = { Text(stringResource(R.string.time_absolute)) },
                    )
                }
            }
        }

        // 外观：钉板背景图
        OutlinedCard {
            Column(Modifier.padding(16.dp)) {
                Text(stringResource(R.string.sec_appearance), style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(8.dp))
                var bgMsg by remember { mutableStateOf("") }
                val bgPrefs = remember { ctx.getSharedPreferences("app_settings", android.content.Context.MODE_PRIVATE) }
                var hasBg by remember { mutableStateOf(!(bgPrefs.getString("board_bg_uri", "") ?: "").isNullOrBlank()) }
                val bgLauncher = androidx.activity.compose.rememberLauncherForActivityResult(
                    androidx.activity.result.contract.ActivityResultContracts.GetContent()
                ) { uri ->
                    if (uri != null) {
                        bgPrefs.edit().putString("board_bg_uri", uri.toString()).apply()
                        try {
                            ctx.contentResolver.takePersistableUriPermission(uri, android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION)
                        } catch (_: Exception) { /* GetContent 可能不支持，失败则忽略 */ }
                        hasBg = true
                        bgMsg = ctx.getString(R.string.bg_choose)
                    }
                }
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(
                        onClick = { bgLauncher.launch("image/*") },
                        modifier = Modifier.weight(1f),
                    ) { Text(stringResource(R.string.bg_choose)) }
                    OutlinedButton(
                        onClick = {
                            bgPrefs.edit().remove("board_bg_uri").apply()
                            hasBg = false
                        },
                        enabled = hasBg,
                        modifier = Modifier.weight(1f),
                    ) { Text(stringResource(R.string.bg_reset)) }
                }
                if (bgMsg.isNotEmpty()) {
                    Spacer(Modifier.height(4.dp))
                    Text(bgMsg, style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}
