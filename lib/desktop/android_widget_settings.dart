import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../data/settings_store.dart';

/// 安卓桌面小组件的显示设置（SPD §14：Max Items / Show Completed）。
class AndroidWidgetSettingsModel extends ChangeNotifier {
  AndroidWidgetSettingsModel(this._store);

  static const _storageKey = 'android.widget';

  final SettingsStore _store;

  int maxItems = 12;
  bool showCompleted = false;

  Future<void> load() async {
    final raw = await _store.read(_storageKey);
    if (raw == null) return;
    try {
      final json = (jsonDecode(raw) as Map).cast<String, Object?>();
      maxItems = (json['maxItems'] as int? ?? 12).clamp(4, 30);
      showCompleted = json['showCompleted'] as bool? ?? false;
    } catch (_) {
      // 配置损坏时用默认值。
    }
  }

  Future<void> setMaxItems(int value) async {
    final clamped = value.clamp(4, 30);
    if (maxItems == clamped) return;
    maxItems = clamped;
    await save();
  }

  Future<void> setShowCompleted(bool value) async {
    if (showCompleted == value) return;
    showCompleted = value;
    await save();
  }

  Future<void> save() async {
    await _store.write(_storageKey, toJson());
    notifyListeners();
  }

  Map<String, Object?> toJson() =>
      {'maxItems': maxItems, 'showCompleted': showCompleted};
}
