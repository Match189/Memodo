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

    // ---------------- 桌面层（移植 Flutter Phase 3：Progman/WorkerW） ----------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc proc, IntPtr l);
    [DllImport("user32.dll")]
    private static extern bool SetParent(IntPtr child, IntPtr parent);
    private delegate bool EnumProc(IntPtr hwnd, IntPtr l);
    private const uint SMTO_NORMAL = 0x2;

    /// <summary>
    /// 附到桌面层（SPD §13）：向 Progman 发 0x052C 生成 WorkerW，
    /// 找到含 SHELLDLL_DefView 之后的那个 WorkerW 作为父窗。失败返回 false（调用方回退）。
    /// </summary>
    public static bool AttachToDesktop(IntPtr hwnd)
    {
        try
        {
            var progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return false;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out _);

            IntPtr worker = IntPtr.Zero;
            EnumWindows((top, l) =>
            {
                if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (worker == IntPtr.Zero) return false;
            return SetParent(hwnd, worker);
        }
        catch { return false; }
    }

    public static bool DetachFromDesktop(IntPtr hwnd) => SetParent(hwnd, IntPtr.Zero);

    private static bool IsWindows11() => Environment.OSVersion.Version.Build >= 22000;
}
