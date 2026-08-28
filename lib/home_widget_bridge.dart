import 'dart:convert';
import 'dart:io';

import 'package:home_widget/home_widget.dart';

import 'models/memo.dart';
import 'models/task.dart';

/// 安卓桌面小组件的数据推送：把任务列表序列化成 JSON 存进
/// home_widget 的共享存储，然后唤醒原生 [TodayWidgetProvider] 重绘。
/// 纯 Dart 无法在小组件里跑 Flutter，所以原生侧只读这份 JSON。
class HomeWidgetBridge {
  HomeWidgetBridge._();

  static const _tasksKey = 'widget_tasks';
  static const _countsKey = 'widget_counts';
  static const _providerClass = 'com.example.todolist.TodayWidgetProvider';
  static const maxItems = 12;

  /// 纯函数：构造推送载荷（便于单测）。
  static Map<String, Object?> buildPayload(List<Task> tasks, List<Memo> memos) {
    return {
      _tasksKey: jsonEncode([
        for (final t in tasks.take(maxItems))
          {'t': t.title, 'd': t.done},
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
  }) async {
    if (!Platform.isAndroid) return;
    final payload = buildPayload(tasks, memos);
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
