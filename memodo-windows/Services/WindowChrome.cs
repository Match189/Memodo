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

    // ---------------- 材质（移植 Flutter win32_window_style.setSurface） ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private const int ACCENT_DISABLED = 1;
    private const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 3;
    private const int ACCENT_ENABLE_BLURBEHIND = 4;
    private const int ACCENT_ENABLE_ACRYLICBLKBEHIND = 5;

    /// <summary>
    /// 窗口材质（Flutter Phase 2 移植 + 用户反馈修正）：acrylic 用 ACRYLICBLKBEHIND(5)，
    /// tint alpha=不透明度（BLURBEHIND(4) 在部分系统上忽略 alpha → 滑杆无效的根因）；
    /// 失败自动降级 TRANSPARENTGRADIENT(3) → DISABLED。
    /// </summary>
    public static void SetSurface(IntPtr hwnd, bool acrylic, int opacity, uint tintRgb)
    {
        try
        {
            var alpha = (uint)(Math.Clamp(opacity, 30, 100) * 255 / 100);
            var policy = new AccentPolicy();
            if (!acrylic && opacity >= 100)
            {
                policy.AccentState = ACCENT_DISABLED;
            }
            else if (acrylic)
            {
                policy.AccentState = ACCENT_ENABLE_ACRYLICBLKBEHIND;
                policy.GradientColor = (alpha << 24) | (tintRgb & 0x00FFFFFF);
            }
            else
            {
                policy.AccentState = ACCENT_ENABLE_TRANSPARENTGRADIENT;
                policy.GradientColor = (alpha << 24) | (tintRgb & 0x00FFFFFF);
            }
            policy.AccentFlags = 2;
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                Data = ptr,
                SizeOfData = Marshal.SizeOf<AccentPolicy>(),
            };
            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(ptr);
        }
        catch { /* 降级为普通窗口 */ }
    }

    // ---------------- 桌面附着兼容清理 ----------------
    // 附着桌面功能已移除（WPF 窗口挂为其他进程子窗口后 D3D 内容不渲染，详见
    // docs/attach-desktop-analysis.md）。仅保留 DetachFromDesktop：把旧版本
    // 可能挂在 Progman/WorkerW 下的窗口解回顶层，避免升级后窗口不可见。

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
    private const uint GA_PARENT = 1;
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    private const uint SWP_NOSIZE = 0x1;
    private const uint SWP_NOMOVE = 0x2;
    private const uint SWP_FRAMECHANGED = 0x20;

    /// <summary>把旧版本附着在桌面层（Progman/WorkerW 子窗口）的窗口解回顶层。已是顶层则原样返回 true。</summary>
    public static bool DetachFromDesktop(IntPtr hwnd)
    {
        if (GetAncestor(hwnd, GA_PARENT) != GetDesktopWindow())
        {
            SetParent(hwnd, IntPtr.Zero);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
        }
        return GetAncestor(hwnd, GA_PARENT) == GetDesktopWindow();
    }

    private static bool IsWindows11() => Environment.OSVersion.Version.Build >= 22000;
}
