package app.memodo.data

import android.content.Context
import kotlinx.coroutines.flow.Flow
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
