import 'dart:async';
import 'dart:convert';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/foundation.dart';
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
      // ⚠️ 必须 unawaited：不阻塞主线程（之前 await 拖死主线程 = 转圈）
      unawaited(_dispatchLayout());
    }
    if (s.preCreate != _lastPreCreate) {
      _lastPreCreate = s.preCreate;
      if (s.preCreate && !_preCreated) {
        await preCreateHidden();
      } else if (!s.preCreate && !_isShown) {
        await destroyWindow();
      }
    }
  }

  static Future<void> _dispatchLayout() async {
    final id = _windowId;
    if (id == null) return;
    try {
      await DesktopMultiWindow.invokeMethod(id, 'layoutChanged');
    } catch (_) {}
  }

  /// 应用启动时调用：按设置预创建/显示窗口。
  static Future<void> boot() async {
    final s = _settings;
    if (s == null) return;
    if (s.preCreate) {
      await preCreateHidden();
    }
    if (s.enabled) {
      await showWindow();
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
        // 先 show，再异步推设置（不等子进程自套样式，避免拖死主线程）
        try {
          await WindowController.fromWindowId(_windowId!).show()
              .timeout(const Duration(seconds: 3));
        } catch (_) {}
        _isShown = true;
        unawaited(_dispatchInitialSettings());
      }
    } finally {
      _opening = false;
    }
  }

  static Future<void> _dispatchInitialSettings() async {
    final s = _settings;
    final id = _windowId;
    if (s == null || id == null) return;
    try {
      await DesktopMultiWindow.invokeMethod(id, 'applySurface', {
        'acrylic': s.material == WidgetMaterial.acrylic,
        'opacity': s.opacity,
      });
    } catch (_) {}
  }

  static Future<void> hideWindow() async {
    if (_windowId == null) return;
    _isShown = false;
    try {
      await WindowController.fromWindowId(_windowId!).hide()
          .timeout(const Duration(seconds: 3));
    } catch (_) {}
  }

  static Future<void> destroyWindow() async {
    final id = _windowId;
    _windowId = null;
    _preCreated = false;
    _isShown = false;
    if (id == null) return;
    try {
      await WindowController.fromWindowId(id).close()
          .timeout(const Duration(seconds: 3));
    } catch (_) {}
  }

  static Future<void> preCreateHidden() async {
    if (_windowId != null) return;
    if (_opening) return;
    _opening = true;
    try {
      await _createWindow();
      if (_windowId != null) {
        try {
          await DesktopMultiWindow.invokeMethod(_windowId!, 'initHidden')
              .timeout(const Duration(seconds: 3));
          await WindowController.fromWindowId(_windowId!).hide()
              .timeout(const Duration(seconds: 3));
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
        final controller = await DesktopMultiWindow
            .createWindow(jsonEncode({
              'type': 'widget',
              'kind': s.layout == WidgetLayout.dual ? 'dual' : 'todo',
            }))
            .timeout(const Duration(seconds: 6));
        _windowId = controller.windowId;
        try {
          await controller.setTitle(widgetWindowTitle);
          await controller.setFrame(const Offset(1200, 260) & defaultSize);
          await controller.show();
        } catch (_) {}
      }
    } catch (e) {
      debugPrint('[launcher] createWindow failed: $e');
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
