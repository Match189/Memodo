import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:todolist/home_widget_bridge.dart';
import 'package:todolist/models/memo.dart';
import 'package:todolist/models/task.dart';

void main() {
  test('小组件载荷：映射字段、截断到上限、带统计', () {
    final tasks = List.generate(
      20,
      (i) => Task(
        uuid: 'u$i',
        title: '任务$i',
        done: i.isEven,
        createdAt: DateTime(2026),
        updatedAt: DateTime(2026),
      ),
    );
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
    expect(list, hasLength(HomeWidgetBridge.maxItems));
    expect((list.first as Map)['t'], '任务0');
    expect((list.first as Map)['d'], true);

    final counts = jsonDecode(payload['widget_counts']! as String) as Map;
    expect(counts['tasks'], 20);
    expect(counts['memos'], 1);
  });
}
