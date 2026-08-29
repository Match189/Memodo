import 'dart:async';
import 'dart:convert';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';

import 'widget_settings.dart';
import '../pages/widget_window_page.dart';

/// Windows 桌面小组件窗口管理（SPD §11/§12 + 用户确认的语义）。
///
/// - 单窗口多布局：single=按内容类型显示；dual=窗口内分两栏（待办+备忘）
/// - 预创建（preCreate=true）：应用启动时即起子进程，藏在任务栏后，
///   切换 enabled/layout 时只需 show()/hide()，无 Flutter 引擎启动延迟
class WidgetLauncher {
  WidgetLauncher._();

  static const defaultSize = Size(360, 480);

  static int? _windowId;
  static bool _opening = false;
  static bool _preCreated = false;
  static bool _isShown = false;
  static DesktopWidgetSettingsModel? _settings;
  static bool _lastEnabled = false;
  static bool _lastPreCreate = false;
  static WidgetLayout _lastLayout = WidgetLayout.single;

  static void bind(DesktopWidgetSettingsModel settings) {
    _settings = settings;
    _lastEnabled = settings.enabled;
    _lastLayout = settings.layout;
    _lastPreCreate = settings.preCreate;
    settings.addListener(_onSettingsChanged);
  }

  static Future<void> _onSettingsChanged() async {
    final s = _settings;
    if (s == null) return;
    if (s.enabled != _lastEnabled) {
      _lastEnabled = s.enabled;
      if (s.enabled) {
        await showWindow();
      } else {
        await hideWindow();
      }
      return;
    }
    if (s.layout != _lastLayout) {
      _lastLayout = s.layout;
      // 布局变化时向子进程广播，让它重画（无需重启进程）
      try {
        await DesktopMultiWindow.invokeMethod(_windowId!, 'layoutChanged');
      } catch (_) {}
    }
    if (s.preCreate != _lastPreCreate) {
      _lastPreCreate = s.preCreate;
      if (s.preCreate && !_preCreated) {
        await preCreateHidden();
      } else if (!s.preCreate && !_isShown) {
        // 关掉预创建 = 关闭已有窗口
        await destroyWindow();
      }
    }
  }

  /// 应用启动时调用：按设置预创建/显示窗口。
  static Future<void> boot() async {
    final s = _settings;
    if (s == null) return;
    if (s.preCreate || s.enabled) {
      // 预创建：立即起子窗口但**保持隐藏**，用户打开时只 show() 不再起进程
      if (s.preCreate) {
        await preCreateHidden();
      }
      if (s.enabled) {
        await showWindow();
      }
    }
  }

  static bool get isOpen => _isShown;

  static Future<void> showWindow() async {
    if (_opening) return;
    _opening = true;
    try {
      if (_windowId == null) {
        await _createWindow();
      }
      if (_windowId != null) {
        await WindowController.fromWindowId(_windowId!).show();
        _isShown = true;
      }
    } finally {
      _opening = false;
    }
  }

  static Future<void> hideWindow() async {
    if (_windowId == null) return;
    _isShown = false;
    try {
      await WindowController.fromWindowId(_windowId!).hide();
    } catch (_) {}
  }

  static Future<void> destroyWindow() async {
    final id = _windowId;
    _windowId = null;
    _preCreated = false;
    _isShown = false;
    if (id == null) return;
    try {
      await WindowController.fromWindowId(id).close();
    } catch (_) {}
  }

  /// 启动一个 Flutter 引擎子进程（窗口先藏起来），供后续秒开。
  static Future<void> preCreateHidden() async {
    if (_windowId != null) return;
    if (_opening) return;
    _opening = true;
    try {
      await _createWindow();
      if (_windowId != null) {
        // 让子窗口自套样式后立即隐藏
        try {
          await DesktopMultiWindow.invokeMethod(_windowId!, 'initHidden');
          await WindowController.fromWindowId(_windowId!).hide();
        } catch (_) {}
        _preCreated = true;
      }
    } finally {
      _opening = false;
    }
  }

  static Future<void> _createWindow() async {
    final s = _settings;
    if (s == null) return;
    try {
      // 自愈：清理遗留子窗口
      try {
        final ids = await DesktopMultiWindow.getAllSubWindowIds()
            .timeout(const Duration(seconds: 3));
        for (final id in ids) {
          if (_windowId == null) {
            _windowId = id;
          } else {
            try {
              await WindowController.fromWindowId(id).close();
            } catch (_) {}
          }
        }
      } catch (_) {}

      if (_windowId == null) {
        // 加超时：主进程不能等子进程太久，否则整个应用被拖死。
        final controller = await DesktopMultiWindow.createWindow(jsonEncode({
          'type': 'widget',
          'kind': s.layout == WidgetLayout.dual ? 'dual' : 'todo',
        })).timeout(const Duration(seconds: 6));
        _windowId = controller.windowId;
        await controller
          ..setTitle(widgetWindowTitle)
          ..setFrame(const Offset(1200, 260) & defaultSize)
          ..show()
          .timeout(const Duration(seconds: 3));
      }
    } catch (e) {
      debugPrint('[launcher] createWindow failed: $e');
      // 失败时彻底放弃此次创建，避免半状态阻塞后续 enable。
      _windowId = null;
    }
  }

  /// 小组件窗口自己点了关闭：清引用但不重复关。
  static void forget(int windowId) {
    if (_windowId == windowId) {
      _windowId = null;
      _preCreated = false;
      _isShown = false;
    }
  }

  /// 广播材质/透明度给子进程（由子进程自己套样式）。
  static Future<void> updateSurface({
    required WidgetMaterial material,
    required int opacity,
  }) async {
    final id = _windowId;
    if (id == null) return;
    try {
      await DesktopMultiWindow.invokeMethod(id, 'applySurface', {
        'acrylic': material == WidgetMaterial.acrylic,
        'opacity': opacity,
      });
    } catch (_) {}
  }

  static Future<void> updateTopmost(bool topmost) async {
    final id = _windowId;
    if (id == null) return;
    try {
      await DesktopMultiWindow.invokeMethod(id, 'setTopmost', topmost);
    } catch (_) {}
  }

  static Future<void> updateAttach(bool attach) async {
    final id = _windowId;
    if (id == null) return;
    try {
      await DesktopMultiWindow.invokeMethod(id, attach ? 'attach' : 'detach');
    } catch (_) {}
  }
}
