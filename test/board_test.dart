import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:memodo/data/app_database.dart';
import 'package:memodo/data/board_repository.dart';
import 'package:memodo/data/settings_store.dart';
import 'package:memodo/board/board_controller.dart';

void main() {
  sqfliteFfiInit();
  databaseFactory = databaseFactoryFfi;

  late AppDatabase appDb;

  setUp(() async {
    appDb = await AppDatabase.open(path: inMemoryDatabasePath);
  });

  tearDown(() async {
    await appDb.close();
  });

  group('图钉板数据层', () {
    test('ensureDefaultBoard 幂等；pin 去重；unpin 软删除', () async {
      final repo = BoardRepository(appDb.database, deviceId: 'dev-test');

      final b1 = await repo.ensureDefaultBoard();
      final b2 = await repo.ensureDefaultBoard();
      expect(b1, b2);

      final card1 = await repo.pinCard(
          boardUuid: b1, refType: 'todo', refUuid: 'task-1');
      expect(card1, isNotNull);

      // 同实体重复钉 → null（去重）
      final dup = await repo.pinCard(
          boardUuid: b1, refType: 'todo', refUuid: 'task-1');
      expect(dup, isNull);

      // 不同实体可钉
      final card2 = await repo.pinCard(
          boardUuid: b1, refType: 'memo', refUuid: 'memo-1');
      expect(card2, isNotNull);

      expect(await repo.listCards(b1), hasLength(2));

      await repo.unpin(card1!);
      final after = await repo.listCards(b1);
      expect(after, hasLength(1));
      expect(after.first.refUuid, 'memo-1');
    });
  });

  group('BoardController（布局持久化）', () {
    test('装载默认板；钉卡生成布局并持久化；重载恢复', () async {
      final store = SettingsStore(appDb.database);
      final controller = BoardController(
        boardRepository: BoardRepository(appDb.database, deviceId: 'd1'),
        settingsStore: store,
      );

      await controller.load();
      expect(controller.boardUuid, isNotNull);
      expect(controller.cards, isEmpty);

      final view = await controller.pinCard(
          refType: 'todo', refUuid: 'task-9');
      expect(view, isNotNull);
      expect(controller.cards, hasLength(1));
      // 旋转种子已生成（±1.5° 内）
      expect(view!.layout.rotationDegrees.abs(), lessThanOrEqualTo(1.5));

      // 模拟拖动后持久化
      controller.dragBy(view, 33, 21);
      await controller.endGesture(view);
      // dragBy 是相对增量：种子位 (48,64) + (33,21)
      expect(view.layout.x, 81);
      expect(view.layout.y, 85);

      // 新控制器（模拟应用重启）恢复卡片与布局
      final controller2 = BoardController(
        boardRepository: BoardRepository(appDb.database, deviceId: 'd1'),
        settingsStore: store,
      );
      await controller2.load();
      expect(controller2.cards, hasLength(1));
      expect(controller2.cards.first.layout.x, 81);
      expect(controller2.cards.first.layout.y, 85);
    });

    test('置顶递增 z 序', () async {
      final store = SettingsStore(appDb.database);
      final controller = BoardController(
        boardRepository: BoardRepository(appDb.database, deviceId: 'd1'),
        settingsStore: store,
      );
      await controller.load();
      final a = await controller.pinCard(
          refType: 'todo', refUuid: 'a');
      final b = await controller.pinCard(
          refType: 'todo', refUuid: 'b');

      controller.bringToFront(a!);
      expect(a.layout.z, greaterThan(b!.layout.z));
    });
  });
}
