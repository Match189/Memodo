package app.memodo.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import app.memodo.data.BoardItem
import app.memodo.data.CardItem
import app.memodo.data.CardLayoutItem
import app.memodo.data.MemoItem
import app.memodo.data.Repo
import app.memodo.data.ServerSync
import app.memodo.data.SyncScheduler
import app.memodo.data.SyncStatus
import app.memodo.data.WebDavSync
import app.memodo.widget.WidgetRefresher
import app.memodo.data.TaskItem
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import kotlinx.coroutines.ExperimentalCoroutinesApi

/**
 * 单一 ViewModel 暴露给所有页面（任务书 §4 ViewModels）。
 * 屏幕状态用 StateFlow，UI 用 collectAsState 订阅。
 */
@OptIn(ExperimentalCoroutinesApi::class)
class MainViewModel(app: Application) : AndroidViewModel(app) {
    private val repo = Repo.get(app)
    private val scope = viewModelScope

    val tasks: StateFlow<List<TaskItem>> = repo.observeTasks()
        .stateIn(scope, SharingStarted.Eagerly, emptyList())
    val memos: StateFlow<List<MemoItem>> = repo.observeMemos()
        .stateIn(scope, SharingStarted.Eagerly, emptyList())

    // 删除快照（内存级，进程死即失效；软删数据仍在库）
    private val _lastDeletedTask = MutableStateFlow<TaskItem?>(null)
    private val _lastDeletedMemo = MutableStateFlow<MemoItem?>(null)

    private val _board = MutableStateFlow<BoardItem?>(null)
    val board: StateFlow<BoardItem?> = _board

    val cards: StateFlow<List<CardItem>> = _board
        .flatMapLatest { board -> board?.let { repo.observeCards(it.id) } ?: flowOf(emptyList()) }
        .stateIn(scope, SharingStarted.Eagerly, emptyList())

    init {
        WidgetRefresher.startWatching(app)
        ensureBoard()
        // 一次性恢复历史归档数据（归档功能已移除；只跑一次，避免与跨端同步互相覆盖）
        scope.launch {
            val ctx: android.app.Application = getApplication()
            val flag = ctx.getSharedPreferences("app_settings", android.content.Context.MODE_PRIVATE)
            if (!flag.getBoolean("unarchive_done", false)) {
                repo.unarchiveAll()
                flag.edit().putBoolean("unarchive_done", true).apply()
            }
        }
        // 启动时自动同步 + 注册定时同步（AlarmManager）
        scope.launch {
            syncNow()
            SyncScheduler.schedule(getApplication())
        }
    }

    /** 立即按当前模式同步一次（主界面同步指示器点击也走这里）。 */
    fun syncNow() = scope.launch {
        val ctx: android.app.Application = getApplication()
        val mode = WebDavSync.mode(ctx)
        when {
            mode == "webdav" && WebDavSync.url(ctx).isNotBlank() && WebDavSync.user(ctx).isNotBlank() ->
                WebDavSync.run(ctx)
            mode == "server" && ServerSync.url(ctx).isNotBlank() ->
                ServerSync.run(ctx)
            else -> SyncStatus.markIdle()
        }
    }

    private fun ensureBoard() = scope.launch {
        _board.value = repo.ensureDefaultBoard()
    }

    fun observeBoardCards() = _board.value?.let { board ->
        repo.observeCards(board.id)
    } ?: flowOf(emptyList())

    /** 所有变更后调用：刷新三套桌面卡片（解决卡片不同步/勾选不同步问题） */
    private fun refreshWidgets() = scope.launch { WidgetRefresher.refreshAll(getApplication()) }

    fun addTask(title: String) = scope.launch {
        if (title.isBlank()) return@launch
        repo.addTask(title.trim()); refreshWidgets()
    }
    fun toggleTask(t: TaskItem) = scope.launch { repo.toggleTask(t); refreshWidgets() }
    fun deleteTask(id: String) = scope.launch {
        val snapshot = repo.getTask(id) ?: return@launch
        repo.deleteTask(id)
        _lastDeletedTask.value = snapshot
        refreshWidgets()
    }
    fun undoDeleteTask() = scope.launch {
        _lastDeletedTask.value?.let { repo.restoreTask(it) }
        _lastDeletedTask.value = null
        refreshWidgets()
    }
    fun updateTask(id: String, title: String) = scope.launch { repo.updateTask(id, title); refreshWidgets() }
    /** 编辑对话框保存：标题 + 到期时间（null=清除）。 */
    fun updateTaskFull(item: TaskItem) = scope.launch { repo.updateTaskFull(item); refreshWidgets() }
    fun addMemo(title: String, content: String) = scope.launch {
        if (title.isBlank() && content.isBlank()) return@launch
        repo.addMemo(title.trim(), content.trim()); refreshWidgets()
    }
    fun deleteMemo(id: String) = scope.launch {
        val snapshot = repo.getMemo(id) ?: return@launch
        repo.deleteMemo(id)
        _lastDeletedMemo.value = snapshot
        refreshWidgets()
    }
    fun undoDeleteMemo() = scope.launch {
        _lastDeletedMemo.value?.let { repo.restoreMemo(it) }
        _lastDeletedMemo.value = null
        refreshWidgets()
    }
    fun updateMemo(id: String, title: String, content: String) = scope.launch { repo.updateMemo(id, title, content); refreshWidgets() }
    fun toggleMemoDone(m: MemoItem) = scope.launch { repo.setMemoDone(m, !m.completed); refreshWidgets() }
    fun toggleMemoShow(m: MemoItem) = scope.launch { repo.setMemoShow(m, !m.showOnBoard); refreshWidgets() }

    fun pinTodo(uuid: String) = scope.launch { repo.pin("todo", uuid) }
    fun pinMemo(uuid: String) = scope.launch { repo.pin("memo", uuid) }
    fun unpin(cardId: String) = scope.launch { repo.unpin(cardId) }
    fun moveCard(card: CardItem, delta: Int) = scope.launch { repo.moveCard(card, delta) }

    suspend fun getLayout(cardId: String): CardLayoutItem? = repo.getLayout(cardId)
    suspend fun saveLayout(item: CardLayoutItem) = repo.saveLayout(item)
}
