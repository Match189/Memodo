import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../data/settings_store.dart';
import 'sync_transport.dart';
import 'transports/oss_transport.dart';
import 'transports/server_transport.dart';
import 'transports/webdav_transport.dart';

/// 同步通道种类。
enum SyncChannel {
  none('不同步'),
  webdav('WebDAV（坚果云等）'),
  oss('对象存储（阿里云 OSS / 腾讯云 COS）'),
  server('自建服务器');

  const SyncChannel(this.label);
  final String label;

  static SyncChannel from(String? name) => SyncChannel.values
      .firstWhere((c) => c.name == name, orElse: () => SyncChannel.none);
}

/// 各通道的连接配置 + 加密口令 + 自动同步开关。
/// 整体作为 JSON 存在本地 settings 表里（不进同步快照）。
class SyncSettingsModel extends ChangeNotifier {
  SyncSettingsModel(this._store);

  static const _storageKey = 'sync.config';

  final SettingsStore _store;

  SyncChannel channel = SyncChannel.none;
  bool autoSync = true;

  /// 快照加密口令；留空表示明文上传。两端必须一致。
  String passphrase = '';

  final webdav = WebdavConfig();
  final oss = OssConfig();
  final server = ServerConfig();

  DateTime? lastSyncAt;

  bool get configured => channel != SyncChannel.none;

  Future<void> load() async {
    final raw = await _store.read(_storageKey);
    if (raw == null) return;
    try {
      final json = (jsonDecode(raw) as Map).cast<String, Object?>();
      channel = SyncChannel.from(json['channel'] as String?);
      autoSync = json['autoSync'] as bool? ?? true;
      passphrase = json['passphrase'] as String? ?? '';
      lastSyncAt = json['lastSyncAt'] == null
          ? null
          : DateTime.fromMillisecondsSinceEpoch(json['lastSyncAt'] as int);
      webdav.fillFrom((json['webdav'] as Map?)?.cast<String, Object?>());
      oss.fillFrom((json['oss'] as Map?)?.cast<String, Object?>());
      server.fillFrom((json['server'] as Map?)?.cast<String, Object?>());
    } catch (_) {
      // 配置损坏时回退默认值，不打断启动。
    }
  }

  Future<void> save() async {
    await _store.write(_storageKey, toJson());
    notifyListeners();
  }

  Map<String, Object?> toJson() => {
        'channel': channel.name,
        'autoSync': autoSync,
        'passphrase': passphrase,
        'lastSyncAt': lastSyncAt?.millisecondsSinceEpoch,
        'webdav': webdav.toJson(),
        'oss': oss.toJson(),
        'server': server.toJson(),
      };

  /// 按当前选择构造传输实现；未配置/必填项缺失返回 null。
  SyncTransport? buildTransport() {
    switch (channel) {
      case SyncChannel.none:
        return null;
      case SyncChannel.webdav:
        if (webdav.baseUrl.trim().isEmpty ||
            webdav.username.trim().isEmpty ||
            webdav.password.isEmpty) {
          return null;
        }
        return WebdavTransport(
          baseUrl: webdav.baseUrl.trim(),
          folder: webdav.folder.trim().isEmpty ? 'todolist' : webdav.folder.trim(),
          username: webdav.username.trim(),
          password: webdav.password,
        );
      case SyncChannel.oss:
        if (oss.endpoint.trim().isEmpty ||
            oss.bucket.trim().isEmpty ||
            oss.accessKeyId.trim().isEmpty ||
            oss.accessKeySecret.isEmpty) {
          return null;
        }
        return OssTransport(
          endpoint: oss.endpoint.trim(),
          bucket: oss.bucket.trim(),
          accessKeyId: oss.accessKeyId.trim(),
          accessKeySecret: oss.accessKeySecret,
          objectKey: oss.objectKey.trim().isEmpty
              ? 'todolist/snapshot.json'
              : oss.objectKey.trim(),
        );
      case SyncChannel.server:
        if (server.baseUrl.trim().isEmpty || server.token.isEmpty) {
          return null;
        }
        return ServerTransport(
          baseUrl: server.baseUrl.trim(),
          token: server.token,
        );
    }
  }
}

class WebdavConfig {
  /// 服务地址，坚果云填 https://dav.jianguoyun.com/dav/
  String baseUrl = '';
  String folder = 'todolist';
  String username = '';

  /// 坚果云这里填「应用密码」，不是登录密码。
  String password = '';

  Map<String, Object?> toJson() =>
      {'baseUrl': baseUrl, 'folder': folder, 'username': username, 'password': password};

  void fillFrom(Map<String, Object?>? json) {
    if (json == null) return;
    baseUrl = json['baseUrl'] as String? ?? baseUrl;
    folder = json['folder'] as String? ?? folder;
    username = json['username'] as String? ?? username;
    password = json['password'] as String? ?? password;
  }
}

class OssConfig {
  String endpoint = '';
  String bucket = '';
  String accessKeyId = '';
  String accessKeySecret = '';
  String objectKey = 'todolist/snapshot.json';

  Map<String, Object?> toJson() => {
        'endpoint': endpoint,
        'bucket': bucket,
        'accessKeyId': accessKeyId,
        'accessKeySecret': accessKeySecret,
        'objectKey': objectKey,
      };

  void fillFrom(Map<String, Object?>? json) {
    if (json == null) return;
    endpoint = json['endpoint'] as String? ?? endpoint;
    bucket = json['bucket'] as String? ?? bucket;
    accessKeyId = json['accessKeyId'] as String? ?? accessKeyId;
    accessKeySecret = json['accessKeySecret'] as String? ?? accessKeySecret;
    objectKey = json['objectKey'] as String? ?? objectKey;
  }
}

class ServerConfig {
  String baseUrl = '';
  String token = '';

  Map<String, Object?> toJson() => {'baseUrl': baseUrl, 'token': token};

  void fillFrom(Map<String, Object?>? json) {
    if (json == null) return;
    baseUrl = json['baseUrl'] as String? ?? baseUrl;
    token = json['token'] as String? ?? token;
  }
}
