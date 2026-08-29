import 'dart:async';
import 'dart:convert';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';

import 'widget_settings.dart';
import '../pages/widget_window_page.dart';

/// Windows 桌面小组件窗口管理（SPD §11/§12）。
///
/// 架构约定（防跨进程崩溃）：主进程只做窗口创建/关闭/发命令
/// （desktop_multi_window 插件级 API，进程安全）；**对本窗口的原生加工
/// （无边框/置顶/材质/附着桌面/位置采样）由小组件子窗口进程自己执行**
/// （见 widget_window_page 的自套样式与命令响应）。
class WidgetLauncher {
  WidgetLauncher._();

  static const singleSize = Size(300, 430);
  static const todoSplitSize = Size(300, 380);
  static const memoSplitSize = Size(300, 300);

  static int? _todoWindowId;
  static int? _memoWindowId;
  static bool _opening = false;
  static DesktopWidgetSettingsModel? _settings;
  static bool _lastEnabled = false;
  static bool _lastLayoutNotified = false;
  static WidgetLayout _lastLayout = WidgetLayout.single;

  static void bind(DesktopWidgetSettingsModel settings) {
    _settings = settings;
    _lastEnabled = settings.enabled;
    _lastLayout = settings.layout;
    settings.addListener(_onSettingsChanged);
  }

  /// 只对布局/开关变化做出反应（这些会改变窗口集合）。
  static Future<void> _onSettingsChanged() async {
    final s = _settings;
    if (s == null) return;
    if (s.enabled != _lastEnabled) {
      _lastEnabled = s.enabled;
      if (s.enabled) {
        await ensureOpen();
      } else {
        await close();
      }
      return;
    }
    if (s.layout != _lastLayout) {
      _lastLayout = s.layout;
      // 布局切换 = 关掉按新布局重开（各自位置记忆保留）。
      await close();
      if (s.enabled) await ensureOpen();
    }
  }

  static bool get isOpen => _todoWindowId != null || _memoWindowId != null;

  static List<int> get _openIds => [
        if (_todoWindowId != null) _todoWindowId!,
        if (_memoWindowId != null) _memoWindowId!,
      ];

  /// 打开（或按布局补齐）小组件窗口组。
  ///
  /// 可被多条路径并发触发，用 [_opening] 单飞 + 孤儿子窗口收养，
  /// 保证任何时刻至多一组窗口。原生样式由子窗口进程自己套。
  static Future<void> ensureOpen() async {
    if (_opening) return;
    _opening = true;
    try {
      final s = _settings;
      if (s == null) return;
      final wantMemo = s.layout == WidgetLayout.split;

      // 自愈：遗留的孤儿子窗口先收养（第一个归待办，第二个归备忘），多余关掉。
      try {
        final ids = await DesktopMultiWindow.getAllSubWindowIds();
        for (final id in ids) {
          if (_todoWindowId == null) {
            _todoWindowId = id;
          } else if (wantMemo && _memoWindowId == null) {
            _memoWindowId = id;
          } else {
            await WindowController.fromWindowId(id).close();
          }
        }
      } catch (_) {}

      if (_todoWindowId == null) {
        final controller = await DesktopMultiWindow.createWindow(jsonEncode({
          'type': 'widget',
          'kind': 'todo',
        }));
        _todoWindowId = controller.windowId;
        final size = wantMemo ? todoSplitSize : singleSize;
        await controller
          ..setTitle(widgetWindowTitle)
          ..setFrame(const Offset(1200, 260) & size)
          ..show();
      }
      if (wantMemo && _memoWindowId == null) {
        final controller = await DesktopMultiWindow.createWindow(jsonEncode({
          'type': 'widget',
          'kind': 'memo',
        }));
        _memoWindowId = controller.windowId;
        await controller
          ..setTitle(memoWidgetWindowTitle)
          ..setFrame(const Offset(960, 460) & memoSplitSize)
          ..show();
      }
      // 单卡片布局下备忘窗口不应存在。
      if (!wantMemo && _memoWindowId != null) {
        final id = _memoWindowId!;
        _memoWindowId = null;
        try {
          await WindowController.fromWindowId(id).close();
        } catch (_) {}
      }
    } finally {
      _opening = false;
    }
  }

  static Future<void> close() async {
    final ids = [_todoWindowId, _memoWindowId].whereType<int>().toList();
    _todoWindowId = null;
    _memoWindowId = null;
    for (final id in ids) {
      try {
        await WindowController.fromWindowId(id).close();
      } catch (_) {}
    }
    // desktop_multi_window 0.2.x 的 close() 只发原生信号，子进程退出有延迟。
    // 紧接着的 createWindow 可能撞上仍持着 exe 的子进程（Windows LNK1104 风险）。
    // 轮询等待子进程窗口彻底消失，最多 ~1.5s。
    for (var i = 0; i < 15; i++) {
      try {
        final left = await DesktopMultiWindow.getAllSubWindowIds();
        if (left.isEmpty) break;
      } catch (_) {
        break;
      }
      await Future<void>.delayed(const Duration(milliseconds: 100));
    }
  }

  /// 小组件窗口自己点了关闭：清引用但不重复关。
  static void forget(int windowId) {
    if (_todoWindowId == windowId) _todoWindowId = null;
    if (_memoWindowId == windowId) _memoWindowId = null;
  }

  /// 广播材质/透明度变化给小组件（子窗口在本进程内自套 accent）。
  static Future<void> updateSurface({
    required WidgetMaterial material,
    required int opacity,
  }) async {
    for (final id in _openIds) {
      try {
        await DesktopMultiWindow.invokeMethod(id, 'applySurface', {
          'acrylic': material == WidgetMaterial.acrylic,
          'opacity': opacity,
        });
      } catch (_) {}
    }
  }

  /// 广播置顶变化。
  static Future<void> updateTopmost(bool topmost) async {
    for (final id in _openIds) {
      try {
        await DesktopMultiWindow.invokeMethod(id, 'setTopmost', topmost);
      } catch (_) {}
    }
  }

  /// 广播附着桌面/脱离（子窗口在本进程内 WorkerW SetParent，安全）。
  static Future<void> updateAttach(bool attach) async {
    for (final id in _openIds) {
      try {
        await DesktopMultiWindow.invokeMethod(id, attach ? 'attach' : 'detach');
      } catch (_) {}
    }
  }
}
