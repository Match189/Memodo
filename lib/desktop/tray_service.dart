import 'dart:io';

import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;
import 'package:tray_manager/tray_manager.dart';
import 'package:window_manager/window_manager.dart';

import 'main_window.dart';
import 'win32_window_style.dart';

/// Windows 系统托盘：图标 + 右键菜单（显示主窗口 / 退出）。
/// 同时接管主窗口关闭行为：closeToTray 时隐藏进托盘，否则真正退出。
class TrayService with TrayListener, WindowListener {
  TrayService._();

  static final TrayService instance = TrayService._();

  static const _iconAsset = 'assets/icon/tray_icon.ico';

  /// 关闭主窗口时是否隐藏进托盘（由设置驱动）。
  bool closeToTray = true;

  /// 初始化：把打包在资产里的 ico 解到应用支持目录，再交给系统托盘。
  Future<void> init() async {
    try {
      final support = await getApplicationSupportDir();
      final iconPath = p.join(support, 'memodo_tray.ico');
      final data = await rootBundle.load(_iconAsset);
      await File(iconPath).writeAsBytes(data.buffer.asUint8List(), flush: true);

      final tray = TrayManager.instance;
      await tray.setIcon(iconPath);
      await tray.setToolTip('念念 Memodo');
      await tray.setContextMenu(Menu(
        items: [
          MenuItem(key: 'show', label: '显示主窗口'),
          MenuItem.separator(),
          MenuItem(key: 'quit', label: '退出'),
        ],
      ));
      tray.addListener(this);
    } catch (_) {
      // 托盘失败不影响主功能（如精简环境无托盘）。
    }
    // 主窗口关闭拦截（window_manager 只管主窗口）。
    WindowManager.instance.addListener(this);
  }

  @override
  void onTrayIconMouseDown() {
    openMainWindow();
  }

  @override
  void onTrayIconRightMouseUp() {
    TrayManager.instance.popUpContextMenu();
  }

  @override
  void onTrayMenuItemClick(MenuItem menuItem) {
    if (menuItem.key == 'show') {
      openMainWindow();
    } else if (menuItem.key == 'quit') {
      quit();
    }
  }

  /// 主窗口关闭事件（window_manager preventClose 拦截后回调到这里）。
  @override
  void onWindowClose() async {
    if (closeToTray) {
      await WindowManager.instance.hide(); // 隐藏进托盘，进程存活
    } else {
      await quit();
    }
  }

  /// 托盘"退出"：真正结束应用（绕过 preventClose）。
  Future<void> quit() async {
    await WindowManager.instance.destroy();
    exit(0);
  }

  /// 把主窗口带到前台（含从托盘隐藏状态恢复）。
  void openMainWindow() {
    WidgetWindowNative.openMainWindow();
  }
}

/// 应用支持目录（避免直接依赖 path_provider 的调用点分散）。
Future<String> getApplicationSupportDir() async {
  final appData = Platform.environment['APPDATA'];
  final dir = appData == null
      ? Directory.systemTemp.path
      : p.join(appData, 'app.memodo', 'Memodo');
  await Directory(dir).create(recursive: true);
  return dir;
}
