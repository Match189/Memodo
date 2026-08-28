import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../data/settings_store.dart';

/// Windows 桌面小组件的本地设置（不参与同步）。SPD §14。
class DesktopWidgetSettingsModel extends ChangeNotifier {
  DesktopWidgetSettingsModel(this._store);

  static const _storageKey = 'desktop.widget';

  final SettingsStore _store;

  bool enabled = false;

  /// 置顶是**可选项**，默认关（SPD 禁止事项 #7：不得强制 Always On Top）。
  bool alwaysOnTop = false;

  /// 卡片不透明度百分比（100 = 完全不透明）。
  int opacity = 90;

  /// 锁定位置后禁止拖动。
  bool lockPosition = false;

  /// 桌面层模式（SPD §13 V2）：把卡片 SetParent 到 WorkerW，成为壁纸层。
  /// 不稳定/失败时自动回退普通窗口模式。
  bool attachToDesktop = false;

  /// 上次窗口位置与尺寸（恢复用；由主进程周期性采样保存）。
  int? posX;
  int? posY;
  int? width;
  int? height;

  Future<void> load() async {
    final raw = await _store.read(_storageKey);
    if (raw == null) return;
    try {
      final json = (jsonDecode(raw) as Map).cast<String, Object?>();
      enabled = json['enabled'] as bool? ?? false;
      alwaysOnTop = json['alwaysOnTop'] as bool? ?? false;
      opacity = (json['opacity'] as int? ?? 90).clamp(30, 100);
      lockPosition = json['lockPosition'] as bool? ?? false;
      attachToDesktop = json['attachToDesktop'] as bool? ?? false;
      posX = json['posX'] as int?;
      posY = json['posY'] as int?;
      width = json['width'] as int?;
      height = json['height'] as int?;
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
    if (alwaysOnTop == value) return;
    alwaysOnTop = value;
    await save();
  }

  Future<void> setOpacity(int value) async {
    final clamped = value.clamp(30, 100);
    if (opacity == clamped) return;
    opacity = clamped;
    await save();
  }

  Future<void> setLockPosition(bool value) async {
    if (lockPosition == value) return;
    lockPosition = value;
    await save();
  }

  Future<void> setAttachToDesktop(bool value) async {
    if (attachToDesktop == value) return;
    attachToDesktop = value;
    await save();
  }

  Future<void> saveWindowRect({
    required int x,
    required int y,
    required int w,
    required int h,
  }) async {
    if (posX == x && posY == y && width == w && height == h) return;
    posX = x;
    posY = y;
    width = w;
    height = h;
    await save();
  }

  Future<void> save() async {
    await _store.write(_storageKey, toJson());
    notifyListeners();
  }

  Map<String, Object?> toJson() => {
        'enabled': enabled,
        'alwaysOnTop': alwaysOnTop,
        'opacity': opacity,
        'lockPosition': lockPosition,
        'attachToDesktop': attachToDesktop,
        'posX': posX,
        'posY': posY,
        'width': width,
        'height': height,
      };
}
