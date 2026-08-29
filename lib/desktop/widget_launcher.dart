import 'dart:async';
import 'dart:convert';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';

import 'win32_window_style.dart';
import 'widget_settings.dart';
import '../pages/widget_window_page.dart';

/// Windows 桌面小组件窗口管理（SPD §11/§12）：
/// - 布局可切换：单卡片（待办+备忘合并）/ 双卡片（待办、备忘两个独立窗口）
/// - 位置尺寸按窗口分别记忆；材质（不透明/毛玻璃/透明）与透明度可调
/// - 任何时刻至多一组窗口；并发触发被单飞吸收，孤儿子窗口自动收养/清理
class WidgetLauncher {
  WidgetLauncher._();

  static const singleSize = Size(300, 430);
  static const todoSplitSize = Size(300, 380);
  static const memoSplitSize = Size(300, 300);

  static int? _todoWindowId;
  static int? _memoWindowId;
  static bool _opening = false;
  static DesktopWidgetSettingsModel? _settings;
  static Timer? _rectWatcher;
  static bool _lastEnabled = false;
  static bool _lastTopmost = false;
  static int _lastOpacity = 90;
  static WidgetMaterial _lastMaterial = WidgetMaterial.solid;
  static WidgetLayout _lastLayout = WidgetLayout.single;
  static bool _lastAttach = false;

