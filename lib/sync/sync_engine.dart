import 'dart:async';
import 'dart:convert';

import 'package:crypto/crypto.dart' as crypto;
import 'package:flutter/foundation.dart';

import '../data/memo_repository.dart';
import '../data/task_repository.dart';
import '../models/memo.dart';
import '../models/task.dart';
import '../state/memo_list_model.dart';
import '../state/task_list_model.dart';
import 'snapshot_codec.dart';
import 'sync_settings_model.dart';
import 'sync_transport.dart';

enum SyncStatus { idle, syncing, success, error }

/// 同步引擎：拉取远端快照 → 按 uuid 合并（LWW）→ 落库 → 回传合并结果。
///
/// 合并是纯函数（[SyncEngine.merge]），网络与存储都可注入，便于测试。
class SyncEngine extends ChangeNotifier {
  SyncEngine({
    required TaskRepository taskRepository,
    required MemoRepository memoRepository,
    required SyncSettingsModel settings,
    SyncTransport? transportOverride,
  })  : _taskRepo = taskRepository,
        _memoRepo = memoRepository,
        // ignore: prefer_initializing_formals
        _settings = settings,
        // ignore: prefer_initializing_formals
        _transportOverride = transportOverride;

  final TaskRepository _taskRepo;
  final MemoRepository _memoRepo;
  final SyncSettingsModel _settings;

  /// 测试注入点：非空时替代按设置构造的传输实现。
  final SyncTransport? _transportOverride;

  TaskListModel? _taskModel;
  MemoListModel? _memoModel;

  /// 挂载界面状态模型：合并落库后通知列表重载。
  void attach({
    required TaskListModel taskModel,
    required MemoListModel memoModel,
  }) {
    _taskModel = taskModel;
    _memoModel = memoModel;
  }

  SyncStatus status = SyncStatus.idle;
  String? lastError;
  DateTime? get lastSuccessAt => _settings.lastSyncAt;

  bool _syncing = false;
  Timer? _debounce;
  String? _lastUploadedHash;

  /// 本地数据变化后调用：防抖 3 秒后自动同步一次。
  void scheduleSync() {
    if (!_settings.configured || !_settings.autoSync) return;
    _debounce?.cancel();
    _debounce = Timer(const Duration(seconds: 3), () {
      unawaited(syncNow());
    });
  }

  /// 手动/自动同步入口。[manual] 时即使未配置也给出提示信息。
  Future<void> syncNow({bool manual = false}) async {
    if (_syncing) return;
    final transport = _transportOverride ?? _settings.buildTransport();
    if (transport == null) {
      if (manual) {
        status = SyncStatus.error;
        lastError = '尚未配置同步通道或必填项不完整';
        notifyListeners();
      }
      return;
    }

    _syncing = true;
    status = SyncStatus.syncing;
    lastError = null;
    notifyListeners();

    try {
      final codec = SnapshotCodec(
        _settings.passphrase.isEmpty ? null : _settings.passphrase,
      );

      // 1. 拉取远端快照（可能不存在）
      final remoteBody = await transport.fetchSnapshot();
      final remote = remoteBody == null ? null : await codec.decode(remoteBody);

      // 2. 与本地全量（含墓碑）按 uuid 合并
      final localTasks = await _taskRepo.listForSync();
      final localMemos = await _memoRepo.listForSync();
      final merged = merge(
        localTasks: localTasks,
        remoteTasks: remote?.tasks ?? const [],
        localMemos: localMemos,
        remoteMemos: remote?.memos ?? const [],
      );

      // 3. 合并结果里有差异的行写回本地，并刷新界面
      var changed = 0;
      for (final task in merged.tasks) {
        if (!_localHasSameTask(localTasks, task)) {
          await _taskRepo.upsertByUuid(task);
          changed++;
        }
      }
      for (final memo in merged.memos) {
        if (!_localHasSameMemo(localMemos, memo)) {
          await _memoRepo.upsertByUuid(memo);
          changed++;
        }
      }
      if (changed > 0) {
        await _taskModel?.load();
        await _memoModel?.load();
      }

      // 4. 回传合并后的快照；内容没变就跳过上传，省流量配额
      final body = await codec.encode(
        Snapshot(tasks: merged.tasks, memos: merged.memos),
      );
      final hash = crypto.sha256.convert(utf8.encode(body)).toString();
      if (hash != _lastUploadedHash) {
        await transport.uploadSnapshot(body);
        _lastUploadedHash = hash;
      }

      status = SyncStatus.success;
      _settings.lastSyncAt = DateTime.now();
      await _settings.save();
    } catch (e) {
      status = SyncStatus.error;
      lastError = describeTransportError(e);
    } finally {
      _syncing = false;
      notifyListeners();
    }
  }

  /// 纯函数合并：按 uuid 取并集，同一 uuid 取 updatedAt 更新的一边（LWW）。
  /// 时间戳完全相等时保留本地，避免两端互相覆盖打乒乓。
  /// 没有 uuid 的行视为本地特有，不参与远端合并。
  @visibleForTesting
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
        isSame: (a, b) =>
            a.title == b.title &&
            a.done == b.done &&
            a.deleted == b.deleted &&
            a.updatedAt == b.updatedAt,
      ),
      memos: _mergeRows<Memo>(
        localMemos,
        remoteMemos,
        key: (m) => m.uuid,
        updatedAt: (m) => m.updatedAt,
        isSame: (a, b) =>
            a.title == b.title &&
            a.content == b.content &&
            a.deleted == b.deleted &&
            a.updatedAt == b.updatedAt,
      ),
    );
  }

  static List<T> _mergeRows<T>(
    List<T> local,
    List<T> remote, {
    required String? Function(T) key,
    required DateTime Function(T) updatedAt,
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
      if (existing == null || updatedAt(row).isAfter(updatedAt(existing))) {
        merged[k] = row;
      }
    }
    return [...localOnly, ...merged.values];
  }

  bool _localHasSameTask(List<Task> localTasks, Task candidate) {
    for (final t in localTasks) {
      if (t.uuid == candidate.uuid) {
        return t.title == candidate.title &&
            t.done == candidate.done &&
            t.deleted == candidate.deleted &&
            t.updatedAt == candidate.updatedAt;
      }
    }
    return false;
  }

  bool _localHasSameMemo(List<Memo> localMemos, Memo candidate) {
    for (final m in localMemos) {
      if (m.uuid == candidate.uuid) {
        return m.title == candidate.title &&
            m.content == candidate.content &&
            m.deleted == candidate.deleted &&
            m.updatedAt == candidate.updatedAt;
      }
    }
    return false;
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }
}

class MergeResult {
  const MergeResult({required this.tasks, required this.memos});

  final List<Task> tasks;
  final List<Memo> memos;
}
