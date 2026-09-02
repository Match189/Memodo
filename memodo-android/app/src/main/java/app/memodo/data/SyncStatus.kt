package app.memodo.data

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

/** 同步状态（主界面指示器）：进程内单例，同步引擎成功/失败/进行中时更新。 */
object SyncStatus {
    enum class State { IDLE, SYNCING, OK, FAIL }

    private val _state = MutableStateFlow(State.IDLE)
    val state: StateFlow<State> = _state

    @Volatile var lastMessage: String = ""
        private set

    @Volatile var lastSyncAt: Long = 0
        private set

    fun markSyncing() {
        _state.value = State.SYNCING
    }

    fun markOk(message: String) {
        lastMessage = message
        lastSyncAt = System.currentTimeMillis()
        _state.value = State.OK
    }

    fun markFail(message: String) {
        lastMessage = message
        _state.value = State.FAIL
    }

    fun markIdle() {
        _state.value = State.IDLE
    }
}
