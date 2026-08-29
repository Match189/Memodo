using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Memodo.Windows.Services;

/// <summary>
/// 原生窗口增强（任务书 §24）：去标题栏 + Mica 背景 + 圆角 + 关闭拦截。
/// </summary>
public static class WindowChrome
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pv, int cb);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2;

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_SYSMENU = 0x00080000L;

    [DllImport("user32.dll")]
    private static extern long GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

    public static void ApplyFrameless(Window w, bool rounded = true)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        var style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_SYSMENU);
        style |= WS_THICKFRAME;
        SetWindowLong(hwnd, GWL_STYLE, style);
        if (rounded)
        {
            int pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
    }

    public static void ApplyMica(Window w)
    {
        if (!IsWindows11()) return;
        var hwnd = new WindowInteropHelper(w).Handle;
        int bt = DWMSBT_MAINWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref bt, sizeof(int));
    }

    private static bool IsWindows11() => Environment.OSVersion.Version.Build >= 22000;
}
