package app.memodo.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import kotlinx.coroutines.flow.Flow

@Dao
interface TaskDao {
    @Query("SELECT * FROM tasks WHERE deleted_at IS NULL AND archived_at IS NULL ORDER BY completed ASC, updated_at DESC")
    fun observeActive(): Flow<List<TaskItem>>

    @Query("SELECT * FROM tasks WHERE deleted_at IS NULL AND archived_at IS NOT NULL ORDER BY archived_at DESC")
    fun observeArchived(): Flow<List<TaskItem>>

    @Query("SELECT * FROM tasks")
    suspend fun listAll(): List<TaskItem>

    @Query("SELECT * FROM tasks WHERE id = :id")
    suspend fun getById(id: String): TaskItem?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(item: TaskItem)

    @Update
    suspend fun update(item: TaskItem)

    @Query("UPDATE tasks SET completed = :done, updated_at = :now, deleted_at = NULL WHERE id = :id")
    suspend fun setDone(id: String, done: Boolean, now: Long)

    @Query("UPDATE tasks SET archived_at = :now, updated_at = :now WHERE id = :id")
    suspend fun archive(id: String, now: Long)

    @Query("UPDATE tasks SET archived_at = NULL, updated_at = :now WHERE id = :id")
    suspend fun unarchive(id: String, now: Long)

    @Query("UPDATE tasks SET archived_at = NULL, updated_at = :now WHERE archived_at IS NOT NULL")
    suspend fun unarchiveAll(now: Long)
}

@Dao
interface MemoDao {
    @Query("SELECT * FROM memos WHERE deleted_at IS NULL AND archived_at IS NULL ORDER BY updated_at DESC")
    fun observeActive(): Flow<List<MemoItem>>

    @Query("SELECT * FROM memos WHERE deleted_at IS NULL AND archived_at IS NOT NULL ORDER BY archived_at DESC")
    fun observeArchived(): Flow<List<MemoItem>>

    @Query("SELECT * FROM memos WHERE id = :id")
    suspend fun getById(id: String): MemoItem?

    @Query("SELECT * FROM memos")
    suspend fun listAll(): List<MemoItem>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(item: MemoItem)

    @Update
    suspend fun update(item: MemoItem)

    @Query("UPDATE memos SET archived_at = :now, updated_at = :now WHERE id = :id")
    suspend fun archive(id: String, now: Long)

    @Query("UPDATE memos SET archived_at = NULL, updated_at = :now WHERE id = :id")
    suspend fun unarchive(id: String, now: Long)

    @Query("UPDATE memos SET archived_at = NULL, updated_at = :now WHERE archived_at IS NOT NULL")
    suspend fun unarchiveAll(now: Long)
}

@Dao
interface BoardDao {
    @Query("SELECT * FROM boards WHERE deleted_at IS NULL LIMIT 1")
    suspend fun firstActive(): BoardItem?

    @Query("SELECT * FROM boards WHERE deleted_at IS NULL ORDER BY created_at ASC")
    fun observeAll(): Flow<List<BoardItem>>

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insert(item: BoardItem)
}

@Dao
interface SectionDao {
    @Query("SELECT * FROM sections WHERE deleted_at IS NULL AND board_id = :board ORDER BY sort, created_at")
    fun observeByBoard(board: String): Flow<List<SectionItem>>

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insert(item: SectionItem)
}

@Dao
interface CardDao {
    @Query("SELECT * FROM cards WHERE board_id = :board AND deleted_at IS NULL ORDER BY sort, created_at")
    fun observeByBoard(board: String): Flow<List<CardItem>>

    @Query("SELECT * FROM cards WHERE ref_type = :type AND ref_uuid = :uuid AND deleted_at IS NULL LIMIT 1")
    suspend fun findPined(type: String, uuid: String): CardItem?

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insert(item: CardItem)

    @Update
    suspend fun update(item: CardItem)

    @Query("UPDATE cards SET deleted_at = :now, updated_at = :now WHERE id = :id")
    suspend fun unpin(id: String, now: Long)
}

@Dao
interface CardLayoutDao {
    @Query("SELECT * FROM card_layouts WHERE card_id = :card AND platform = :platform")
    suspend fun get(card: String, platform: String): CardLayoutItem?

    @Query("SELECT * FROM card_layouts WHERE card_id = :card AND platform = :platform")
    fun observe(card: String, platform: String): Flow<CardLayoutItem?>

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insert(item: CardLayoutItem)

    @Update
    suspend fun update(item: CardLayoutItem)
}
