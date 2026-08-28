import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:path/path.dart' as p;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:todolist/data/app_database.dart';

/// SPD Phase 1：数据库迁移回归测试。
/// 从手工构造的 v1 旧库出发，经 AppDatabase.open 走完 v1→v2→v3 全链路。
void main() {
  sqfliteFfiInit();
  databaseFactory = databaseFactoryFfi;

  late Directory tempDir;
  late String dbPath;

  setUp(() async {
    tempDir = await Directory.systemTemp.createTemp('todolist_mig');
    dbPath = p.join(tempDir.path, 'old.db');
  });

  tearDown(() async {
    await tempDir.delete(recursive: true);
  });

  Future<void> createLegacyV1Db() async {
    final db = await databaseFactoryFfi.openDatabase(
      dbPath,
      options: OpenDatabaseOptions(
        version: 1,
        onCreate: (db, version) async {
        await db.execute('''
          CREATE TABLE tasks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            done INTEGER NOT NULL DEFAULT 0,
            created_at INTEGER NOT NULL,
            updated_at INTEGER NOT NULL
          )
        ''');
        await db.execute('''
          CREATE TABLE memos (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            content TEXT NOT NULL DEFAULT '',
            created_at INTEGER NOT NULL,
            updated_at INTEGER NOT NULL
          )
        ''');
        final now = DateTime.now().millisecondsSinceEpoch;
        await db.insert('tasks', {
          'title': 'v1老任务',
          'done': 0,
          'created_at': now,
          'updated_at': now,
        });
        await db.insert('memos', {
          'title': 'v1老备忘',
          'content': '内容',
          'created_at': now,
          'updated_at': now,
        });
      },
      ),
    );
    await db.close();
  }

  test('v1 → v3：旧数据保留、uuid 回填、新列可写', () async {
    await createLegacyV1Db();

    final appDb = await AppDatabase.open(path: dbPath);
    final db = appDb.database;

    expect(await db.getVersion(), 3);

    final tasks = await db.query('tasks');
    expect(tasks, hasLength(1));
    final task = tasks.single;
    expect(task['title'], 'v1老任务');
    // v2 回填
    expect(task['uuid'], isNotNull);
    // v3 新列存在且有默认值
    expect(task['description'], '');
    expect(task['priority'], 0);
    expect(task['due_at'], isNull);
    expect(task['deleted_at'], isNull);

    final memos = await db.query('memos');
    expect(memos.single['title'], 'v1老备忘');
    expect(memos.single['uuid'], isNotNull);

    // 新列可写（v3 upsert 路径）
    await db.update('tasks', {'priority': 2, 'due_at': 1234567890},
        where: "title = 'v1老任务'");
    expect((await db.query('tasks')).single['priority'], 2);

    await appDb.close();
  });

  test('v2 库（有布尔墓碑）→ v3：墓碑时间戳回填', () async {
    await createLegacyV1Db();
    // 手工把库演进到 v2 形态并塞一条"已删除"
    final db = await databaseFactoryFfi.openDatabase(dbPath);
    // 演进到与真实 v2 完全一致的形态（两张表都有 uuid/deleted）
    await db.execute('ALTER TABLE tasks ADD COLUMN uuid TEXT');
    await db
        .execute('ALTER TABLE tasks ADD COLUMN deleted INTEGER NOT NULL DEFAULT 0');
    await db.execute('ALTER TABLE memos ADD COLUMN uuid TEXT');
    await db
        .execute('ALTER TABLE memos ADD COLUMN deleted INTEGER NOT NULL DEFAULT 0');
    await db.execute("UPDATE tasks SET uuid = 'legacy-uuid', deleted = 1");
    await db.execute('PRAGMA user_version = 2');
    await db.close();

    final appDb = await AppDatabase.open(path: dbPath);
    final task = (await appDb.database.query('tasks')).single;
    expect(task['deleted'], 1);
    // v3 回填：deleted=1 的行应有 deleted_at
    expect(task['deleted_at'], isNotNull);
    await appDb.close();
  });
}
