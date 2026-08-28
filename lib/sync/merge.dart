import '../models/memo.dart';
import '../models/task.dart';

/// SPD §19：冲突策略 = Last Write Wins。
/// updatedAt 较新者胜；时间戳完全相等时用 deviceId 字典序决胜（两端结论一致）；
/// 仍相等则保留本地。没有 uuid 的行视为本地特有，原样保留（远端会忽略）。
class SyncMerge {
  SyncMerge._();

  static MergeResult merge({
    required List<Task> localTasks,
    required List<Task> remoteTasks,
    required List<Memo> localMemos,
    required List<Memo> remoteMemos,
  }) {
    return MergeResult(
      tasks: _mergeRows<Task>(
        localTasks,
        remoteTasks,
        key: (t) => t.uuid,
        updatedAt: (t) => t.updatedAt,
        deviceId: (t) => t.deviceId ?? '',
        isSame: sameTask,
      ),
      memos: _mergeRows<Memo>(
        localMemos,
        remoteMemos,
        key: (m) => m.uuid,
        updatedAt: (m) => m.updatedAt,
        deviceId: (m) => m.deviceId ?? '',
        isSame: sameMemo,
      ),
    );
  }

  static bool sameTask(Task a, Task b) =>
      a.title == b.title &&
      a.description == b.description &&
      a.done == b.done &&
      a.priority == b.priority &&
      a.dueAt == b.dueAt &&
      a.deleted == b.deleted &&
      a.deletedAt == b.deletedAt &&
      a.updatedAt == b.updatedAt;

  static bool sameMemo(Memo a, Memo b) =>
      a.title == b.title &&
      a.content == b.content &&
      a.deleted == b.deleted &&
      a.deletedAt == b.deletedAt &&
      a.updatedAt == b.updatedAt;

  static List<T> _mergeRows<T>(
    List<T> local,
    List<T> remote, {
    required String? Function(T) key,
    required DateTime Function(T) updatedAt,
    required String Function(T) deviceId,
    bool Function(T, T)? isSame,
  }) {
    final merged = <String, T>{};
    final localOnly = <T>[];
    for (final row in local) {
      final k = key(row);
      if (k == null) {
        localOnly.add(row);
      } else {
        merged[k] = row;
      }
    }
    for (final row in remote) {
      final k = key(row);
      if (k == null) continue;
      final existing = merged[k];
      if (existing == null || _newer(row, existing, updatedAt, deviceId)) {
        merged[k] = row;
      }
    }
    return [...localOnly, ...merged.values];
  }

  /// 严格更新者胜出（新时间戳；平局比 deviceId；再平局保留 existing=本地）。
  static bool _newer<T>(
    T candidate,
    T existing,
    DateTime Function(T) updatedAt,
    String Function(T) deviceId,
  ) {
    final c = updatedAt(candidate);
    final e = updatedAt(existing);
    if (c.isAfter(e)) return true;
    if (c.isBefore(e)) return false;
    return deviceId(candidate).compareTo(deviceId(existing)) > 0;
  }
}

class MergeResult {
  const MergeResult({required this.tasks, required this.memos});

  final List<Task> tasks;
  final List<Memo> memos;
}