  static void bind(DesktopWidgetSettingsModel settings) {
    _settings = settings;
    _lastEnabled = settings.enabled;
    _lastTopmost = settings.alwaysOnTop;
    _lastOpacity = settings.opacity;
    _lastMaterial = settings.material;
    _lastLayout = settings.layout;
    _lastAttach = settings.attachToDesktop;
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
      return;
    }
    if (s.alwaysOnTop != _lastTopmost) {
      _lastTopmost = s.alwaysOnTop;
      await _forEachWindow(
          (title) => WidgetWindowNative.setTopmost(s.alwaysOnTop, windowTitle: title));
    }
    if (s.opacity != _lastOpacity || s.material != _lastMaterial) {
      _lastOpacity = s.opacity;
      _lastMaterial = s.material;
      await _applySurface();
    }
    if (s.attachToDesktop != _lastAttach) {
      _lastAttach = s.attachToDesktop;
      if (isOpen) await applyAttachState();
    }
  }

  static bool get isOpen => _todoWindowId != null || _memoWindowId != null;

  static List<String> get _openTitles => [
        if (_todoWindowId != null) widgetWindowTitle,
        if (_memoWindowId != null) memoWidgetWindowTitle,
      ];

  static Future<void> _forEachWindow(
      Future<void> Function(String title) action) async {
    for (final title in _openTitles) {
      await action(title);
    }
  }

  static Future<void> _applySurface() async {
    final s = _settings;
    if (s == null) return;
    await _forEachWindow((title) => WidgetWindowNative.setSurface(
          windowTitle: title,
          acrylic: s.material == WidgetMaterial.acrylic,
          opacity: s.opacity,
        ));
  }

  /// 应用"桌面层模式"开关；失败自动回退普通窗口并改回设置。
  static Future<void> applyAttachState() async {
    final s = _settings;
    if (s == null || !isOpen) return;
    if (!s.attachToDesktop) {
      await _forEachWindow((title) async {
        await WidgetWindowNative.detachFromDesktop(windowTitle: title);
        await WidgetWindowNative.setTopmost(s.alwaysOnTop, windowTitle: title);
      });
      return;
    }
    final ok = await WidgetWindowNative.attachToDesktopAll(titles: _openTitles);
    if (!ok) {
      await s.setAttachToDesktop(false);
      _lastAttach = false;
    }
  }

  /// 打开（或按布局补齐）小组件窗口组。
  ///
  /// 可被多条路径并发触发（设置监听器、启动恢复），用 [_opening] 单飞 +
  /// 孤儿子窗口收养，保证任何时刻至多一组窗口。
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

      final created = <String>[];
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
        created.add(widgetWindowTitle);
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
        created.add(memoWidgetWindowTitle);
      }
      // 单卡片布局下备忘窗口不应存在。
      if (!wantMemo && _memoWindowId != null) {
        final id = _memoWindowId!;
        _memoWindowId = null;
        try {
          await WindowController.fromWindowId(id).close();
        } catch (_) {}
      }

      // 窗口样式加工（去标题栏/置顶/材质/位置恢复）。
      for (final title in _openTitles) {
        final isMemo = title == memoWidgetWindowTitle;
        final hasSaved = isMemo
            ? (s.memoPosX != null && s.memoWidth != null)
            : (s.posX != null && s.width != null);
        await WidgetWindowNative.applyFramelessAndTopmost(
          windowTitle: title,
          alwaysOnTop: s.alwaysOnTop,
        );
        if (created.contains(title)) {
          if (hasSaved) {
            if (isMemo) {
              await WidgetWindowNative.setRect(s.memoPosX!, s.memoPosY!,
                  s.memoWidth!, s.memoHeight!,
                  windowTitle: title);
            } else {
              await WidgetWindowNative.setRect(
                  s.posX!, s.posY!, s.width!, s.height!,
                  windowTitle: title);
            }
          } else {
            final size = isMemo ? memoSplitSize : (wantMemo ? todoSplitSize : singleSize);
            await WidgetWindowNative.placeAtBottomRight(
                size.width.toInt(), size.height.toInt(),
                windowTitle: title);
          }
          await WidgetWindowNative.setSurface(
            windowTitle: title,
            acrylic: s.material == WidgetMaterial.acrylic,
            opacity: s.opacity,
          );
        }
      }
      if (s.attachToDesktop && _openTitles.isNotEmpty) {
        _lastAttach = true;
        final ok = await WidgetWindowNative.attachToDesktopAll(
            titles: _openTitles);
        if (!ok) await s.setAttachToDesktop(false);
      }
      // 位置记忆：布局完成即保存一次；后续在关闭卡片/应用时保存。
      // （周期轮询采样在部分环境下触发原生崩溃，暂停使用，待 debug 会话排查。）
      for (final title in _openTitles) {
        final r = WidgetWindowNative.getRect(windowTitle: title);
        if (r == null) continue;
        if (title == memoWidgetWindowTitle) {
          await s.saveMemoWindowRect(x: r.x, y: r.y, w: r.w, h: r.h);
        } else {
          await s.saveWindowRect(x: r.x, y: r.y, w: r.w, h: r.h);
        }
      }
    } finally {
      _opening = false;
    }
  }

  static Future<void> close() async {
    _rectWatcher?.cancel();
    _rectWatcher = null;
    final ids = [_todoWindowId, _memoWindowId].whereType<int>().toList();
    _todoWindowId = null;
    _memoWindowId = null;
    for (final id in ids) {
      try {
        await WindowController.fromWindowId(id).close();
      } catch (_) {}
    }
  }

  /// 小组件窗口自己点了关闭：清引用但不重复关；一组全关时停采样。
  static void forget(int windowId) {
    if (_todoWindowId == windowId) _todoWindowId = null;
    if (_memoWindowId == windowId) _memoWindowId = null;
    if (!isOpen) {
      _rectWatcher?.cancel();
      _rectWatcher = null;
    }
  }

  /// 设置里的置顶/透明度(材质)/桌面层变化时调用。
  static Future<void> updateTopmost(bool topmost) async {
    if (!isOpen) return;
    await _forEachWindow(
        (title) => WidgetWindowNative.setTopmost(topmost, windowTitle: title));
  }

  static Future<void> updateOpacity(int opacity) async {
    if (!isOpen) return;
    await _applySurface();
  }

  static Future<bool> updateAttachToDesktop(bool attach) async {
    if (!isOpen) return false;
    if (!attach) {
      await _forEachWindow((title) async {
        await WidgetWindowNative.detachFromDesktop(windowTitle: title);
        await WidgetWindowNative.setTopmost(
            _settings?.alwaysOnTop ?? false,
            windowTitle: title);
      });
      return true;
    }
    return WidgetWindowNative.attachToDesktopAll(titles: _openTitles);
  }
}
