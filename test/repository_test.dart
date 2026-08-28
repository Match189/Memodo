import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite/sqflite.dart' as sqflite;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:todolist/data/app_database.dart';
import 'package:todolist/data/memo_repository.dart';
import 'package:todolist/data/settings_store.dart';
import 'package:todolist/data/task_repository.dart';
import 'package:todolist/models/memo.dart';
import 'package:todolist/models/task.dart';

void main() {
  sqfliteFfiInit();
  sqflite.databaseFactory = databaseFactoryFfi;

  late AppDatabase appDb;

  setUp(() async {
    appDb = await AppDatabase.open(path: inMemoryDatabasePath);
  });

  tearDown(() async {
    await appDb.close();
  });

  Task newTask(String title, {DateTime? updatedAt}) {
    final now = updatedAt ?? DateTime.now();
    return Task(title: title, done: false, createdAt: now, updatedAt: now);
  }

  Memo newMemo(String title) {
    final now = DateTime.now();
    return Memo(
        title: title, content: '内容-$title', createdAt: now, updatedAt: now);
  }

  test('task：新增自动生成 uuid，勾选、改标题正常', () async {
    final repo = TaskRepository(appDb.database);

    final inserted = await repo.insert(newTask('买牛奶'));
    expect(inserted.uuid, isNotNull);

    final tasks = await repo.listAll();
    expect(tasks, hasLength(1));
    expect(tasks.first.uuid, inserted.uuid);
    expect(tasks.first.title, '买牛奶');

    await repo.update(tasks.first.copyWith(done: true));
    expect((await repo.listAll()).first.done, isTrue);

    await repo.update((await repo.listAll()).first.copyWith(title: '买两盒牛奶'));
    expect((await repo.listAll()).first.title, '买两盒牛奶');
  });

  test('task：删除是软删除，listAll 不含墓碑，listForSync 含墓碑', () async {
    final repo = TaskRepository(appDb.database);
    final a = await repo.insert(newTask('a'));
    await repo.insert(newTask('b'));

    await repo.delete(a);

    expect((await repo.listAll()).map((t) => t.title), ['b']);
    final forSync = await repo.listForSync();
    expect(forSync, hasLength(2));
    expect(forSync.firstWhere((t) => t.uuid == a.uuid).deleted, isTrue);
  });

  test('task：deleteDone 软删除已完成，未完成保留', () async {
    final repo = TaskRepository(appDb.database);
    await repo.insert(newTask('a'));
    final b = await repo.insert(newTask('b'));
    await repo.update(b.copyWith(done: true));

    await repo.deleteDone();

    final rest = await repo.listAll();
    expect(rest.map((t) => t.title), ['a']);
    expect(await repo.listForSync(), hasLength(2));
  });

  test('task：upsertByUuid 同 uuid 覆盖、新 uuid 插入', () async {
    final repo = TaskRepository(appDb.database);
    final a = await repo.insert(newTask('原始'));

    final now = DateTime.now();
    await repo.upsertByUuid(Task(
      uuid: a.uuid,
      title: '同步覆盖的标题',
      done: true,
      createdAt: a.createdAt,
      updatedAt: now,
    ));
    var all = await repo.listAll();
    expect(all, hasLength(1));
    expect(all.first.title, '同步覆盖的标题');
    expect(all.first.done, isTrue);

    await repo.upsertByUuid(Task(
      uuid: 'other-uuid',
      title: '远端来的新任务',
      done: false,
      createdAt: now,
      updatedAt: now,
    ));
    all = await repo.listAll();
    expect(all.map((t) => t.title), containsAll(['同步覆盖的标题', '远端来的新任务']));
  });

  test('memo：新增、更新、软删除', () async {
    final repo = MemoRepository(appDb.database);

    final memo = await repo.insert(newMemo('开会纪要'));
    expect(memo.uuid, isNotNull);

    await repo.update((await repo.listAll()).first.copyWith(content: '改过'));
    expect((await repo.listAll()).first.content, '改过');

    await repo.delete((await repo.listAll()).first);
    expect(await repo.listAll(), isEmpty);
    expect(await repo.listForSync(), hasLength(1));
  });

  test('settings store：读写与覆盖', () async {
    final store = SettingsStore(appDb.database);
    await store.write('k', {'a': 1});
    final raw = await store.read('k');
    expect(raw, '{"a":1}');

    await store.write('k', 'plain');
    expect(await store.read('k'), 'plain');
  });
}
