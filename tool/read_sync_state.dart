// 查看 Windows 端应用当前保存的同步配置与状态。
// 用法：dart run tool/read_sync_state.dart
import 'dart:convert';
import 'dart:io';

import 'package:sqflite_common_ffi/sqflite_ffi.dart';

Future<void> main() async {
  sqfliteFfiInit();
  databaseFactory = databaseFactoryFfi;

  final appData = Platform.environment['APPDATA'];
  if (appData == null) {
    stderr.writeln('只在 Windows 上可用（依赖 APPDATA）');
    exit(2);
  }
  final dbPath = '$appData\\com.example\\todolist\\todolist.db';
  final db = await openDatabase(dbPath);
  final rows = await db.query('settings', where: "key = 'sync.config'");
  final tasks = await db.query('tasks', where: 'deleted = 0');
  final memos = await db.query('memos', where: 'deleted = 0');
  await db.close();

  if (rows.isEmpty) {
    stdout.writeln('尚未写入同步配置');
    return;
  }
  final config = jsonDecode(rows.first['value'] as String) as Map<String, dynamic>;
  stdout.writeln('通道=${config['channel']} 自动同步=${config['autoSync']}');
  final last = config['lastSyncAt'];
  stdout.writeln(last == null
      ? '上次同步：从未成功'
      : '上次同步：${DateTime.fromMillisecondsSinceEpoch(last as int)}');
  stdout.writeln('本地数据：${tasks.length} 条待办，${memos.length} 条备忘');
}
