using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Memodo.Windows.Views;

namespace Memodo.Windows.Services;

/// <summary>
/// 系统托盘（任务书 §24）：任务栏图标 + 右键菜单 + 自启注册表项。
/// 不引第三方 Shell32 / WinForms，纯 NotifyIcon.Wpf + Win32 Registry。
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _tray;
    private Window? _mainWindow;
    private DesktopWidgetWindow? _widget;

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
        // 蓝图 §21 托盘菜单：Show、Widget、New Todo/Memo、Sync Now、Settings、自启、Exit
        var newTodo = new System.Windows.Controls.MenuItem { Header = "新建待办" };
        newTodo.Click += (_, _) => { ShowMainWindow(); (_mainWindow as ShellWindow)?.ShowPage("todo"); };
        var newMemo = new System.Windows.Controls.MenuItem { Header = "新建备忘" };
        newMemo.Click += (_, _) => { ShowMainWindow(); (_mainWindow as ShellWindow)?.ShowPage("memo"); };
        var syncNow = new System.Windows.Controls.MenuItem { Header = "立即同步" };
        syncNow.Click += async (_, _) => await SyncNowAsync();
        var settings = new System.Windows.Controls.MenuItem { Header = "设置" };
        settings.Click += (_, _) => { ShowMainWindow(); (_mainWindow as ShellWindow)?.ShowPage("settings"); };
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
        var showWidget = new System.Windows.Controls.MenuItem { Header = "显示桌面组件" };
        showWidget.Click += (_, _) => ShowWidget();
        menu.Items.Add(showWidget);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(newTodo);
        menu.Items.Add(newMemo);
        menu.Items.Add(syncNow);
        menu.Items.Add(settings);
        menu.Items.Add(autostart);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(quit);
        _tray.ContextMenu = menu;
        _tray.TrayLeftMouseDown += (_, _) => ShowMainWindow();
    }

    public void ShowWidget()
    {
        if (_widget is null) _widget = new DesktopWidgetWindow();
        if (!_widget.IsVisible) _widget.Show();
        _widget.Activate();
    }

    /// <summary>设置页改动后即时打到已打开的组件（修复「设置里的开关无效」）。</summary>
    public void ApplyWidgetSettings()
    {
        if (_widget is not null && _widget.IsVisible)
        {
            _widget.ApplySettings();
            _widget.Reload();
        }
    }

    /// <summary>托盘立即同步：当前接 WebDAV（坚果云）；失败弹窗，成功静默+刷新组件。</summary>
    public async System.Threading.Tasks.Task SyncNowAsync()
    {
        var s = SettingsStore.Current;
        if (s.SyncProvider != "webdav")
        {
            System.Windows.MessageBox.Show("当前通道为自建服务器，请在 设置 → 同步 中操作（0.5 版）。",
                "念念 Memodo");
            return;
        }
        try
        {
            var engine = AppHost.Services.GetRequiredService<SyncEngine>();
            var (_, _, err) = await engine.RunWebDavAsync();
            if (err is not null)
                System.Windows.MessageBox.Show("同步失败：" + err, "念念 Memodo");
            else
                _widget?.Reload();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("同步失败：" + ex.Message, "念念 Memodo");
        }
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
