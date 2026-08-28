import 'dart:async';
import 'dart:io';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:sqflite/sqflite.dart' as sqflite;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'data/app_database.dart';
import 'data/device_identity.dart';
import 'data/memo_repository.dart';
import 'data/settings_store.dart';
import 'data/task_repository.dart';
import 'desktop/android_widget_settings.dart';
import 'desktop/widget_launcher.dart';
import 'desktop/widget_settings.dart';
import 'home_widget_bridge.dart';
import 'pages/home_page.dart';
import 'pages/widget_window_page.dart';
import 'state/memo_list_model.dart';
import 'state/task_list_model.dart';
import 'sync/sync_manager.dart';
import 'sync/sync_settings_model.dart';

Future<void> main(List<String> args) async {
  WidgetsFlutterBinding.ensureInitialized();

  // 桌面小组件子窗口：独立引擎，自己开库、自建状态模型。
  // 0.2.x 的约定：args = ['multi_window', windowId, arguments]
  if (Platform.isWindows &&
      args.isNotEmpty &&
      args.first == 'multi_window') {
    final windowId = int.parse(args[1]);
    final db = await AppDatabase.open();
    final subSettingsStore = SettingsStore(db.database);
    final device = await DeviceIdentity.load(subSettingsStore);
    final widgetSettings =
        DesktopWidgetSettingsModel(subSettingsStore);
    await widgetSettings.load();
    runApp(WidgetWindowApp(
      windowId: windowId,
      taskModel: TaskListModel(
        TaskRepository(db.database, deviceId: device.id),
      ),
      memoModel: MemoListModel(
        MemoRepository(db.database, deviceId: device.id),
      ),
      widgetSettings: widgetSettings,
    ));
    return;
  }

  final isDesktop =
      !kIsWeb && (Platform.isWindows || Platform.isLinux || Platform.isMacOS);
  // 桌面端没有平台内置的 SQLite 实现，切换到 FFI 驱动；安卓用平台自带实现。
  if (isDesktop) {
    sqfliteFfiInit();
    sqflite.databaseFactory = databaseFactoryFfi;
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
  }
  final androidWidgetSettings = AndroidWidgetSettingsModel(settingsStore);
  await androidWidgetSettings.load();
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
    ],
    child: const TodolistApp(),
  ));
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
    unawaited(WidgetLauncher.ensureOpen(
      alwaysOnTop: widgetSettings.alwaysOnTop,
      opacity: widgetSettings.opacity,
    ));
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

class TodolistApp extends StatelessWidget {
  const TodolistApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: '待办备忘',
      theme: ThemeData(
        colorSchemeSeed: const Color(0xFF00696D),
        useMaterial3: true,
      ),
      darkTheme: ThemeData(
        brightness: Brightness.dark,
        colorSchemeSeed: const Color(0xFF00696D),
        useMaterial3: true,
      ),
      home: const HomePage(),
    );
  }
}
