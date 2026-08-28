// HWND 与 int 比较、裸 win32 常量均为 win32 官方示例的标准写法（5.x 末代的
// 弃用提示指向的新名字与旧名相同），此处统一忽略 info 级提示。
// ignore_for_file: unrelated_type_equality_checks, deprecated_member_use
import 'dart:ffi' show nullptr;

import 'package:win32/win32.dart';

import '../pages/widget_window_page.dart' show widgetWindowTitle;

/// 用 Win32 API 给小组件子窗口做样式加工：
/// 去掉系统标题栏/边框、置顶、定位到屏幕右下角、提供拖拽启动。
/// 这些能力 desktop_multi_window 0.2.x 没有暴露，只能直接调 user32。
class WidgetWindowNative {
  WidgetWindowNative._();

  static int _findWidgetHwnd() => FindWindow(nullptr, TEXT(widgetWindowTitle));

  /// 去掉系统边框与标题栏，并按需置顶。
  static Future<void> applyFramelessAndTopmost({required bool alwaysOnTop}) async {
    final hwnd = _findWidgetHwnd();
    if (hwnd == 0) return;
    final style = GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
    final frameless = style &
        ~(WINDOW_STYLE.WS_CAPTION |
            WINDOW_STYLE.WS_THICKFRAME |
            WINDOW_STYLE.WS_SYSMENU |
            WINDOW_STYLE.WS_MINIMIZEBOX |
            WINDOW_STYLE.WS_MAXIMIZEBOX);
    SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, frameless);
    SetWindowPos(
      hwnd,
      alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST,
      0,
      0,
      0,
      0,
      SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
          SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
          SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED,
    );
  }

  /// 只切换置顶（设置里的开关）。
  static Future<void> setTopmost(bool topmost) async {
    final hwnd = _findWidgetHwnd();
    if (hwnd == 0) return;
    SetWindowPos(
      hwnd,
      topmost ? HWND_TOPMOST : HWND_NOTOPMOST,
      0,
      0,
      0,
      0,
      SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
          SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
          SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED,
    );
  }

  /// 把窗口放到主屏右下角（留一点边距）。
  static Future<void> placeAtBottomRight(int width, int height) async {
    final hwnd = _findWidgetHwnd();
    if (hwnd == 0) return;
    final screenW = GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
    final screenH = GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
    final left = screenW - width - 24;
    final top = screenH - height - 96;
    SetWindowPos(
      hwnd,
      0,
      left < 0 ? 24 : left,
      top < 0 ? 24 : top,
      0,
      0,
      SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
          SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
          SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE,
    );
  }

  /// 无边框窗口的拖拽：模拟按下标题栏。
  static void beginDrag() {
    final hwnd = _findWidgetHwnd();
    if (hwnd == 0) return;
    ReleaseCapture();
    SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
  }
}
