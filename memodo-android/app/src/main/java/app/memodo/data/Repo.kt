package app.memodo.data

import android.content.Context
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import java.util.UUID

class Repo private constructor(ctx: Context) {

    private val db = AppDatabase.get(ctx)
    private val tasks get() = db.taskDao()
    private val memos get() = db.memoDao()
    private val boards get() = db.boardDao()
    private val cards get() = db.cardDao()
    private val layouts get() = db.cardLayoutDao()

    fun observeTasks(): Flow<List<TaskItem>> = tasks.observeActive()
    fun observeMemos(): Flow<List<MemoItem>> = memos.observeActive()
    fun observeCards(boardId: String): Flow<List<CardItem>> = cards.observeByBoard(boardId)

    suspend fun addTask(title: String): TaskItem {
        val now = System.currentTimeMillis()
        val item = TaskItem(
            id = UUID.randomUUID().toString(),
            title = title,
            createdAt = now, updatedAt = now,
        )
        tasks.upsert(item)
        return item
    }

    suspend fun toggleTask(item: TaskItem) {
        tasks.setDone(item.id, !item.completed, System.currentTimeMillis())
    }

    /// Widget 快速完成（§24）：直写本地库，updated_at 推进使 LWW 自然传播
    suspend fun setTaskDone(id: String, done: Boolean) {
        tasks.setDone(id, done, System.currentTimeMillis())
    }

    suspend fun getTask(id: String): TaskItem? = tasks.getById(id)

    /// 完成的备忘从钉板移除（与待办同语义）
    suspend fun setMemoDone(m: MemoItem, done: Boolean) {
        memos.update(m.copy(completed = done, updatedAt = System.currentTimeMillis()))
    }

    suspend fun deleteTask(id: String) {
        val now = System.currentTimeMillis()
        val cur = tasks.getById(id) ?: return
        tasks.upsert(cur.copy(deletedAt = now, updatedAt = now))
    }

    suspend fun addMemo(title: String, content: String): MemoItem {
        val now = System.currentTimeMillis()
        val item = MemoItem(
            id = UUID.randomUUID().toString(),
            title = title, content = content,
            createdAt = now, updatedAt = now,
        )
        memos.upsert(item)
        return item
    }

    suspend fun deleteMemo(id: String) {
        val now = System.currentTimeMillis()
        val cur = memos.getById(id) ?: return
        memos.upsert(cur.copy(deletedAt = now, updatedAt = now))
    }

    suspend fun ensureDefaultBoard(): BoardItem {
        boards.firstActive()?.let { return it }
        val now = System.currentTimeMillis()
        val b = BoardItem(id = UUID.randomUUID().toString(), createdAt = now, updatedAt = now)
        boards.insert(b)
        return b
    }

    suspend fun pin(refType: String, refUuid: String): CardItem? {
        val board = ensureDefaultBoard()
        val existing = cards.findPined(refType, refUuid)
        if (existing != null) return null
        val now = System.currentTimeMillis()
        val card = CardItem(
            id = UUID.randomUUID().toString(),
            boardId = board.id,
            refType = refType, refUuid = refUuid,
            createdAt = now, updatedAt = now,
        )
        cards.insert(card)
        return card
    }

    suspend fun unpin(cardId: String) {
        cards.unpin(cardId, System.currentTimeMillis())
    }

    /// Android 网格排序（§23 Adaptive Grid）：调整 cards.sort（业务顺序，随同步传播）
    suspend fun moveCard(card: CardItem, delta: Int) {
        val list = db.cardDao().observeByBoard(card.boardId).first()
        val idx = list.indexOfFirst { it.id == card.id }
        val swap = idx + delta
        if (idx < 0 || swap < 0 || swap >= list.size) return
        val now = System.currentTimeMillis()
        cards.update(list[idx].copy(sort = list[swap].sort, updatedAt = now))
        cards.update(list[swap].copy(sort = list[idx].sort, updatedAt = now))
    }

    suspend fun getLayout(cardId: String): CardLayoutItem? =
        layouts.get(cardId, "android")

    suspend fun saveLayout(item: CardLayoutItem) {
        val now = System.currentTimeMillis()
        val cur = layouts.get(item.cardId, item.platform)
        val toSave = item.copy(updatedAt = now, id = cur?.id ?: 0)
        if (cur != null) layouts.update(toSave) else layouts.insert(toSave)
    }

    companion object {
        @Volatile private var instance: Repo? = null
        fun get(ctx: Context): Repo = instance ?: synchronized(this) {
            instance ?: Repo(ctx.applicationContext).also { instance = it }
        }
    }
}
