import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../data/memo_repository.dart';
import '../data/task_repository.dart';
import '../models/memo.dart';
import '../models/task.dart';
import 'merge.dart';
import 'sync_provider.dart';
import 'sync_settings_model.dart';

/// SPD Phase 6：自建服务器通道的 SyncProvider（§5-§9 协议）。
///
/// - 认证：用户名/密码 → JWT（401 时 refresh，失败则重新登录）
/// - push：本地全量变更（小数据量，逐条由服务器 LWW 仲裁）
/// - pull：cursor 增量，循环取完 hasMore
/// - 应用远端变化时带 LWW 守卫（本地更新的离线修改不被覆盖）
class ServerSyncProvider implements SyncProvider {
  ServerSyncProvider({
    required this.config,
    http.Client? httpClient,
  }) : _client = httpClient ?? http.Client();

  final ServerConfig config;
  final http.Client _client;

  static const _timeout = Duration(seconds: 30);

  Uri _uri(String path, [Map<String, String>? q]) {
    final base = Uri.parse(config.baseUrl.trim());
    final p = base.path.endsWith('/')
        ? base.path.substring(0, base.path.length - 1)
        : base.path;
    return base.replace(path: '$p$path', queryParameters: q);
  }

  Map<String, String> _authHeaders() => {
        'Authorization': 'Bearer ${config.accessToken}',
        'Content-Type': 'application/json; charset=utf-8',
      };

  Future<Map<String, Object?>> _postJson(
      String path, Map<String, Object?> body) async {
    final r = await _client
        .post(_uri(path), headers: _authHeaders(), body: jsonEncode(body))
        .timeout(_timeout);
    if (r.statusCode / 100 != 2) {
      throw ServerApiException(r.statusCode, r.body);
    }
    return (jsonDecode(r.body) as Map).cast<String, Object?>();
  }

  Future<Map<String, Object?>> _getJson(String path,
      [Map<String, String>? q]) async {
    final r = await _client
        .get(_uri(path, q), headers: _authHeaders())
        .timeout(_timeout);
    if (r.statusCode / 100 != 2) {
      throw ServerApiException(r.statusCode, r.body);
    }
    return (jsonDecode(r.body) as Map).cast<String, Object?>();
  }

  /// 确保拿到可用 access token：优先 refresh，失败/缺失则账号登录。
  Future<void> ensureAuthenticated() async {
    if (config.accessToken.isEmpty) {
      await _login();
      return;
    }
    // 预检：用 pull 探测 token 是否仍有效
    try {
      await _getJson('/api/v1/sync/pull', {'cursor': '${config.cursor}'});
    } on ServerApiException catch (e) {
      if (e.statusCode == 401) {
        final refreshed = await _tryRefresh();
        if (!refreshed) await _login();
      } else {
        rethrow;
      }
    }
  }

  Future<void> _login() async {
    final r = await _client
        .post(
          _uri('/api/v1/auth/login'),
          headers: {'Content-Type': 'application/json; charset=utf-8'},
          body: jsonEncode({
            'email': config.username.trim(),
            'password': config.password,
          }),
        )
        .timeout(_timeout);
    if (r.statusCode != 200) {
      throw ServerApiException(r.statusCode, '登录失败');
    }
    final body = (jsonDecode(r.body) as Map).cast<String, Object?>();
    config.accessToken = body['access_token'] as String? ?? '';
    config.refreshToken = body['refresh_token'] as String? ?? '';
  }

  Future<bool> _tryRefresh() async {
    if (config.refreshToken.isEmpty) return false;
    try {
      final r = await _client
          .post(
            _uri('/api/v1/auth/refresh'),
            headers: {'Content-Type': 'application/json; charset=utf-8'},
            body: jsonEncode({'refresh_token': config.refreshToken}),
          )
          .timeout(_timeout);
      if (r.statusCode != 200) return false;
      final body = (jsonDecode(r.body) as Map).cast<String, Object?>();
      config.accessToken = body['access_token'] as String? ?? '';
      config.refreshToken = body['refresh_token'] as String? ?? '';
      return config.accessToken.isNotEmpty;
    } catch (_) {
      return false;
    }
  }

  @override
  String get name => '自建服务器';

  @override
  Future<void> testConnection() async {
    await ensureAuthenticated();
    await _getJson('/api/v1/sync/pull', {'cursor': '${config.cursor}'});
  }

