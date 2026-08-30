package app.memodo.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import app.memodo.data.BoardItem
import app.memodo.data.CardItem
import app.memodo.data.CardLayoutItem
import app.memodo.data.MemoItem
import app.memodo.data.Repo
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

    private val _board = MutableStateFlow<BoardItem?>(null)
    val board: StateFlow<BoardItem?> = _board

    val cards: StateFlow<List<CardItem>> = _board
        .flatMapLatest { board -> board?.let { repo.observeCards(it.id) } ?: flowOf(emptyList()) }
        .stateIn(scope, SharingStarted.Eagerly, emptyList())

    init { ensureBoard() }

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
    fun deleteTask(id: String) = scope.launch { repo.deleteTask(id); refreshWidgets() }
    fun addMemo(title: String, content: String) = scope.launch {
        if (title.isBlank() && content.isBlank()) return@launch
        repo.addMemo(title.trim(), content.trim()); refreshWidgets()
    }
    fun deleteMemo(id: String) = scope.launch { repo.deleteMemo(id); refreshWidgets() }
    fun toggleMemoDone(m: MemoItem) = scope.launch { repo.setMemoDone(m, !m.completed); refreshWidgets() }
    fun toggleMemoShow(m: MemoItem) = scope.launch { repo.setMemoShow(m, !m.showOnBoard); refreshWidgets() }

    fun pinTodo(uuid: String) = scope.launch { repo.pin("todo", uuid) }
    fun pinMemo(uuid: String) = scope.launch { repo.pin("memo", uuid) }
    fun unpin(cardId: String) = scope.launch { repo.unpin(cardId) }
    fun moveCard(card: CardItem, delta: Int) = scope.launch { repo.moveCard(card, delta) }

    suspend fun getLayout(cardId: String): CardLayoutItem? = repo.getLayout(cardId)
    suspend fun saveLayout(item: CardLayoutItem) = repo.saveLayout(item)
}
