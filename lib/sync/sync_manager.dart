import 'dart:async';

import 'package:flutter/foundation.dart';

import '../data/memo_repository.dart';
import '../data/task_repository.dart';
import '../state/memo_list_model.dart';
import '../state/task_list_model.dart';
import 'sync_provider.dart';
import 'sync_settings_model.dart';
import 'sync_transport.dart';

/// SPD §4：同步状态机。
enum SyncStatus { idle, syncing, success, failed, offline }

/// SPD §4：SyncManager。
/// 职责：按设置选择当前 SyncProvider、调度同步（手动/启动/防抖自动）、
/// 维护同步状态（含 offline），并把结果通知 UI 与挂载的列表模型。
/// 核心合并/传输逻辑都在 [SyncProvider] 实现里，本类不做协议。
class SyncManager extends ChangeNotifier {
  SyncManager({
    required TaskRepository taskRepository,
    required MemoRepository memoRepository,
    required SyncSettingsModel settings,
    required String deviceId,
    SyncProvider? providerOverride,
  })  : _taskRepo = taskRepository,
        _memoRepo = memoRepository,
        _settings = settings,
        _deviceId = deviceId,
        _providerOverride = providerOverride;

  final TaskRepository _taskRepo;
  final MemoRepository _memoRepo;
  final SyncSettingsModel _settings;
  final String _deviceId;

  /// 测试注入点：非空时替代按设置构造的 Provider。
  final SyncProvider? _providerOverride;

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

  /// 按当前设置构造 Provider；未配置/必填项缺失返回 null。
  SyncProvider? buildProvider() {
    if (_providerOverride != null) return _providerOverride;
    final transport = _settings.buildTransport();
    if (transport == null) return null;
    return SnapshotSyncProvider(
      transport: transport,
      passphrase: _settings.passphrase.isEmpty ? null : _settings.passphrase,
    );
  }

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
    final provider = buildProvider();
    if (provider == null) {
      if (manual) {
        status = SyncStatus.failed;
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
      final result = await provider.sync(SyncContext(
        taskRepo: _taskRepo,
        memoRepo: _memoRepo,
        deviceId: _deviceId,
      ));
      if (result.changedLocal) {
        await _taskModel?.load();
        await _memoModel?.load();
      }
      status = SyncStatus.success;
      _settings.lastSyncAt = DateTime.now();
      await _settings.save();
    } catch (e) {
      // SPD §4：网络层故障归类为 offline（本地功能不受影响，恢复后可重试）。
      status = isNetworkError(e) ? SyncStatus.offline : SyncStatus.failed;
      lastError = describeTransportError(e);
    } finally {
      _syncing = false;
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }
}
