using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace Memodo.Windows.Services;

/// <summary>
/// 系统托盘（任务书 §24）：任务栏图标 + 右键菜单 + 自启注册表项。
/// 不引第三方 Shell32 / WinForms，纯 NotifyIcon.Wpf + Win32 Registry。
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _tray;
    private Window? _mainWindow;

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Memodo";

    public TrayService()
    {
        _tray = new TaskbarIcon
        {
            ToolTipText = "念念 Memodo",
            Icon = LoadTrayIcon(),
            Visibility = Visibility.Visible,
        };
        var menu = new System.Windows.Controls.ContextMenu();
        var show = new System.Windows.Controls.MenuItem { Header = "显示主窗口" };
        show.Click += (_, _) => ShowMainWindow();
        var autostart = new System.Windows.Controls.MenuItem
        {
            Header = "开机自启",
            IsCheckable = true,
            IsChecked = IsAutostartEnabled(),
        };
        autostart.Click += (_, _) => ToggleAutostart(autostart);
        var quit = new System.Windows.Controls.MenuItem { Header = "退出" };
        quit.Click += (_, _) => Quit();
        menu.Items.Add(show);
        menu.Items.Add(autostart);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(quit);
        _tray.ContextMenu = menu;
        _tray.TrayLeftMouseDown += (_, _) => ShowMainWindow();
    }

    public void Attach(Window mainWindow) => _mainWindow = mainWindow;

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    public void Quit()
    {
        _tray.Visibility = Visibility.Collapsed;
        _tray.Dispose();
        Application.Current.Shutdown(0);
    }

    public static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) is string;
    }

    public static void SetAutostart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key is null) return;
        if (enable)
        {
            var exe = Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(exe)) key.SetValue(AppName, "\"" + exe + "\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    private void ToggleAutostart(System.Windows.Controls.MenuItem item) => SetAutostart(item.IsChecked);

    private static Icon LoadTrayIcon()
    {
        var exe = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
        {
            try { return new Icon(exe); } catch { }
        }
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.FromArgb(0, 105, 109));
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose() => _tray.Dispose();
}
