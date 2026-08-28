import 'dart:convert';

import 'package:flutter/material.dart';

import '../data/settings_store.dart';

/// 外观设置（SPD §14 General/Theme）：主题模式、主题色、AMOLED 纯黑。
/// 存本地 settings 表，不参与同步。
class ThemeSettingsModel extends ChangeNotifier {
  ThemeSettingsModel(this._store);

  static const _storageKey = 'appearance';

  final SettingsStore _store;

  ThemeMode themeMode = ThemeMode.system;
  String seedKey = ThemePresets.teal;
  bool amoledBlack = false;

  Future<void> load() async {
    final raw = await _store.read(_storageKey);
    if (raw == null) return;
    try {
      final json = (jsonDecode(raw) as Map).cast<String, Object?>();
      seedKey = json['seed'] as String? ?? seedKey;
      amoledBlack = json['amoled'] as bool? ?? false;
      switch (json['themeMode'] as String?) {
        case 'light':
          themeMode = ThemeMode.light;
        case 'dark':
          themeMode = ThemeMode.dark;
        default:
          themeMode = ThemeMode.system;
      }
      // 预设被删/拼错时回退默认色
      if (!ThemePresets.all.containsKey(seedKey)) {
        seedKey = ThemePresets.teal;
      }
    } catch (_) {
      // 配置损坏时用默认值。
    }
  }

  Color get seedColor => ThemePresets.all[seedKey] ?? ThemePresets.defaultColor;

  Future<void> setThemeMode(ThemeMode mode) async {
    if (themeMode == mode) return;
    themeMode = mode;
    await save();
  }

  Future<void> setSeed(String key) async {
    if (!ThemePresets.all.containsKey(key) || seedKey == key) return;
    seedKey = key;
    await save();
  }

  Future<void> setAmoledBlack(bool value) async {
    if (amoledBlack == value) return;
    amoledBlack = value;
    await save();
  }

  Future<void> save() async {
    await _store.write(_storageKey, toJson());
    notifyListeners();
  }

  Map<String, Object?> toJson() => {
        'themeMode': switch (themeMode) {
          ThemeMode.light => 'light',
          ThemeMode.dark => 'dark',
          _ => 'system',
        },
        'seed': seedKey,
        'amoled': amoledBlack,
      };
}

/// 内置主题色预设（SPD §5：命名不绑定厂商，这里同理不绑定品牌）。
class ThemePresets {
  ThemePresets._();

  static const teal = 'teal';
  static const indigo = 'indigo';
  static const sunset = 'sunset';
  static const sakura = 'sakura';
  static const forest = 'forest';
  static const sky = 'sky';
  static const graphite = 'graphite';
  static const amber = 'amber';

  static const defaultColor = Color(0xFF00696D);

  static final all = <String, Color>{
    teal: defaultColor,
    indigo: const Color(0xFF3F51B5),
    sunset: const Color(0xFFBF4E30),
    sakura: const Color(0xFFC2185B),
    forest: const Color(0xFF2E7D32),
    sky: const Color(0xFF0277BD),
    graphite: const Color(0xFF546E7A),
    amber: const Color(0xFFB26A00),
  };
}
