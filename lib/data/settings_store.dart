import 'dart:convert';

import 'package:sqflite/sqflite.dart';

/// 基于 settings 表的键值存储（同步配置等本地持久化）。
class SettingsStore {
  SettingsStore(this._db);

  final Database _db;

  Future<String?> read(String key) async {
    final rows = await _db.query('settings',
        columns: ['value'], where: 'key = ?', whereArgs: [key], limit: 1);
    return rows.isEmpty ? null : rows.first['value'] as String;
  }

  Future<void> write(String key, Object? value) async {
    final encoded = value is String ? value : jsonEncode(value);
    await _db.insert(
      'settings',
      {'key': key, 'value': encoded},
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> remove(String key) =>
      _db.delete('settings', where: 'key = ?', whereArgs: [key]);
}
