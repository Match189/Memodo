import 'dart:convert';

import 'package:cryptography/cryptography.dart';

import '../models/memo.dart';
import '../models/task.dart';

/// 同步快照：两端全量数据（含软删除墓碑），按 uuid 合并。
class Snapshot {
  Snapshot({
    required this.tasks,
    required this.memos,
    DateTime? exportedAt,
  }) : exportedAt = exportedAt ?? DateTime.now();

  static const format = 1;

  final List<Task> tasks;
  final List<Memo> memos;
  final DateTime exportedAt;

  Map<String, Object?> toJson() => {
        'format': format,
        'exportedAt': exportedAt.millisecondsSinceEpoch,
        'tasks': [for (final t in tasks) _taskJson(t)],
        'memos': [for (final m in memos) _memoJson(m)],
      };

  static Map<String, Object?> _taskJson(Task t) => {
        'uuid': t.uuid,
        'title': t.title,
        'done': t.done,
        'createdAt': t.createdAt.millisecondsSinceEpoch,
        'updatedAt': t.updatedAt.millisecondsSinceEpoch,
        'deleted': t.deleted,
      };

  static Map<String, Object?> _memoJson(Memo m) => {
        'uuid': m.uuid,
        'title': m.title,
        'content': m.content,
        'createdAt': m.createdAt.millisecondsSinceEpoch,
        'updatedAt': m.updatedAt.millisecondsSinceEpoch,
        'deleted': m.deleted,
      };

  factory Snapshot.fromJson(Map<String, Object?> json) {
    if (json['format'] != format) {
      throw const FormatException('快照版本不兼容，请升级应用');
    }
    return Snapshot(
      exportedAt:
          DateTime.fromMillisecondsSinceEpoch(json['exportedAt'] as int? ?? 0),
      tasks: [
        for (final t in (json['tasks'] as List? ?? const []))
          _taskFromJson((t as Map).cast<String, Object?>())
      ],
      memos: [
        for (final m in (json['memos'] as List? ?? const []))
          _memoFromJson((m as Map).cast<String, Object?>())
      ],
    );
  }

  static Task _taskFromJson(Map<String, Object?> j) => Task(
        uuid: j['uuid'] as String?,
        title: j['title'] as String? ?? '',
        done: j['done'] as bool? ?? false,
        createdAt:
            DateTime.fromMillisecondsSinceEpoch(j['createdAt'] as int? ?? 0),
        updatedAt:
            DateTime.fromMillisecondsSinceEpoch(j['updatedAt'] as int? ?? 0),
        deleted: j['deleted'] as bool? ?? false,
      );

  static Memo _memoFromJson(Map<String, Object?> j) => Memo(
        uuid: j['uuid'] as String?,
        title: j['title'] as String? ?? '',
        content: j['content'] as String? ?? '',
        createdAt:
            DateTime.fromMillisecondsSinceEpoch(j['createdAt'] as int? ?? 0),
        updatedAt:
            DateTime.fromMillisecondsSinceEpoch(j['updatedAt'] as int? ?? 0),
        deleted: j['deleted'] as bool? ?? false,
      );
}

/// 快照正文的编解码：明文 JSON，或带口令的 AES-GCM 加密。
class SnapshotCodec {
  static const _encryptPrefix = 'TODOLIST-ENC1:';

  final String? passphrase;

  SnapshotCodec(this.passphrase);

  bool get encrypted => (passphrase ?? '').isNotEmpty;

  Future<SecretKey> _deriveKey(String pass) {
    final pbkdf2 = Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 50000,
      bits: 256,
    );
    // 盐固定为应用常量：两端无需交换盐值即可互通；安全性由口令强度保证。
    return pbkdf2.deriveKeyFromPassword(
      password: pass,
      nonce: utf8.encode('todolist-app'),
    );
  }

  Future<String> encode(Snapshot snapshot) async {
    final json = utf8.encode(jsonEncode(snapshot.toJson()));
    if (!encrypted) return utf8.decode(json);
    final algorithm = AesGcm.with256bits();
    final box = await algorithm.encrypt(
      json,
      secretKey: await _deriveKey(passphrase!),
    );
    return _encryptPrefix + base64Encode(box.concatenation());
  }

  /// 解码云端取回的正文。远端加密而本地无口令（或口令错）会抛 FormatException。
  Future<Snapshot> decode(String text) async {
    if (text.startsWith(_encryptPrefix)) {
      if (!encrypted) {
        throw const FormatException('远端快照已加密，请先在设置中填写同步口令');
      }
      final algorithm = AesGcm.with256bits();
      final boxBytes = base64Decode(text.substring(_encryptPrefix.length));
      final List<int> clear;
      try {
        clear = await algorithm.decrypt(
          SecretBox.fromConcatenation(boxBytes, nonceLength: 12, macLength: 16),
          secretKey: await _deriveKey(passphrase!),
        );
      } on SecretBoxAuthenticationError {
        throw const FormatException('解密失败：口令错误或快照已损坏');
      }
      return Snapshot.fromJson(
          (jsonDecode(utf8.decode(clear)) as Map).cast<String, Object?>());
    }
    return Snapshot.fromJson(
        (jsonDecode(text) as Map).cast<String, Object?>());
  }
}
