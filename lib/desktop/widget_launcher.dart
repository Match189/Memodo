import 'dart:async';
import 'dart:convert';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';

import 'win32_window_style.dart';
import 'widget_settings.dart';
import '../pages/widget_window_page.dart';

/// 主窗口侧的桌面小组件窗口管理：创建、关闭、位置/尺寸记忆、透明度，
/// 并跟随设置变化（SPD §11/§12）。小组件窗口本身是独立 Flutter 引擎，
/// 跑 [WidgetWindowApp]。
class WidgetLauncher {
  WidgetLauncher._();

  static const widgetSize = Size(300, 430);

  static int? _windowId;
  static DesktopWidgetSettingsModel? _settings;
  static Timer? _rectWatcher;
  static bool _lastEnabled = false;
  static bool _lastTopmost = false;
  static int _lastOpacity = 90;

  static void bind(DesktopWidgetSettingsModel settings) {
    _settings = settings;
    _lastEnabled = settings.enabled;
    _lastTopmost = settings.alwaysOnTop;
    _lastOpacity = settings.opacity;
    settings.addListener(_onSettingsChanged);
  }

  /// 只对真正影响窗口生命周期的字段做出反应，
  /// 避免位置采样写设置时又触发开/关窗口的级联。
  static Future<void> _onSettingsChanged() async {
    final s = _settings;
    if (s == null) return;
    if (s.enabled != _lastEnabled) {
      _lastEnabled = s.enabled;
      if (s.enabled) {
        await ensureOpen(alwaysOnTop: s.alwaysOnTop, opacity: s.opacity);
      } else {
        await close();
      }
      return;
    }
    if (s.alwaysOnTop != _lastTopmost) {
      _lastTopmost = s.alwaysOnTop;
      if (_windowId != null) await WidgetWindowNative.setTopmost(s.alwaysOnTop);
    }
    if (s.opacity != _lastOpacity) {
      _lastOpacity = s.opacity;
      if (_windowId != null) await WidgetWindowNative.setOpacity(s.opacity);
    }
  }

  static bool get isOpen => _windowId != null;

  /// 打开（或复用）小组件窗口；有记忆位置则恢复，否则放右下角。
  static Future<void> ensureOpen({
    required bool alwaysOnTop,
    required int opacity,
  }) async {
    if (_windowId != null) return;
    final s = _settings;
    final controller = await DesktopMultiWindow.createWindow(
      jsonEncode({'type': 'widget'}),
    );
    _windowId = controller.windowId;
    await controller.setTitle(widgetWindowTitle);

    final hasSaved = s != null && s.posX != null && s.width != null;
    if (hasSaved) {
      await controller.setFrame(
        Offset(s!.posX!.toDouble(), s.posY!.toDouble()) &
            Size(s.width!.toDouble(), s.height!.toDouble()),
      );
    } else {
      await controller.setFrame(
        const Offset(1200, 260) & widgetSize,
      );
    }
    await controller.show();

    // 去标题栏（保留缩放边框）、置顶、透明度、落位。
    await WidgetWindowNative.applyFramelessAndTopmost(alwaysOnTop: alwaysOnTop);
    if (!hasSaved) {
      await WidgetWindowNative.placeAtBottomRight(
        widgetSize.width.toInt(),
        widgetSize.height.toInt(),
      );
    }
    await WidgetWindowNative.setOpacity(opacity);
    _startRectWatcher();
  }

  /// 周期采样窗口矩形，变化时写回设置（关闭应用也能记住位置/尺寸）。
  static void _startRectWatcher() {
    _rectWatcher?.cancel();
    _rectWatcher = Timer.periodic(const Duration(seconds: 5), (_) {
      if (_windowId == null) return;
      final rect = WidgetWindowNative.getRect();
      final s = _settings;
      if (rect == null || s == null) return;
      // 与记忆值不同才写（save 内部也会去重）。
      unawaited(s.saveWindowRect(
        x: rect.x,
        y: rect.y,
        w: rect.w,
        h: rect.h,
      ));
    });
  }

  static Future<void> close() async {
    final id = _windowId;
    if (id == null) return;
    _windowId = null;
    _rectWatcher?.cancel();
    _rectWatcher = null;
    try {
      await WindowController.fromWindowId(id).close();
    } catch (_) {
      // 窗口可能已经自行关闭。
    }
  }

  /// 小组件窗口自己点了关闭：清引用但不重复关。
  static void forget(int windowId) {
    if (_windowId == windowId) {
      _windowId = null;
      _rectWatcher?.cancel();
      _rectWatcher = null;
    }
  }

  /// 设置里的置顶开关变化时调用。
  static Future<void> updateTopmost(bool topmost) async {
    if (_windowId == null) return;
    await WidgetWindowNative.setTopmost(topmost);
  }

  /// 设置里的透明度滑杆变化时调用。
  static Future<void> updateOpacity(int opacity) async {
    if (_windowId == null) return;
    await WidgetWindowNative.setOpacity(opacity);
  }
}
