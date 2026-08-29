import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:memodo/home_widget_bridge.dart';
import 'package:memodo/models/memo.dart';
import 'package:memodo/models/task.dart';

void main() {
  List<Task> makeTasks(int n) => List.generate(
        n,
        (i) => Task(
          uuid: 'u$i',
          title: '任务$i',
          done: i.isEven,
          createdAt: DateTime(2026),
          updatedAt: DateTime(2026),
        ),
      );

  test('载荷：默认隐藏已完成，截断到上限，带统计与 uuid', () {
    final tasks = makeTasks(20);
    final memos = [
      Memo(
        uuid: 'm1',
        title: '备忘',
        content: '',
        createdAt: DateTime(2026),
        updatedAt: DateTime(2026),
      ),
    ];

    final payload = HomeWidgetBridge.buildPayload(tasks, memos);

    final list = jsonDecode(payload['widget_tasks']! as String) as List;
    // 20 条里 10 条未完成；默认 showCompleted=false
    expect(list, hasLength(10));
    expect((list.first as Map)['u'], 'u1');
    expect((list.first as Map)['t'], '任务1');
    expect((list.first as Map)['d'], false);

    final counts = jsonDecode(payload['widget_counts']! as String) as Map;
    expect(counts['tasks'], 20);
    expect(counts['memos'], 1);
  });

  test('载荷：showCompleted=true 时包含已完成并按上限截断', () {
    final tasks = makeTasks(20);
    final payload = HomeWidgetBridge.buildPayload(
      tasks,
      const [],
      maxItemsOverride: HomeWidgetBridge.maxItems,
      showCompletedOverride: true,
    );
    final list = jsonDecode(payload['widget_tasks']! as String) as List;
    expect(list, hasLength(HomeWidgetBridge.maxItems));
    expect((list.first as Map)['d'], true);
  });
}
