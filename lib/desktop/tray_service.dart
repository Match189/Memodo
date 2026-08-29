import 'dart:io';

import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;
import 'package:tray_manager/tray_manager.dart';

import 'main_window.dart';
import 'win32_window_style.dart';

/// Windows 系统托盘：图标 + 右键菜单（显示主窗口 / 退出）。
/// 左键点图标也打开主窗口。
class TrayService with TrayListener {
  TrayService._();

  static final TrayService instance = TrayService._();

  static const _iconAsset = 'assets/icon/tray_icon.ico';

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
  }

  @override
  void onTrayIconMouseDown() {
    WidgetWindowNative.openMainWindow();
  }

  @override
  void onTrayIconRightMouseUp() {
    TrayManager.instance.popUpContextMenu();
  }

  @override
  void onTrayMenuItemClick(MenuItem menuItem) {
    if (menuItem.key == 'show') {
      WidgetWindowNative.openMainWindow();
    } else if (menuItem.key == 'quit') {
      exit(0);
    }
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
