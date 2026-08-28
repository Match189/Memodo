import 'dart:convert';

import 'package:crypto/crypto.dart' as crypto;

import '../data/memo_repository.dart';
import '../data/task_repository.dart';
import '../models/memo.dart';
import '../models/task.dart';
import 'merge.dart';
import 'snapshot_codec.dart';
import 'sync_transport.dart';

/// 同步结果：一次 sync 的成败与是否有数据变化。
class SyncResult {
  const SyncResult._(this.ok, this.error, this.changedLocal, this.uploaded);

  const SyncResult.ok({bool changedLocal = false, bool uploaded = false})
      : this._(true, null, changedLocal, uploaded);

  const SyncResult.failure(String error)
      : this._(false, error, false, false);

  final bool ok;
  final String? error;
  final bool changedLocal;
  final bool uploaded;
}

/// 同步上下文：Provider 操作本地数据层的最小接口。
class SyncContext {
  const SyncContext({
    required this.taskRepo,
    required this.memoRepo,
    required this.deviceId,
  });

  final TaskRepository taskRepo;
  final MemoRepository memoRepo;

  /// 本机设备标识（写入快照元信息与本地行）。
  final String deviceId;
}

/// SPD §3：同步 Provider 统一抽象。
/// 核心业务只认这个接口；WebDAV / OSS / Server 各自实现。
abstract interface class SyncProvider {
  String get name;

  /// 连通性与鉴权检查。
  Future<void> testConnection();

  /// 完整同步（拉取 → 合并 → 落库 → 推送）。
  Future<SyncResult> sync(SyncContext ctx);
}

/// 基于全量快照的 Provider 实现，WebDAV 与 OSS 共用：
/// 拉取远端快照 → 按 uuid LWW 合并 → 差异落库 → 回传合并结果。
class SnapshotSyncProvider implements SyncProvider {
  SnapshotSyncProvider({required this.transport, this.passphrase});

  final SyncTransport transport;
  final String? passphrase;

  String? _lastUploadedHash;

  @override
  String get name => transport.displayName;

  @override
  Future<void> testConnection() => transport.testConnection();

  @override
  Future<SyncResult> sync(SyncContext ctx) async {
    final codec = SnapshotCodec(passphrase);

    // 1. 拉取远端快照（可能不存在）
    final remoteBody = await transport.fetchSnapshot();
    final remote = remoteBody == null ? null : await codec.decode(remoteBody);

    // 2. 与本地全量（含墓碑）按 uuid 合并
    final localTasks = await ctx.taskRepo.listForSync();
    final localMemos = await ctx.memoRepo.listForSync();
    final merged = SyncMerge.merge(
      localTasks: localTasks,
      remoteTasks: remote?.tasks ?? const [],
      localMemos: localMemos,
      remoteMemos: remote?.memos ?? const [],
    );

    // 3. 合并结果里有差异的行写回本地
    final localTaskById = <String, Task>{
      for (final t in localTasks)
        if (t.uuid != null) t.uuid!: t,
    };
    final localMemoById = <String, Memo>{
      for (final m in localMemos)
        if (m.uuid != null) m.uuid!: m,
    };
    var changed = 0;
    for (final task in merged.tasks) {
      final uuid = task.uuid;
      if (uuid == null) continue;
      final local = localTaskById[uuid];
      if (local == null || !SyncMerge.sameTask(local, task)) {
        await ctx.taskRepo.upsertByUuid(task);
        changed++;
      }
    }
    for (final memo in merged.memos) {
      final uuid = memo.uuid;
      if (uuid == null) continue;
      final local = localMemoById[uuid];
      if (local == null || !SyncMerge.sameMemo(local, memo)) {
        await ctx.memoRepo.upsertByUuid(memo);
        changed++;
      }
    }

    // 4. 回传合并后的快照；内容没变就跳过上传，省流量配额
    final body = await codec.encode(
      Snapshot(tasks: merged.tasks, memos: merged.memos, device: ctx.deviceId),
    );
    final hash = crypto.sha256.convert(utf8.encode(body)).toString();
    var uploaded = false;
    if (hash != _lastUploadedHash) {
      await transport.uploadSnapshot(body);
      _lastUploadedHash = hash;
      uploaded = true;
    }

    return SyncResult.ok(changedLocal: changed > 0, uploaded: uploaded);
  }
}
