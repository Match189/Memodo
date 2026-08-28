// 手动把本地数据库打包成快照，推送到已配置的同步通道（云端修复/手动备份）。
// 注意：保持纯 Dart（不 import flutter 依赖的库），才能用 dart run 直接跑。
// 用法：dart run tool/push_snapshot.dart [--dry-run]
import 'dart:convert';
import 'dart:io';

import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import '../lib/models/memo.dart';
import '../lib/models/task.dart';
import '../lib/sync/snapshot_codec.dart';
import '../lib/sync/sync_transport.dart';
import '../lib/sync/transports/oss_transport.dart';
import '../lib/sync/transports/server_transport.dart';
import '../lib/sync/transports/webdav_transport.dart';

Future<void> main(List<String> args) async {
  sqfliteFfiInit();
  databaseFactory = databaseFactoryFfi;

  final appData = Platform.environment['APPDATA'];
  if (appData == null) {
    stderr.writeln('只在 Windows 上可用');
    exit(2);
  }
  final dbPath = '$appData\\com.example\\todolist\\todolist.db';
  final db = await databaseFactoryFfi.openDatabase(dbPath);

  // 读同步配置（结构与 SyncSettingsModel 一致，这里手动解析避免 Flutter 依赖）
  final cfgRows = await db.query('settings',
      columns: ['value'], where: "key = 'sync.config'", limit: 1);
  if (cfgRows.isEmpty) {
    stderr.writeln('尚未配置同步');
    exit(2);
  }
  final cfg =
      (jsonDecode(cfgRows.first['value'] as String) as Map).cast<String, Object?>();
  final channel = cfg['channel'] as String? ?? 'none';
  final passphrase = (cfg['passphrase'] as String? ?? '').trim();

  final taskRows = await db.query('tasks');
  final memoRows = await db.query('memos');
  final snapshot = Snapshot(
    tasks: [for (final r in taskRows) Task.fromMap(r)],
    memos: [for (final r in memoRows) Memo.fromMap(r)],
  );
  final body = await SnapshotCodec(
    passphrase.isEmpty ? null : passphrase,
  ).encode(snapshot);
  stdout.writeln(
      '本地数据：${taskRows.length} 条待办（含墓碑），${memoRows.length} 条备忘；快照 ${body.length} 字节');

  if (args.contains('--dry-run')) {
    stdout.writeln('--dry-run：预览前 160 字符');
    stdout.writeln(body.substring(0, body.length < 160 ? body.length : 160));
    await db.close();
    return;
  }

  Future<void> upload(SyncTransport uploader) async {
    await uploader.testConnection();
    await uploader.uploadSnapshot(body);
  }

  switch (channel) {
    case 'webdav':
      final c = (cfg['webdav'] as Map?)?.cast<String, Object?>() ?? const {};
      await upload(WebdavTransport(
        baseUrl: (c['baseUrl'] as String? ?? '').trim(),
        folder: ((c['folder'] as String? ?? 'todolist').trim().isEmpty)
            ? 'todolist'
            : (c['folder'] as String).trim(),
        username: (c['username'] as String? ?? '').trim(),
        password: c['password'] as String? ?? '',
      ));
    case 'oss':
      final c = (cfg['oss'] as Map?)?.cast<String, Object?>() ?? const {};
      await upload(OssTransport(
        endpoint: (c['endpoint'] as String? ?? '').trim(),
        bucket: (c['bucket'] as String? ?? '').trim(),
        accessKeyId: (c['accessKeyId'] as String? ?? '').trim(),
        accessKeySecret: c['accessKeySecret'] as String? ?? '',
        objectKey: ((c['objectKey'] as String? ?? 'todolist/snapshot.json')
                    .trim()
                    .isEmpty)
            ? 'todolist/snapshot.json'
            : (c['objectKey'] as String).trim(),
      ));
    case 'server':
      final c = (cfg['server'] as Map?)?.cast<String, Object?>() ?? const {};
      await upload(ServerTransport(
        baseUrl: (c['baseUrl'] as String? ?? '').trim(),
        token: c['token'] as String? ?? '',
      ));
    default:
      stderr.writeln('当前通道为 none，未上传');
      await db.close();
      exit(2);
  }
  stdout.writeln('OK：快照已上传（通道=$channel）');
  await db.close();
}