  @override
  Future<SyncResult> sync(SyncContext ctx) async {
    await ensureAuthenticated();

    // ---- push：本地全量变更（服务器逐条 LWW 仲裁） ----
    final localTasks = await ctx.taskRepo.listForSync();
    final localMemos = await ctx.memoRepo.listForSync();
    Map<String, Object?> taskChange(Task t) => {
          'entity': 'todo',
          'id': t.uuid,
          'operation': t.deleted ? 'delete' : 'upsert',
          'data': {
            'title': t.title,
            'description': t.description,
            'done': t.done,
            'priority': t.priority,
            'dueAt': t.dueAt?.millisecondsSinceEpoch,
            'createdAt': t.createdAt.millisecondsSinceEpoch,
          },
          'updatedAt': t.updatedAt.millisecondsSinceEpoch,
          'deletedAt': t.deletedAt?.millisecondsSinceEpoch,
          'deviceId': t.deviceId ?? ctx.deviceId,
        };
    Map<String, Object?> memoChange(Memo m) => {
          'entity': 'memo',
          'id': m.uuid,
          'operation': m.deleted ? 'delete' : 'upsert',
          'data': {
            'title': m.title,
            'content': m.content,
            'createdAt': m.createdAt.millisecondsSinceEpoch,
          },
          'updatedAt': m.updatedAt.millisecondsSinceEpoch,
          'deletedAt': m.deletedAt?.millisecondsSinceEpoch,
          'deviceId': m.deviceId ?? ctx.deviceId,
        };

    var changedLocal = false;
    var anyRejected = false;

    Future<void> pushBatch(List<Map<String, Object?>> changes) async {
      if (changes.isEmpty) return;
      final out = await _postJson('/api/v1/sync/push', {
        'deviceId': ctx.deviceId,
        'changes': changes,
      });
      for (final r in (out['results']! as List).cast<Map>()) {
        if (r['status'] == 'rejected') anyRejected = true;
      }
    }

    await pushBatch([
      for (final t in localTasks)
        if (t.uuid != null) taskChange(t),
    ]);
    await pushBatch([
      for (final m in localMemos)
        if (m.uuid != null) memoChange(m),
    ]);

    // ---- pull：cursor 增量，循环到 hasMore=false（带设备心跳） ----
    var changedRemote = 0;
    var guard = 0;
    while (true) {
      final page = await _getJson('/api/v1/sync/pull',
          {'cursor': '${config.cursor}', 'deviceId': ctx.deviceId});
      config.cursor = (page['cursor']! as num).toInt();

      for (final raw in (page['changes']! as List).cast<Map>()) {
        final c = raw.cast<String, Object?>();
        final applied = await _applyRemoteChange(ctx, c.cast());
        if (applied) changedRemote++;
      }
      if (page['hasMore'] != true) break;
      if (++guard > 100) break; // 防御性上限
    }

    return SyncResult.ok(changedLocal: changedRemote > 0, uploaded: true);
  }

  /// 远端变化落库（LWW 守卫：本地更新的离线修改不被覆盖）。
  Future<bool> _applyRemoteChange(
    SyncContext ctx,
    Map<String, Object?> c,
  ) async {
    final entity = c['entity'] as String;
    final uuid = c['id'] as String;
    final data = (c['data'] as Map?)?.cast<String, Object?>() ?? const {};
    final updatedAtMs = (c['updatedAt']! as num).toInt();
    final deletedAtMs = c['deletedAt'] as int?;
    final remoteUpdatedAt = DateTime.fromMillisecondsSinceEpoch(updatedAtMs);

    if (entity == 'todo') {
      final existing = await _taskByUuid(ctx.taskRepo, uuid);
      if (existing != null &&
          !SyncMerge.isNewer(remoteUpdatedAt, existing.updatedAt,
              c['deviceId'] as String? ?? '', existing.deviceId ?? '')) {
        return false;
      }
      final server = Task(
        uuid: uuid,
        title: data['title'] as String? ?? '',
        description: data['description'] as String? ?? '',
        done: data['done'] as bool? ?? false,
        priority: data['priority'] as int? ?? 0,
        dueAt: data['dueAt'] == null
            ? null
            : DateTime.fromMillisecondsSinceEpoch(data['dueAt']! as int),
        createdAt: data['createdAt'] == null
            ? remoteUpdatedAt
            : DateTime.fromMillisecondsSinceEpoch(data['createdAt']! as int),
        updatedAt: remoteUpdatedAt,
        deleted: deletedAtMs != null,
        deletedAt: deletedAtMs == null
            ? null
            : DateTime.fromMillisecondsSinceEpoch(deletedAtMs),
        deviceId: c['deviceId'] as String?,
      );
      await ctx.taskRepo.upsertByUuid(server);
      return true;
    } else {
      final existing = await _memoByUuid(ctx.memoRepo, uuid);
      if (existing != null &&
          !SyncMerge.isNewer(remoteUpdatedAt, existing.updatedAt,
              c['deviceId'] as String? ?? '', existing.deviceId ?? '')) {
        return false;
      }
      final server = Memo(
        uuid: uuid,
        title: data['title'] as String? ?? '',
        content: data['content'] as String? ?? '',
        createdAt: data['createdAt'] == null
            ? remoteUpdatedAt
            : DateTime.fromMillisecondsSinceEpoch(data['createdAt']! as int),
        updatedAt: remoteUpdatedAt,
        deleted: deletedAtMs != null,
        deletedAt: deletedAtMs == null
            ? null
            : DateTime.fromMillisecondsSinceEpoch(deletedAtMs),
        deviceId: c['deviceId'] as String?,
      );
      await ctx.memoRepo.upsertByUuid(server);
      return true;
    }
  }

  Future<Task?> _taskByUuid(TaskRepository repo, String uuid) async {
    for (final t in await repo.listForSync()) {
      if (t.uuid == uuid) return t;
    }
    return null;
  }

  Future<Memo?> _memoByUuid(MemoRepository repo, String uuid) async {
    for (final m in await repo.listForSync()) {
      if (m.uuid == uuid) return m;
    }
    return null;
  }
}

class ServerApiException implements Exception {
  ServerApiException(this.statusCode, this.body);

  final int statusCode;
  final String body;

  @override
  String toString() => 'ServerApiException($statusCode)';
}
