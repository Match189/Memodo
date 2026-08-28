import 'dart:convert';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';

import 'win32_window_style.dart';
import 'widget_settings.dart';
import '../pages/widget_window_page.dart';

/// 主窗口侧的桌面小组件窗口管理：创建、关闭、跟随设置变化。
/// 小组件窗口本身是独立 Flutter 引擎，跑 [WidgetWindowApp]。
class WidgetLauncher {
  WidgetLauncher._();

  static const widgetSize = Size(300, 430);

  static int? _windowId;
  static DesktopWidgetSettingsModel? _settings;

  static void bind(DesktopWidgetSettingsModel settings) {
    _settings = settings;
    settings.addListener(_onSettingsChanged);
  }

  static Future<void> _onSettingsChanged() async {
    final s = _settings;
    if (s == null) return;
    if (s.enabled) {
      await ensureOpen(alwaysOnTop: s.alwaysOnTop);
    } else {
      await close();
    }
  }

  static bool get isOpen => _windowId != null;

  /// 打开（或复用）小组件窗口。
  static Future<void> ensureOpen({required bool alwaysOnTop}) async {
    debugPrint('[widget] ensureOpen begin (current=$_windowId)');
    if (_windowId != null) return;
    final controller = await DesktopMultiWindow.createWindow(
      jsonEncode({'type': 'widget'}),
    );
    debugPrint('[widget] created id=${controller.windowId}');
    _windowId = controller.windowId;
    await controller.setTitle(widgetWindowTitle);
    await controller.setFrame(
      const Offset(1200, 260) & widgetSize,
    );
    await controller.show();
    debugPrint('[widget] shown, applying win32 style');
    // 系统标题栏与置顶交给 win32 加工（在原生窗口创建后调用）。
    await WidgetWindowNative.applyFramelessAndTopmost(alwaysOnTop: alwaysOnTop);
    await WidgetWindowNative.placeAtBottomRight(
      widgetSize.width.toInt(),
      widgetSize.height.toInt(),
    );
    debugPrint('[widget] win32 style applied');
  }

  static Future<void> close() async {
    final id = _windowId;
    if (id == null) return;
    _windowId = null;
    try {
      await WindowController.fromWindowId(id).close();
    } catch (_) {
      // 窗口可能已经自行关闭。
    }
  }

  /// 小组件窗口自己点了关闭：清引用但不重复关。
  static void forget(int windowId) {
    if (_windowId == windowId) _windowId = null;
  }

  /// 设置里的置顶开关变化时调用。
  static Future<void> updateTopmost(bool topmost) async {
    if (_windowId == null) return;
    await WidgetWindowNative.setTopmost(topmost);
  }
}
