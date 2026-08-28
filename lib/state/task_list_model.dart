import 'package:flutter/foundation.dart';

import '../data/task_repository.dart';
import '../models/task.dart';

/// 任务列表的状态：持有数据并在变更后通知界面刷新。
class TaskListModel extends ChangeNotifier {
  TaskListModel(this._repo);

  final TaskRepository _repo;

  List<Task> _tasks = const [];
  bool _loading = true;

  List<Task> get tasks => _tasks;
  bool get loading => _loading;
  bool get hasDoneTasks => _tasks.any((t) => t.done);

  Future<void> load() async {
    _tasks = await _repo.listAll();
    _loading = false;
    notifyListeners();
  }

  Future<void> add(String title) async {
    final trimmed = title.trim();
    if (trimmed.isEmpty) return;
    final now = DateTime.now();
    await _repo.insert(Task(
      title: trimmed,
      done: false,
      createdAt: now,
      updatedAt: now,
    ));
    await load();
  }

  Future<void> toggle(Task task) async {
    await _repo.update(task.copyWith(done: !task.done));
    await load();
  }

  Future<void> rename(Task task, String title) async {
    final trimmed = title.trim();
    if (trimmed.isEmpty || trimmed == task.title) return;
    await _repo.update(task.copyWith(title: trimmed));
    await load();
  }

  Future<void> remove(Task task) async {
    await _repo.delete(task);
    await load();
  }

  /// 撤销删除：清除墓碑并刷新。
  Future<void> restore(Task task) async {
    await _repo.undelete(task);
    await load();
  }

  Future<void> clearDone() async {
    await _repo.deleteDone();
    await load();
  }
}
