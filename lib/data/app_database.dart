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

  static const _schemaTasksV3 = '''
    CREATE TABLE tasks (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      uuid TEXT,
      title TEXT NOT NULL,
      description TEXT NOT NULL DEFAULT '',
      done INTEGER NOT NULL DEFAULT 0,
      priority INTEGER NOT NULL DEFAULT 0,
      due_at INTEGER,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      deleted INTEGER NOT NULL DEFAULT 0,
      deleted_at INTEGER,
      device_id TEXT
    )
  ''';

  static const _schemaMemosV3 = '''
    CREATE TABLE memos (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      uuid TEXT,
      title TEXT NOT NULL,
      content TEXT NOT NULL DEFAULT '',
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      deleted INTEGER NOT NULL DEFAULT 0,
      deleted_at INTEGER,
      device_id TEXT
    )
  ''';

  static const _schemaSettings = '''
    CREATE TABLE settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL
    )
  ''';

  // v4：图钉板（Board / Card）。Card 只引用 todo/memo 的 uuid，不复制内容。
  static const _schemaBoards = '''
    CREATE TABLE boards (
      uuid TEXT PRIMARY KEY,
      name TEXT NOT NULL,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      deleted_at INTEGER,
      device_id TEXT,
      deleted INTEGER NOT NULL DEFAULT 0
    )
  ''';

  static const _schemaCards = '''
    CREATE TABLE cards (
      uuid TEXT PRIMARY KEY,
      board_uuid TEXT NOT NULL,
      ref_type TEXT NOT NULL,
      ref_uuid TEXT NOT NULL,
      created_at INTEGER NOT NULL,
      updated_at INTEGER NOT NULL,
      deleted_at INTEGER,
      device_id TEXT,
      deleted INTEGER NOT NULL DEFAULT 0
    )
  ''';

  /// [path] 传 ':memory:' 可在测试中用内存库。
  static Future<AppDatabase> open({String? path}) async {
    final file = path ??
        p.join((await getApplicationSupportDirectory()).path, 'todolist.db');
    final db = await openDatabase(
      file,
      version: 4,
      onConfigure: (db) async {
        // 主窗口与桌面小组件子窗口是两个引擎并发访问同一个库文件。
        // ⚠️ 安卓平台版 sqflite 不允许 execute 执行 PRAGMA，必须 rawQuery。
        await db.rawQuery('PRAGMA busy_timeout = 3000');
      },
      onCreate: (db, version) async {
        await db.execute(_schemaTasksV3);
        await db.execute(_schemaMemosV3);
        await db.execute(_schemaSettings);
        await db.execute(_schemaBoards);
        await db.execute(_schemaCards);
        await _createUuidIndexes(db);
      },
      onUpgrade: (db, oldVersion, newVersion) async {
        if (oldVersion < 2) {
          // v1 -> v2：软删除与全局标识。
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
        if (oldVersion < 3) {
          // v2 -> v3：SPD §17 完整字段（description/priority/dueAt/deviceId/deletedAt）。
          await db
              .execute("ALTER TABLE tasks ADD COLUMN description TEXT NOT NULL DEFAULT ''");
          await db
              .execute('ALTER TABLE tasks ADD COLUMN priority INTEGER NOT NULL DEFAULT 0');
          await db.execute('ALTER TABLE tasks ADD COLUMN due_at INTEGER');
          await db.execute('ALTER TABLE tasks ADD COLUMN deleted_at INTEGER');
          await db.execute('ALTER TABLE tasks ADD COLUMN device_id TEXT');
          await db.execute('ALTER TABLE memos ADD COLUMN deleted_at INTEGER');
          await db.execute('ALTER TABLE memos ADD COLUMN device_id TEXT');
          await _backfillDeletedAt(db, 'tasks');
          await _backfillDeletedAt(db, 'memos');
        }
        if (oldVersion < 4) {
          // v3 -> v4：图钉板（Board / Card）。
          await db.execute(_schemaBoards);
          await db.execute(_schemaCards);
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

  /// v2 的布尔墓碑补写成 v3 的时间戳墓碑。
  static Future<void> _backfillDeletedAt(Database db, String table) async {
    await db.execute(
        'UPDATE $table SET deleted_at = updated_at WHERE deleted = 1 AND deleted_at IS NULL');
  }

  Future<void> close() => _db.close();
}
