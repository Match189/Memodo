import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite/sqflite.dart' as sqflite;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:todolist/data/app_database.dart';
import 'package:todolist/data/memo_repository.dart';
import 'package:todolist/data/settings_store.dart';
import 'package:todolist/data/task_repository.dart';
import 'package:todolist/models/memo.dart';
import 'package:todolist/models/task.dart';
import 'package:todolist/state/memo_list_model.dart';
import 'package:todolist/state/task_list_model.dart';
import 'package:todolist/sync/snapshot_codec.dart';
import 'package:todolist/sync/sync_engine.dart';
import 'package:todolist/sync/sync_settings_model.dart';
import 'package:todolist/sync/sync_transport.dart';

void main() {
  sqfliteFfiInit();
  sqflite.databaseFactory = databaseFactoryFfi;

  DateTime at(int minutes) => DateTime(2026, 1, 1, 10, minutes);

  Task task(String uuid, String title, DateTime updatedAt,
          {bool deleted = false}) =>
      Task(
        uuid: uuid,
        title: title,
        done: false,
        createdAt: DateTime(2026, 1, 1),
        updatedAt: updatedAt,
        deleted: deleted,
      );

  group('合并（LWW）', () {
    test('两端并集；同 uuid 新时间戳胜出', () {
      final result = SyncEngine.merge(
        localTasks: [task('A', '本地新版', at(10)), task('C', '仅本地', at(5))],
        remoteTasks: [
          task('A', '远端旧版', at(3)),
          task('B', '仅远端', at(7)),
        ],
        localMemos: const [],
        remoteMemos: const [],
      );
      expect(result.tasks, hasLength(3));
      expect(
          result.tasks.firstWhere((t) => t.uuid == 'A').title, '本地新版');
      expect(result.tasks.any((t) => t.uuid == 'B'), isTrue);
      expect(result.tasks.any((t) => t.uuid == 'C'), isTrue);
    });

    test('远端墓碑（删除）会胜过本地旧内容', () {
      final result = SyncEngine.merge(
        localTasks: [task('A', '还活着', at(5))],
        remoteTasks: [task('A', '任意', at(9), deleted: true)],
        localMemos: const [],
        remoteMemos: const [],
      );
      expect(result.tasks.single.deleted, isTrue);
    });

    test('时间戳完全相等时保留本地，避免乒乓', () {
      final result = SyncEngine.merge(
        localTasks: [task('A', '本地', at(5))],
        remoteTasks: [task('A', '远端', at(5))],
        localMemos: const [],
        remoteMemos: const [],
      );
      expect(result.tasks.single.title, '本地');
    });

    test('没有 uuid 的行只保留本地，不上传覆盖他人', () {
      final result = SyncEngine.merge(
        localTasks: [
          Task(title: '无uuid本地', done: false, createdAt: at(1), updatedAt: at(1)),
          task('A', '正常', at(2)),
        ],
        remoteTasks: [Task(title: '无uuid远端', done: false, createdAt: at(1), updatedAt: at(1))],
        localMemos: const [],
        remoteMemos: const [],
      );
      expect(result.tasks, hasLength(2));
      expect(result.tasks.any((t) => t.title == '无uuid远端'), isFalse);
    });
  });

  group('快照编解码与加密', () {
    final codecPlain = SnapshotCodec(null);
    final sample = Snapshot(
      tasks: [task('A', '任务', at(5))],
      memos: [
        Memo(
          uuid: 'M1',
          title: '备忘',
          content: '正文',
          createdAt: at(1),
          updatedAt: at(2),
        ),
      ],
    );

    test('明文 JSON 编解码往返', () async {
      final body = await codecPlain.encode(sample);
      expect(body.startsWith('TODOLIST-ENC1:'), isFalse);
      final decoded = await codecPlain.decode(body);
      expect(decoded.tasks.single.uuid, 'A');
      expect(decoded.memos.single.title, '备忘');
    });

    test('加密往返；错误口令与缺口令都报错', () async {
      final codecEnc = SnapshotCodec('口令123');
      final body = await codecEnc.encode(sample);
      expect(body.startsWith('TODOLIST-ENC1:'), isTrue);

      final decoded = await SnapshotCodec('口令123').decode(body);
      expect(decoded.memos.single.content, '正文');

      await expectLater(SnapshotCodec('错的').decode(body),
          throwsFormatException);
      await expectLater(codecPlain.decode(body), throwsFormatException);
    });
  });

  group('同步引擎整链路（内存库 + 假通道）', () {
    late AppDatabase appDb;

    setUp(() async {
      appDb = await AppDatabase.open(path: inMemoryDatabasePath);
    });
    tearDown(() async => appDb.close());

    test('拉取合并落库、回传合并结果', () async {
      final taskRepo = TaskRepository(appDb.database);
      final memoRepo = MemoRepository(appDb.database);
      final settingsStore = SettingsStore(appDb.database);
      final syncSettings = SyncSettingsModel(settingsStore);

      final taskModel = TaskListModel(taskRepo);
      final memoModel = MemoListModel(memoRepo);
      final fake = FakeTransport(SnapshotCodec(null).encode(Snapshot(
        tasks: [
          task('A', '远端旧标题', at(3)),
          task('B', '远端新任务', at(8)),
        ],
        memos: [
          Memo(
              uuid: 'M1',
              title: '远端备忘',
              content: '',
              createdAt: at(1),
              updatedAt: at(1))
        ],
      )));

      final engine = SyncEngine(
        taskRepository: taskRepo,
        memoRepository: memoRepo,
        settings: syncSettings,
        transportOverride: fake,
      );
      engine.attach(taskModel: taskModel, memoModel: memoModel);
      syncSettings.channel = SyncChannel.server; // 标记为已配置（不影响传输实现）

      // 本地：一条已有任务（uuid 相同、比远端新）
      final local = await taskRepo.insert(task('A', '本地任务', at(10)));

      await engine.syncNow();

      // 本地库：A 保持本地新版，B、M1 已同步进来
      final tasks = await taskRepo.listAll();
      expect(tasks.map((t) => t.title), containsAll(['本地任务', '远端新任务']));
      expect((await memoRepo.listAll()).single.title, '远端备忘');

      // 回传的快照里包含合并后的全部 3 条
      final uploaded = await SnapshotCodec(null).decode(fake.lastUpload!);
      expect(uploaded.tasks, hasLength(2));
      expect(uploaded.tasks.firstWhere((t) => t.uuid == local.uuid).title,
          '本地任务');
      expect(uploaded.memos, hasLength(1));
      expect(engine.status, SyncStatus.success);
    });

    test('未配置通道时手动同步给出提示而不崩溃', () async {
      final engine = SyncEngine(
        taskRepository: TaskRepository(appDb.database),
        memoRepository: MemoRepository(appDb.database),
        settings: SyncSettingsModel(SettingsStore(appDb.database)),
      );
      await engine.syncNow(manual: true);
      expect(engine.status, SyncStatus.error);
      expect(engine.lastError, contains('未配置'));
    });
  });
}

/// 内存假通道：返回预置快照，记录最近一次上传。
class FakeTransport implements SyncTransport {
  FakeTransport(this.preloaded);

  final Future<String> preloaded;
  String? lastUpload;

  @override
  String get displayName => 'Fake';

  @override
  Future<void> testConnection() async {}

  @override
  Future<String?> fetchSnapshot() async => preloaded;

  @override
  Future<void> uploadSnapshot(String body) async {
    lastUpload = body;
  }
}
