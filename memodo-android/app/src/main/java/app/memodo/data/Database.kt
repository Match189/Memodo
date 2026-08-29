package app.memodo.data

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

@Database(
    entities = [
        TaskItem::class,
        MemoItem::class,
        BoardItem::class,
        SectionItem::class,
        CardItem::class,
        CardLayoutItem::class,
    ],
    version = 1,
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
        fun get(ctx: Context): AppDatabase = instance ?: synchronized(this) {
            instance ?: Room.databaseBuilder(ctx.applicationContext, AppDatabase::class.java, "memodo.db")
                .fallbackToDestructiveMigration()
                .build()
                .also { instance = it }
        }
    }
}
