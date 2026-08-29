import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:provider/provider.dart';
import 'package:sqflite/sqflite.dart' as sqflite;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'data/app_database.dart';
import 'data/device_identity.dart';
import 'data/memo_repository.dart';
import 'data/settings_store.dart';
import 'data/task_repository.dart';
import 'desktop/android_widget_settings.dart';
import 'desktop/main_window.dart' show mainWindowDisplayTitle;
import 'desktop/tray_service.dart';
import 'desktop/widget_launcher.dart';
import 'desktop/widget_settings.dart';
import 'desktop/win32_window_style.dart';
import 'home_widget_bridge.dart';
import 'pages/home_page.dart';
import 'pages/widget_window_page.dart';
import 'state/memo_list_model.dart';
import 'state/task_list_model.dart';
import 'sync/sync_manager.dart';
import 'sync/sync_settings_model.dart';
import 'theme/app_theme.dart';
import 'theme/theme_settings.dart';

Future<void> main(List<String> args) async {
  // 全局错误兜底：release 模式下未捕获异常不再"无声卡启动屏"，
  // 而是把错误直接画在屏幕上，方便用户截图反馈（尤其手机端无 adb 时）。
  await runZonedGuarded<Future<void>>(() async {
    await _boot(args);
  }, (error, stack) {
    _showFatalError(error, stack);
  });
}

Future<void> _boot(List<String> args) async {
  WidgetsFlutterBinding.ensureInitialized();

  final isDesktop =
      !kIsWeb && (Platform.isWindows || Platform.isLinux || Platform.isMacOS);
  // 桌面端没有平台内置的 SQLite 实现，切换到 FFI 驱动。
  // ⚠️ 必须在小组件子窗口分支之前：子窗口引擎同样要开库。
  if (isDesktop) {
    sqfliteFfiInit();
    sqflite.databaseFactory = databaseFactoryFfi;
  }

  // 桌面小组件子窗口：独立引擎，自己开库、自建状态模型。
  // 0.2.x 的约定：args = ['multi_window', windowId, arguments]
  if (Platform.isWindows &&
      args.isNotEmpty &&
      args.first == 'multi_window') {
    // 子窗口出错要落日志（否则就是用户看到的"白窗口"）。
    FlutterError.onError = (details) {
      debugPrint('[widget] FlutterError: ${details.exceptionAsString()}');
      FlutterError.presentError(details);
    };
    final windowId = int.parse(args[1]);
    var kind = 'todo';
    if (args.length > 2) {
      try {
        kind =
            ((jsonDecode(args[2]) as Map)['kind'] as String?) ?? 'todo';
      } catch (_) {}
    }
    final sqflite.Database db;
    try {
      final appDb = await AppDatabase.open();
      db = appDb.database;
    } catch (e) {
      debugPrint('[widget] init failed: $e');
      runApp(MaterialApp(
        home: Scaffold(
          body: Center(
            child: Text('小组件初始化失败：$e',
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 12)),
          ),
        ),
      ));
      return;
    }
    final subSettingsStore = SettingsStore(db);
    final device = await DeviceIdentity.load(subSettingsStore);
    final widgetSettings = DesktopWidgetSettingsModel(subSettingsStore);
    await widgetSettings.load();
    final themeSettings = ThemeSettingsModel(subSettingsStore);
    await themeSettings.load();
    runApp(WidgetWindowApp(
      windowId: windowId,
      kind: kind,
      taskModel: TaskListModel(
        TaskRepository(db, deviceId: device.id),
      ),
      memoModel: MemoListModel(
        MemoRepository(db, deviceId: device.id),
      ),
      widgetSettings: widgetSettings,
      themeSettings: themeSettings,
    ));
    WidgetsBinding.instance.addPostFrameCallback((_) {
      debugPrint('[widget] first frame rendered ✓');
    });
    return;
  }

  if (Platform.isWindows) {
    await migrateLegacyDatabase();
  }
  final db = await AppDatabase.open();

  // SPD §9：本机设备身份（同步 LWW 平局决胜）。
  final settingsStore = SettingsStore(db.database);
  final device = await DeviceIdentity.load(settingsStore);

  final taskModel = TaskListModel(
    TaskRepository(db.database, deviceId: device.id),
  );
  final memoModel = MemoListModel(
    MemoRepository(db.database, deviceId: device.id),
  );
  final syncSettings = SyncSettingsModel(settingsStore);
  await syncSettings.load();
  final desktopWidgetSettings = DesktopWidgetSettingsModel(settingsStore);
  await desktopWidgetSettings.load();
  final androidWidgetSettings = AndroidWidgetSettingsModel(settingsStore);
  await androidWidgetSettings.load();
  final themeSettings = ThemeSettingsModel(settingsStore);
  await themeSettings.load();

  final syncManager = SyncManager(
    taskRepository: TaskRepository(db.database, deviceId: device.id),
    memoRepository: MemoRepository(db.database, deviceId: device.id),
    settings: syncSettings,
    deviceId: device.id,
  );
  syncManager.attach(taskModel: taskModel, memoModel: memoModel);
  // 本地数据一变就安排一次防抖同步。
  taskModel.addListener(syncManager.scheduleSync);
  memoModel.addListener(syncManager.scheduleSync);

  if (Platform.isWindows) {
    _setupDesktopWidget(taskModel, memoModel, desktopWidgetSettings);
    unawaited(TrayService.instance.init());
    // 品牌化窗口标题（运行时改，规避 Runner 模板编码问题）。
    Timer(const Duration(milliseconds: 800), () {
      unawaited(
          WidgetWindowNative.setMainWindowTitle(mainWindowDisplayTitle));
    });
  }
  if (Platform.isAndroid) {
    _setupAndroidWidget(
      taskModel,
      memoModel,
      dbPath: db.database.path ?? '',
      widgetSettings: androidWidgetSettings,
    );
  }

  unawaited(taskModel.load());
  unawaited(memoModel.load());
  if (syncSettings.configured && syncSettings.autoSync) {
    unawaited(syncManager.syncNow());
  }

  runApp(MultiProvider(
    providers: [
      Provider<AppDatabase>.value(value: db),
      ChangeNotifierProvider.value(value: taskModel),
      ChangeNotifierProvider.value(value: memoModel),
      ChangeNotifierProvider.value(value: syncSettings),
      ChangeNotifierProvider.value(value: syncManager),
      ChangeNotifierProvider.value(value: desktopWidgetSettings),
      ChangeNotifierProvider.value(value: androidWidgetSettings),
      ChangeNotifierProvider.value(value: themeSettings),
    ],
    child: const TodolistApp(),
  ));
}

