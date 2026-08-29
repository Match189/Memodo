// HWND 与 int 比较、裸 win32 常量均为 win32 官方示例的标准写法（5.x 末代的
// 弃用提示指向的新名字与旧名相同），此处统一忽略 info 级提示。
// ignore_for_file: unrelated_type_equality_checks, deprecated_member_use
import 'dart:ffi';

import 'package:ffi/ffi.dart';
import 'package:win32/win32.dart';

import '../pages/widget_window_page.dart'
    show widgetWindowTitle, memoWidgetWindowTitle;
import 'main_window.dart' show mainWindowTitle, mainWindowDisplayTitle;

/// 用 Win32 API 给小组件子窗口做样式加工（SPD §11 Windows Native API）：
/// 去标题栏但保留可缩放边框、可选置顶、材质（不透明/毛玻璃/透明）、
/// 位置采样与拖拽。desktop_multi_window 0.2.x 未暴露这些能力，直接调 user32。
class WidgetWindowNative {
  WidgetWindowNative._();

  static int _findByTitle(String title) => FindWindow(nullptr, TEXT(title));

  /// 去掉标题栏（保留 WS_THICKFRAME → 仍可缩放），并按需置顶。
  static Future<void> applyFramelessAndTopmost(
      {required String windowTitle, required bool alwaysOnTop}) async {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return;
    final style = GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
    final frameless = style &
        ~(WINDOW_STYLE.WS_CAPTION |
            WINDOW_STYLE.WS_SYSMENU |
            WINDOW_STYLE.WS_MINIMIZEBOX |
            WINDOW_STYLE.WS_MAXIMIZEBOX);
    SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, frameless);
    // 工具窗口样式：不占任务栏、不进 Alt-Tab（桌面挂件的定位）。
    final ex = GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
    SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
        ex | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
    setTopmost(alwaysOnTop, windowTitle: windowTitle);
  }

  /// 只切换置顶。
  static Future<void> setTopmost(bool topmost,
      {required String windowTitle}) async {
    final hwnd = _findByTitle(windowTitle);
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

  /// 当前窗口矩形（外框，屏幕坐标）；取不到返回 null。
  static ({int x, int y, int w, int h})? getRect(
      {required String windowTitle}) {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return null;
    final rect = calloc<RECT>();
    try {
      if (GetWindowRect(hwnd, rect) == 0) return null;
      return (
        x: rect.ref.left,
        y: rect.ref.top,
        w: rect.ref.right - rect.ref.left,
        h: rect.ref.bottom - rect.ref.top,
      );
    } finally {
      calloc.free(rect);
    }
  }

  /// 把窗口放到指定矩形。
  static Future<void> setRect(int x, int y, int w, int h,
      {required String windowTitle}) async {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return;
    SetWindowPos(hwnd, 0, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
  }

  /// 把窗口放到主屏右下角（留一点边距）。
  static Future<void> placeAtBottomRight(int width, int height,
      {required String windowTitle}) async {
    final screenW = GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
    final screenH = GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
    final left = screenW - width - 24;
    final top = screenH - height - 96;
    await setRect(
        left < 0 ? 24 : left, top < 0 ? 24 : top, width, height,
        windowTitle: windowTitle);
  }

  /// 窗口材质与不透明度（30-100）：
  /// - solid：正常窗口
  /// - acrylic：毛玻璃（ACCENT_ENABLE_BLURBEHIND，背景模糊）
  /// - transparent：透明渐变（ACCENT_ENABLE_TRANSPARENTGRADIENT + alpha）
  /// 未文档化 API，广泛使用且稳定；失败静默降级为不透明。
  static Future<void> setSurface(
      {required String windowTitle,
      required bool acrylic,
      required int opacity}) async {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return;
    final user32 = DynamicLibrary.open('user32.dll');
    final setAttr = user32.lookupFunction<
        Int32 Function(IntPtr, Pointer<WindowCompositionAttribData>),
        int Function(
            int, Pointer<WindowCompositionAttribData>)>('SetWindowCompositionAttribute');
    final data = calloc<WindowCompositionAttribData>();
    final policy = calloc<AccentPolicy>();
    try {
      final alpha = (opacity.clamp(30, 100) * 255 ~/ 100);
      if (!acrylic && opacity >= 100) {
        policy.ref.AccentState = ACCENT_DISABLED;
      } else if (acrylic) {
        policy.ref.AccentState = ACCENT_ENABLE_BLURBEHIND;
        policy.ref.GradientColor = (alpha << 24) | 0x101418;
      } else {
        policy.ref.AccentState = ACCENT_ENABLE_TRANSPARENTGRADIENT;
        policy.ref.GradientColor = (alpha << 24) | 0x00FFFFFF;
      }
      policy.ref.AccentFlags = 2;
      data.ref.Attribute = policy.ref.AccentState;
      data.ref.Data = policy;
      data.ref.DataSize = sizeOf<AccentPolicy>();
      setAttr(hwnd, data);
    } finally {
      calloc.free(data);
      calloc.free(policy);
    }
  }

  /// 无边框窗口的拖拽：模拟按下标题栏。
  static void beginDrag({required String windowTitle}) {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return;
    ReleaseCapture();
    SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
  }

  /// SPD §13 V2：把窗口附到桌面层（WorkerW），成为壁纸一样的存在。
  /// 返回是否成功；失败时调用方应回退普通窗口模式。
  static Future<bool> attachToDesktop({required String windowTitle}) async {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return false;
    final progman = FindWindow(nullptr, TEXT('Program Manager'));
    if (progman == 0) return false;

    // 让 Explorer 在壁纸后面生成一个 WorkerW（0x052C 魔数消息）。
    final user32 = DynamicLibrary.open('user32.dll');
    final sendMessageTimeout = user32.lookupFunction<
        IntPtr Function(IntPtr, Uint32, IntPtr, IntPtr, Uint32, Uint32,
            Pointer<Void>),
        int Function(int, int, int, int, int, int,
            Pointer<Void>)>('SendMessageTimeoutW');
    sendMessageTimeout(progman, 0x052C, 0, 0, 0 /*SMTO_NORMAL*/, 1000, nullptr);

    final worker = _findDesktopWorkerW();
    if (worker == 0) return false;

    // 作为 WorkerW 子窗口时不再需要任何顶层样式交互。
    if (SetParent(hwnd, worker) == 0) return false;
    setTopmost(false, windowTitle: windowTitle);
    return true;
  }

  /// 从桌面层脱离，恢复普通顶层窗口。
  static Future<void> detachFromDesktop(
      {required String windowTitle}) async {
    final hwnd = _findByTitle(windowTitle);
    if (hwnd == 0) return;
    SetParent(hwnd, 0);
    SetWindowPos(
      hwnd,
      0,
      0,
      0,
      0,
      0,
      SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
          SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
          SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED,
    );
  }

  /// 找到承载桌面图标的 SHELLDLL_DefView 之后的那个 WorkerW。
  static int _findDesktopWorkerW() {
    var worker = 0;
    while (true) {
      worker = FindWindowEx(0, worker, TEXT('WorkerW'), nullptr);
      if (worker == 0) return 0;
      final defview =
          FindWindowEx(worker, 0, TEXT('SHELLDLL_DefView'), nullptr);
      if (defview != 0) {
        final target = FindWindowEx(0, worker, TEXT('WorkerW'), nullptr);
        return target != 0 ? target : worker;
      }
    }
  }

  /// 批量把多个窗口附到桌面层；任一失败则全部脱离并返回 false。
  static Future<bool> attachToDesktopAll(
      {required List<String> titles}) async {
    for (final title in titles) {
      if (!await attachToDesktop(windowTitle: title)) {
        for (final t in titles) {
          await detachFromDesktop(windowTitle: t);
        }
        return false;
      }
    }
    return true;
  }

  /// 小组件上的"打开主窗口"：把主窗口恢复并带到前台。兼容重命名前后标题。
  static Future<void> openMainWindow() async {
    var hwnd = FindWindow(nullptr, TEXT(mainWindowDisplayTitle));
    if (hwnd == 0) hwnd = FindWindow(nullptr, TEXT(mainWindowTitle));
    if (hwnd == 0) return;
    ShowWindow(hwnd, SW_RESTORE);
    SetForegroundWindow(hwnd);
  }

  /// 启动后把窗口标题改成品牌名（运行时改，规避 Runner 编码问题）。
  static Future<void> setMainWindowTitle(String title) async {
    final hwnd = FindWindow(nullptr, TEXT(mainWindowTitle));
    if (hwnd == 0) return;
    SetWindowText(hwnd, TEXT(title));
  }
}

/// SetWindowCompositionAttribute 的参数结构（未文档化，手工绑定）。
final class WindowCompositionAttribData extends Struct {
  /// WINDOWCOMPOSITIONATTRIB 枚举值（ACCENT_*）。
  @Int32()
  external int Attribute;

  external Pointer<AccentPolicy> Data;

  @IntPtr()
  external int DataSize;
}

final class AccentPolicy extends Struct {
  @Int32()
  external int AccentState;

  @Int32()
  external int AccentFlags;

  @Uint32()
  external int GradientColor;

  @Int32()
  external int AnimationId;
}

const int ACCENT_DISABLED = 0;
const int ACCENT_ENABLE_BLURBEHIND = 3;
const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 4;
