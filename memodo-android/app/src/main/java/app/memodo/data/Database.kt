package app.memodo.data

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import java.io.File

@Database(
    entities = [
        TaskItem::class,
        MemoItem::class,
        BoardItem::class,
        SectionItem::class,
        CardItem::class,
        CardLayoutItem::class,
    ],
    version = 6,
    exportSchema = false,
)
abstract class AppDatabase : RoomDatabase() {
    abstract fun taskDao(): TaskDao
    abstract fun memoDao(): MemoDao
    abstract fun boardDao(): BoardDao
    abstract fun sectionDao(): SectionDao
    abstract fun cardDao(): CardDao
    abstract fun cardLayoutDao(): CardLayoutDao

    companion object {
        @Volatile private var instance: AppDatabase? = null

        /** v5→v6：tasks/memos 表加 archived_at 列（归档功能）。
         *  每条 ALTER 用 try-catch 包裹，防止列已存在时崩溃导致 Room 清库。 */
        private val MIGRATION_5_6 = object : Migration(5, 6) {
            override fun migrate(db: SupportSQLiteDatabase) {
                try { db.execSQL("ALTER TABLE tasks ADD COLUMN archived_at INTEGER") } catch (_: Exception) {}
                try { db.execSQL("ALTER TABLE memos ADD COLUMN archived_at INTEGER") } catch (_: Exception) {}
            }
        }

        fun get(ctx: Context): AppDatabase = instance ?: synchronized(this) {
            val ctxApp = ctx.applicationContext
            instance = try {
                build(ctxApp)
            } catch (e: IllegalStateException) {
                // 历史版本(<=4)无迁移链：删除重建（一次性代价，换取可用性）
                val dbFile = ctxApp.getDatabasePath("memodo.db")
                if (dbFile.exists()) {
                    dbFile.delete()
                    File(dbFile.path + "-wal").delete()
                    File(dbFile.path + "-shm").delete()
                }
                build(ctxApp)
            }
            instance!!
        }

        private fun build(ctx: Context): AppDatabase =
            Room.databaseBuilder(ctx, AppDatabase::class.java, "memodo.db")
                .addMigrations(MIGRATION_5_6)
                .fallbackToDestructiveMigrationOnDowngrade()
                .build()
    }
}
