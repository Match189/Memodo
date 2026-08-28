import 'dart:convert';
import 'dart:io';

import 'package:home_widget/home_widget.dart';

import 'models/memo.dart';
import 'models/task.dart';

/// 安卓桌面小组件的推送桥（SPD §12/§13/§14）：
/// 把任务快照 + 本机数据库路径 + 显示设置写入 home_widget 共享存储，
/// 然后唤醒原生 [TodayWidgetProvider] 重绘。
///
/// 小组件上的勾选由 Kotlin 原生直写 SQLite（路径来自 db_path），
/// 不需要 Flutter 引擎常驻（SPD 禁止事项 #6）。
class HomeWidgetBridge {
  HomeWidgetBridge._();

  static const _tasksKey = 'widget_tasks';
  static const _countsKey = 'widget_counts';
  static const _dbPathKey = 'db_path';
  static const _showCompletedKey = 'show_completed';
  static const _maxItemsKey = 'max_items';
  static const _providerClass = 'com.example.todolist.TodayWidgetProvider';

  static int maxItems = 12;
  static bool showCompleted = false;

  /// 纯函数：构造推送载荷（便于单测）。
  static Map<String, Object?> buildPayload(
    List<Task> tasks,
    List<Memo> memos, {
    int? maxItemsOverride,
    bool? showCompletedOverride,
  }) {
    final cap = maxItemsOverride ?? maxItems;
    final showDone = showCompletedOverride ?? showCompleted;
    final visible = showDone ? tasks : tasks.where((t) => !t.done).toList();
    return {
      _tasksKey: jsonEncode([
        for (final t in visible.take(cap))
          {'u': t.uuid, 't': t.title, 'd': t.done},
      ]),
      _countsKey: jsonEncode({
        'tasks': tasks.length,
        'memos': memos.length,
      }),
    };
  }

  /// 数据变化/同步完成后调用；只在安卓上有意义。
  static Future<void> push({
    required List<Task> tasks,
    required List<Memo> memos,
    required String dbPath,
  }) async {
    if (!Platform.isAndroid) return;
    final payload = buildPayload(tasks, memos);
    await HomeWidget.saveWidgetData<String>(
        _dbPathKey, dbPath.replaceAll('\\', '/'));
    await HomeWidget.saveWidgetData<bool>(_showCompletedKey, showCompleted);
    await HomeWidget.saveWidgetData<int>(_maxItemsKey, maxItems);
    for (final entry in payload.entries) {
      await HomeWidget.saveWidgetData<String>(entry.key, entry.value as String);
    }
    await HomeWidget.updateWidget(
      name: 'TodayWidgetProvider',
      qualifiedAndroidName: _providerClass,
    );
  }

  /// 引导用户把小组件钉到桌面（Android 8+ 系统弹窗）。
  static Future<void> requestPin() async {
    if (!Platform.isAndroid) return;
    await HomeWidget.requestPinWidget(
      name: 'TodayWidgetProvider',
      qualifiedAndroidName: _providerClass,
    );
  }
}
