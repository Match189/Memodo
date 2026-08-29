package app.memodo.data

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

/**
 * 与 Windows 端 SQLite DDL 逐列对齐（任务书「共享数据协议」）。
 * Kotlin 字段名用驼峰，列名用 @ColumnInfo 映射到 snake_case。
 */

@Entity(tableName = "tasks", indices = [Index("completed"), Index("updated_at")])
data class TaskItem(
    @PrimaryKey val id: String,
    val title: String,
    val description: String = "",
    val completed: Boolean = false,
    val priority: Int = 0,
    @ColumnInfo(name = "due_date") val dueDate: Long? = null,
    @ColumnInfo(name = "created_at") val createdAt: Long,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
    @ColumnInfo(name = "deleted_at") val deletedAt: Long? = null,
)

@Entity(tableName = "memos", indices = [Index("updated_at")])
data class MemoItem(
    @PrimaryKey val id: String,
    val title: String,
    val content: String = "",
    @ColumnInfo(name = "created_at") val createdAt: Long,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
    @ColumnInfo(name = "deleted_at") val deletedAt: Long? = null,
)

@Entity(tableName = "boards", indices = [Index("updated_at")])
data class BoardItem(
    @PrimaryKey val id: String,
    val name: String = "我的图钉板",
    @ColumnInfo(name = "created_at") val createdAt: Long,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
    @ColumnInfo(name = "deleted_at") val deletedAt: Long? = null,
)

@Entity(
    tableName = "sections",
    indices = [Index("board_id")]
)
data class SectionItem(
    @PrimaryKey val id: String,
    @ColumnInfo(name = "board_id") val boardId: String,
    val name: String = "",
    val sort: Int = 0,
    @ColumnInfo(name = "created_at") val createdAt: Long,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
    @ColumnInfo(name = "deleted_at") val deletedAt: Long? = null,
)

@Entity(
    tableName = "cards",
    indices = [Index("board_id"), Index(value = ["ref_type", "ref_uuid"])]
)
data class CardItem(
    @PrimaryKey val id: String,
    @ColumnInfo(name = "board_id") val boardId: String,
    @ColumnInfo(name = "section_id") val sectionId: String = "",
    @ColumnInfo(name = "ref_type") val refType: String,
    @ColumnInfo(name = "ref_uuid") val refUuid: String,
    val sort: Int = 0,
    @ColumnInfo(name = "created_at") val createdAt: Long,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
    @ColumnInfo(name = "deleted_at") val deletedAt: Long? = null,
)

@Entity(
    tableName = "card_layouts",
    indices = [Index(value = ["card_id", "platform"], unique = true)]
)
data class CardLayoutItem(
    @PrimaryKey(autoGenerate = true) val id: Int = 0,
    @ColumnInfo(name = "card_id") val cardId: String,
    val platform: String = "windows",
    val x: Double = 0.0,
    val y: Double = 0.0,
    val width: Double = 190.0,
    val height: Double = 150.0,
    val rotation: Double = 0.0,
    val z: Int = 0,
    @ColumnInfo(name = "order") val order: Int? = null,
    @ColumnInfo(name = "size_class") val sizeClass: String? = null,
    @ColumnInfo(name = "updated_at") val updatedAt: Long,
)
