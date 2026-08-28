// 往 Windows 端应用的本地设置库写一个键值配置。
// 用法（先关闭应用），stdin 传 JSON 信封：
//   echo '{"key":"sync.config","value":{...}}' | dart run tool/configure_sync.dart
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

  final input = await stdin.map(utf8.decode).join();
  final envelope = jsonDecode(input) as Map<String, dynamic>;
  final key = envelope['key'] as String?;
  final value = envelope['value'];
  if (key == null || value == null) {
    stderr.writeln('输入需要 {"key": "...", "value": {...}}');
    exit(2);
  }

  final db = await openDatabase(dbPath);
  await db.insert(
    'settings',
    {'key': key, 'value': jsonEncode(value)},
    conflictAlgorithm: ConflictAlgorithm.replace,
  );
  await db.close();
  stdout.writeln('OK：已写入 $key -> $dbPath');
}
