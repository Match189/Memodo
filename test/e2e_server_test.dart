// SPD Phase 7：跨设备一致性端到端测试（真栈）。
// 启动 todo-server 的 uvicorn（SQLite），两个"设备"（独立 SQLite 库）
// 通过 ServerSyncProvider 走完整协议，验证双向一致与墓碑传播。
// 依赖 todo-server/.venv（见 todo-server/README.md），缺失则跳过。
import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:path/path.dart' as p;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'package:memodo/data/app_database.dart';
import 'package:memodo/data/memo_repository.dart';
import 'package:memodo/data/task_repository.dart';
import 'package:memodo/models/task.dart';
import 'package:memodo/sync/server_sync_provider.dart';
import 'package:memodo/sync/sync_provider.dart';
import 'package:memodo/sync/sync_settings_model.dart' show ServerConfig;

const serverPort = 18123;

@Timeout(Duration(minutes: 3))
void main() {
  sqfliteFfiInit();
  databaseFactory = databaseFactoryFfi;

  late Process server;
  late Directory tempDir;
  final openedDatabases = <Database>[];
  final venvPython = File('todo-server/.venv/Scripts/python.exe').absolute.path;

  setUpAll(() async {
    if (!File(venvPython).existsSync()) {
      fail('SKIP: todo-server/.venv 不存在（见 todo-server/README.md 初始化）');
    }
    tempDir = await Directory.systemTemp.createTemp('todolist_e2e');
    server = await Process.start(
      venvPython,
      ['-m', 'uvicorn', 'app.main:app', '--port', '$serverPort'],
      // 工作目录 = 临时目录 → 服务器 SQLite 库相对落地，每次运行干净；
      // app 包经 PYTHONPATH 解析。
      workingDirectory: tempDir.path,
      environment: {
        ...Platform.environment,
        'PYTHONPATH': Directory('todo-server').absolute.path,
        'TODOLIST_DATABASE_URL': 'sqlite+aiosqlite:///./server.db',
      },
    );
    unawaited(server.stdout.pipe(File(p.join(tempDir.path, 'server-out.log'))
        .openWrite()));
    unawaited(server.stderr.pipe(File(p.join(tempDir.path, 'server-err.log'))
        .openWrite()));
    // 等服务就绪
    final client = HttpClient();
    for (var i = 0; i < 40; i++) {
      try {
        final r = await client
            .getUrl(Uri.parse('http://127.0.0.1:$serverPort/health'))
            .then((req) => req.close());
        if (r.statusCode == 200) return;
      } catch (_) {}
      await Future<void>.delayed(const Duration(milliseconds: 250));
    }
    fail('uvicorn 未在 10s 内就绪');
  });

  tearDownAll(() async {
    server.kill();
    for (final db in openedDatabases) {
      try {
        await db.close();
      } catch (_) {}
    }
    try {
      await tempDir.delete(recursive: true);
    } catch (_) {
      // Windows 上文件句柄释放可能滞后，清理失败不影响结论。
    }
  });

  Future<SyncContext> device(String name, String deviceId) async {
    final path = p.join(tempDir.path, '$name.db');
    final appDb = await AppDatabase.open(path: path);
    openedDatabases.add(appDb.database);
    return SyncContext(
      taskRepo: TaskRepository(appDb.database, deviceId: deviceId),
      memoRepo: MemoRepository(appDb.database, deviceId: deviceId),
      deviceId: deviceId,
    );
  }

  ServerSyncProvider provider() => ServerSyncProvider(
        config: ServerConfig()
          ..baseUrl = 'http://127.0.0.1:$serverPort'
          ..username = 'e2e@example.com'
          ..password = 'e2e-password-123',
      );

  /// 首次运行注册账号（已存在则忽略 409）。
  Future<void> registerUser() async {
    final client = HttpClient();
    final req = await client.postUrl(
        Uri.parse('http://127.0.0.1:$serverPort/api/v1/auth/register'));
    req.headers.contentType = ContentType.json;
    req.write(jsonEncode(
        {'email': 'e2e@example.com', 'password': 'e2e-password-123'}));
    final res = await req.close();
    await res.drain<void>();
    // 201 新注册 / 409 已存在，都算通过
    expect(res.statusCode, anyOf(201, 409));
    client.close();
  }

  test('双设备：添加 → 互相同步 → 删除墓碑传播 → 两端一致', () async {
    debugPrint('[e2e] 0 开始注册 ${DateTime.now()}');
    await registerUser();
    debugPrint('[e2e] 1 注册完成 ${DateTime.now()}');
    final windows = await device('windows', 'windows-e2e-a');
    final android = await device('android', 'android-e2e-b');
    final winProvider = provider();
    final androidProvider = provider();

    // 1. Windows 添加两条
    await windows.taskRepo.insert(Task(
      uuid: 't-x',
      title: '跨设备任务X',
      done: false,
      createdAt: DateTime(2026),
      updatedAt: DateTime(2026),
    ));
    await windows.taskRepo.insert(Task(
      uuid: 't-y',
      title: '跨设备任务Y',
      done: false,
      createdAt: DateTime(2026),
      updatedAt: DateTime(2026),
    ));

    // 2. Windows push+pull
    debugPrint('[e2e] 2 windows.sync begin ${DateTime.now()}');
    var r = await winProvider.sync(windows);
    debugPrint('[e2e] 2 windows.sync done ${DateTime.now()}');
    expect(r.ok, isTrue);

    // 3. Android 首次同步 → 拿到两条
    debugPrint('[e2e] 3 android.sync begin ${DateTime.now()}');
    r = await androidProvider.sync(android);
    debugPrint('[e2e] 3 android.sync done ${DateTime.now()}');
    expect(r.ok, isTrue);
    var androidTasks = await android.taskRepo.listAll();
    expect(androidTasks.map((t) => t.title),
        containsAll(['跨设备任务X', '跨设备任务Y']));

    // 4. Android 勾选 X（新时间戳），Windows 同时加 Z（离线互不干扰）
    debugPrint('[e2e] 4 begin ${DateTime.now()}');
    final fetchedX = androidTasks.firstWhere((t) => t.title == '跨设备任务X');
    await android.taskRepo.update(fetchedX.copyWith(
      done: true,
      updatedAt: DateTime(2026, 1, 2),
    ));
    debugPrint('[e2e] 4a toggle done ${DateTime.now()}');
    await windows.taskRepo.insert(Task(
      uuid: 't-z',
      title: '离线任务Z',
      done: false,
      createdAt: DateTime(2026),
      updatedAt: DateTime(2026, 1, 2, 1),
    ));
    debugPrint('[e2e] 4b insert Z done ${DateTime.now()}');

    // 5. 各自同步
    debugPrint('[e2e] 5a android.sync begin ${DateTime.now()}');
    await androidProvider.sync(android);
    debugPrint('[e2e] 5a done ${DateTime.now()}');
    await winProvider.sync(windows);
    debugPrint('[e2e] 5b windows.sync done ${DateTime.now()}');
    await androidProvider.sync(android); // Z 传给 Android
    debugPrint('[e2e] 5c done ${DateTime.now()}');

    // 6. 删除 Y（Windows），同步两次
    final winTasks = await windows.taskRepo.listAll();
    await windows.taskRepo
        .delete(winTasks.firstWhere((t) => t.title == '跨设备任务Y'));
    await winProvider.sync(windows);
    await androidProvider.sync(android);

    // 7. 最终一致性：两端可见任务完全一致
    final winFinal = await windows.taskRepo.listAll();
    final androidFinal = await android.taskRepo.listAll();
    final winTitles = winFinal.map((t) => '${t.title}:${t.done}').toSet();
    final androidTitles = androidFinal.map((t) => '${t.title}:${t.done}').toSet();
    expect(winTitles, androidTitles);
    expect(winTitles, contains('离线任务Z:false')); // Windows 离线新增传给安卓
    expect(winTitles, contains('跨设备任务X:true')); // Android 的勾选传回 Windows
    expect(winTitles, isNot(contains('跨设备任务Y:false'))); // 墓碑生效

    // 墓碑行在 Android 本地保留（deleted=1），但不出现在列表
    final androidAll = await android.taskRepo.listForSync();
    expect(androidAll.any((t) => t.uuid == 't-y' && t.deleted), isTrue);
  });
}