/// R1 包名迁移：老版本（com.example\todolist）的库文件搬到新品牌位置。
/// 只在新位置没有库时搬一次；两处都存在则信任新位置。
Future<void> migrateLegacyDatabase() async {
  final appData = Platform.environment['APPDATA'];
  if (appData == null) return;
  final legacy = File(p.join(appData, 'com.example', 'todolist', 'todolist.db'));
  if (!legacy.existsSync()) return;
  final support = await getApplicationSupportDir();
  final target = File(p.join(support, 'todolist.db'));
  if (target.existsSync()) return;
  await legacy.copy(target.path);
  debugPrint('[migrate] legacy db -> ${target.path}');
}

/// Windows 桌面小组件：跟随设置开关；小组件与主窗口互相同步数据变化。
void _setupDesktopWidget(
  TaskListModel taskModel,
  MemoListModel memoModel,
  DesktopWidgetSettingsModel widgetSettings,
) {
  WidgetLauncher.bind(widgetSettings);

  // 接收小组件子窗口的消息。
  DesktopMultiWindow.setMethodHandler((call, fromWindowId) async {
    switch (call.method) {
      case 'dataChangedFromWidget':
        await taskModel.load();
        await memoModel.load();
      case 'widgetClosed':
        final id = call.arguments is int ? call.arguments as int : null;
        if (id != null) WidgetLauncher.forget(id);
        await widgetSettings.setEnabled(false);
    }
    return null;
  });

  // 主窗口数据一变就广播给小组件重载（小组件不会回广播，不会成环）。
  void broadcastToWidget() {
    unawaited(() async {
      try {
        for (final id in await DesktopMultiWindow.getAllSubWindowIds()) {
          await DesktopMultiWindow.invokeMethod(id, 'dataChangedFromMain');
        }
      } catch (_) {}
    }());
  }

  taskModel.addListener(broadcastToWidget);
  memoModel.addListener(broadcastToWidget);

  // 应用启动时若上次开着小组件，则自动恢复。
  if (widgetSettings.enabled) {
    unawaited(WidgetLauncher.ensureOpen());
  }
}

/// 安卓桌面小组件：数据变化后防抖推送一份 JSON 快照给原生渲染。
void _setupAndroidWidget(
  TaskListModel taskModel,
  MemoListModel memoModel, {
  required String dbPath,
  required AndroidWidgetSettingsModel widgetSettings,
}) {
  Timer? pushTimer;
  void schedulePush() {
    pushTimer?.cancel();
    pushTimer = Timer(const Duration(milliseconds: 800), () {
      HomeWidgetBridge.maxItems = widgetSettings.maxItems;
      HomeWidgetBridge.showCompleted = widgetSettings.showCompleted;
      unawaited(HomeWidgetBridge.push(
        tasks: taskModel.tasks,
        memos: memoModel.memos,
        dbPath: dbPath,
      ));
    });
  }

  taskModel.addListener(schedulePush);
  memoModel.addListener(schedulePush);
  widgetSettings.addListener(schedulePush);
}

/// 致命错误展示：把异常与堆栈直接渲染出来（release 也能看到）。
void _showFatalError(Object error, StackTrace stack) {
  FlutterError.presentError(
      FlutterErrorDetails(exception: error, stack: stack));
  runApp(MaterialApp(
    debugShowCheckedModeBanner: false,
    home: Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: SelectableText(
            '启动失败，请截图反馈给开发者：\n\n$error\n\n$stack',
            style: const TextStyle(fontSize: 12),
          ),
        ),
      ),
    ),
  ));
}

class TodolistApp extends StatelessWidget {
  const TodolistApp({super.key});

  @override
  Widget build(BuildContext context) {
    final appearance = context.watch<ThemeSettingsModel>();
    return MaterialApp(
      title: '待办备忘',
      theme: AppTheme.light(appearance.seedColor),
      darkTheme:
          AppTheme.dark(appearance.seedColor, amoled: appearance.amoledBlack),
      themeMode: appearance.themeMode,
      home: const HomePage(),
    );
  }
}
