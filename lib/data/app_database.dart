import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite/sqflite.dart';
import 'package:uuid/uuid.dart';

/// 本地 SQLite 数据库的打开、建表与迁移。
///
/// 桌面端（Windows/Linux/macOS）由 main() 先把全局 databaseFactory 切到
/// sqflite_common_ffi，安卓端用平台内置实现；本类不感知差异。
class AppDatabase {
  AppDatabase._(this._db);

  final Database _db;

  Database get database => _db;

  static const _schemaV2Tasks = '''
    CREATE TABLE tasks (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      uuid TEXT,
      title TEXT NOT NULL,
      done INTEGER NOT NULL DEFAULT 0,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      deleted INTEGER NOT NULL DEFAULT 0
    )
  ''';

  static const _schemaV2Memos = '''
    CREATE TABLE memos (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      uuid TEXT,
      title TEXT NOT NULL,
      content TEXT NOT NULL DEFAULT '',
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      deleted INTEGER NOT NULL DEFAULT 0
    )
  ''';

  static const _schemaSettings = '''
    CREATE TABLE settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL
    )
  ''';

  /// [path] 传 ':memory:' 可在测试中用内存库。
  static Future<AppDatabase> open({String? path}) async {
    final file = path ??
        p.join((await getApplicationSupportDirectory()).path, 'todolist.db');
    final db = await openDatabase(
      file,
      version: 2,
      onConfigure: (db) async {
        // 主窗口与桌面小组件子窗口是两个引擎并发访问同一个库文件。
        await db.execute('PRAGMA busy_timeout = 3000');
      },
      onCreate: (db, version) async {
        await db.execute(_schemaV2Tasks);
        await db.execute(_schemaV2Memos);
        await db.execute(_schemaSettings);
        await _createUuidIndexes(db);
      },
      onUpgrade: (db, oldVersion, newVersion) async {
        // v1 -> v2：加 uuid / deleted 列、uuid 唯一索引、设置表，并回填 uuid。
        if (oldVersion < 2) {
          await db.execute('ALTER TABLE tasks ADD COLUMN uuid TEXT');
          await db.execute(
              'ALTER TABLE tasks ADD COLUMN deleted INTEGER NOT NULL DEFAULT 0');
          await db.execute('ALTER TABLE memos ADD COLUMN uuid TEXT');
          await db.execute(
              'ALTER TABLE memos ADD COLUMN deleted INTEGER NOT NULL DEFAULT 0');
          await db.execute(_schemaSettings);
          await _backfillUuids(db, 'tasks');
          await _backfillUuids(db, 'memos');
          await _createUuidIndexes(db);
        }
      },
    );
    return AppDatabase._(db);
  }

  static Future<void> _createUuidIndexes(Database db) async {
    // SQLite 唯一索引把 NULL 视为互不相同，所以先回填再建索引最稳妥。
    await db.execute(
        'CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_uuid ON tasks(uuid)');
    await db.execute(
        'CREATE UNIQUE INDEX IF NOT EXISTS idx_memos_uuid ON memos(uuid)');
  }

  static Future<void> _backfillUuids(Database db, String table) async {
    final rows = await db.query(table,
        columns: ['id'], where: "uuid IS NULL OR uuid = ''");
    for (final row in rows) {
      await db.update(table, {'uuid': const Uuid().v4()},
          where: 'id = ?', whereArgs: [row['id']]);
    }
  }

  Future<void> close() => _db.close();
}
