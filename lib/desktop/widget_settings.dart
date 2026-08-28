import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../data/settings_store.dart';

/// Windows 桌面小组件的本地设置（不参与同步）。
class DesktopWidgetSettingsModel extends ChangeNotifier {
  DesktopWidgetSettingsModel(this._store);

  static const _storageKey = 'desktop.widget';

  final SettingsStore _store;

  bool enabled = false;
  bool alwaysOnTop = true;

  Future<void> load() async {
    final raw = await _store.read(_storageKey);
    if (raw == null) return;
    try {
      final json = (jsonDecode(raw) as Map).cast<String, Object?>();
      enabled = json['enabled'] as bool? ?? false;
      alwaysOnTop = json['alwaysOnTop'] as bool? ?? true;
    } catch (_) {
      // 配置损坏时用默认值。
    }
  }

  Future<void> setEnabled(bool value) async {
    if (enabled == value) return;
    enabled = value;
    await save();
  }

  Future<void> setAlwaysOnTop(bool value) async {
    alwaysOnTop = value;
    await save();
  }

  Future<void> save() async {
    await _store.write(_storageKey, toJson());
    notifyListeners();
  }

  Map<String, Object?> toJson() =>
      {'enabled': enabled, 'alwaysOnTop': alwaysOnTop};
}
