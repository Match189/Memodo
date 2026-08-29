import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../data/settings_store.dart';

/// 小组件显示布局：
/// - [single]：单卡片（按内容类型显示待办或备忘）
/// - [dual]：单窗口分两栏（待办在左、备忘在右，同时可见）
enum WidgetLayout { single, dual }

/// 小组件窗口材质（Windows 窗口合成）。
enum WidgetMaterial {
  solid('不透明'),
  acrylic('毛玻璃'),
  transparent('透明');

  const WidgetMaterial(this.label);
  final String label;

  static WidgetMaterial from(String? name) => WidgetMaterial.values
      .firstWhere((m) => m.name == name, orElse: () => WidgetMaterial.solid);
}

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

  /// 窗口材质：不透明 / 毛玻璃（acrylic 模糊）/ 透明。
  WidgetMaterial material = WidgetMaterial.solid;

  /// 布局：单卡片（待办+备忘合并）或双卡片（待办、备忘两个独立窗口）。
  WidgetLayout layout = WidgetLayout.single;

  /// 锁定位置后禁止拖动。
  bool lockPosition = false;

  /// 桌面层模式（SPD §13 V2）：把卡片 SetParent 到 WorkerW，成为壁纸层。
  /// 不稳定/失败时自动回退普通窗口模式。
  bool attachToDesktop = false;

  /// 开机自启（Windows：HKCU Run 注册表，无需管理员）。
  bool autostart = false;

  /// 关闭主窗口时最小化到托盘（true=不退出，托盘图标可恢复）。
  bool closeToTray = true;

  /// 启动时是否预创建子窗口（藏在任务栏后，秒开）。当前默认关：
  /// desktop_multi_window 0.2.x 预创建会在主窗口标题切换时把主进程
  /// dispose 退掉（已实测复现）。如需秒开可启用，但接受偶发退出风险。
  bool preCreate = false;

  /// 卡片样式：classic=经典列表（当前模式）；board=图钉板样式。
  String cardStyle = 'classic';

  /// 图钉板主题（软木板/毛玻璃），应用与小组件共用。
  String boardThemeId = 'cork';

  /// 待办（/单卡片）窗口上次位置与尺寸。
  int? posX;
  int? posY;
  int? width;
  int? height;

  /// 备忘窗口（双卡片布局）上次位置与尺寸。
  int? memoPosX;
  int? memoPosY;
  int? memoWidth;
  int? memoHeight;

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
      autostart = json['autostart'] as bool? ?? false;
      closeToTray = json['closeToTray'] as bool? ?? true;
      preCreate = json['preCreate'] as bool? ?? true;
      cardStyle = json['cardStyle'] as String? ?? cardStyle;
      boardThemeId = json['boardThemeId'] as String? ?? boardThemeId;
      material = WidgetMaterial.from(json['material'] as String?);
      layout = json['layout'] == 'dual'
          ? WidgetLayout.dual
          : WidgetLayout.single;
      posX = json['posX'] as int?;
      posY = json['posY'] as int?;
      width = json['width'] as int?;
      height = json['height'] as int?;
      memoPosX = json['memoPosX'] as int?;
      memoPosY = json['memoPosY'] as int?;
      memoWidth = json['memoWidth'] as int?;
      memoHeight = json['memoHeight'] as int?;
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

  Future<void> setMaterial(WidgetMaterial value) async {
    if (material == value) return;
    material = value;
    await save();
  }

  Future<void> setLayout(WidgetLayout value) async {
    if (layout == value) return;
    layout = value;
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

  Future<void> setAutostart(bool value) async {
    if (autostart == value) return;
    autostart = value;
    await save();
  }

  Future<void> setCloseToTray(bool value) async {
    if (closeToTray == value) return;
    closeToTray = value;
    await save();
  }

  /// 主进程广播设置变化后调用：重新加载并通知本引擎的 UI。
  Future<void> reload() async {
    await load();
    notifyListeners();
  }

  Future<void> setPreCreate(bool value) async {
    if (preCreate == value) return;
    preCreate = value;
    await save();
  }

  Future<void> setCardStyle(String value) async {
    if (cardStyle == value) return;
    cardStyle = value;
    await save();
  }

  Future<void> setBoardThemeId(String value) async {
    if (boardThemeId == value) return;
    boardThemeId = value;
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

  Future<void> saveMemoWindowRect({
    required int x,
    required int y,
    required int w,
    required int h,
  }) async {
    if (memoPosX == x && memoPosY == y && memoWidth == w && memoHeight == h) {
      return;
    }
    memoPosX = x;
    memoPosY = y;
    memoWidth = w;
    memoHeight = h;
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
        'material': material.name,
        'layout': layout.name,
        'lockPosition': lockPosition,
        'attachToDesktop': attachToDesktop,
        'autostart': autostart,
        'closeToTray': closeToTray,
        'preCreate': preCreate,
        'cardStyle': cardStyle,
        'boardThemeId': boardThemeId,
        'posX': posX,
        'posY': posY,
        'width': width,
        'height': height,
        'memoPosX': memoPosX,
        'memoPosY': memoPosY,
        'memoWidth': memoWidth,
        'memoHeight': memoHeight,
      };
}
