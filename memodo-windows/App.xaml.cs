using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;
using Memodo.Windows.Views;

namespace Memodo.Windows;

public partial class App : Application
{
    public static TrayService? Tray { get; private set; }
    public static bool CloseToTray { get; set; } = true;
    private DispatcherTimer? _syncTimer;
    private bool _syncing;

    /// <summary>数据变更广播：小组件/同步等改动后，主窗口列表据此刷新。</summary>
    public static event Action? DataChanged;
    public static void NotifyDataChanged() => DataChanged?.Invoke();

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            File.WriteAllText("crash.log",
                args.ExceptionObject.ToString() + Environment.StackTrace);
        DispatcherUnhandledException += (_, args) =>
        {
            File.WriteAllText("crash.log",
                args.Exception.ToString() + args.Exception.StackTrace);
            args.Handled = true;
        };
        base.OnStartup(e);

        ThemeService.Apply(); // 设计系统：Cork/Glass/Hybrid × Dark

        var win = new ShellWindow();
        win.Loaded += (_, _) =>
        {
            try { WindowChrome.ApplyFrameless(win); WindowChrome.ApplyMica(win); } catch { }
            Tray = new TrayService();
            Tray.Attach(win);
            if (SettingsStore.Current.ShowWidgetOnStartup) Tray.ShowWidget();
        };
        win.Closing += (_, args) =>
        {
            if (CloseToTray)
            {
                args.Cancel = true;
                win.Hide();
            }
        };
        win.Show();
        StartAutoSync();
    }

    /// <summary>自动同步（Flutter sync_manager 精神移植）：启动一次 + 每 3 分钟，WebDAV 通道，静默。</summary>
    private void StartAutoSync()
    {
        if (_syncTimer is not null) return;
        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        _syncTimer.Tick += async (_, _) => await RunAutoSync();
        _syncTimer.Start();
        _ = RunAutoSync();
    }

    private async System.Threading.Tasks.Task RunAutoSync()
    {
        if (_syncing) return;
        var s = SettingsStore.Current;
        if (!s.AutoSync || s.SyncProvider != "webdav") return;
        if (string.IsNullOrWhiteSpace(s.WebDavUser) || string.IsNullOrEmpty(s.WebDavPassProtected)) return;
        _syncing = true;
        try
        {
            var engine = AppHost.Services.GetRequiredService<SyncEngine>();
            var (_, _, err) = await engine.RunWebDavAsync();
            if (err is null)
            {
                Tray?.ApplyWidgetSettings(); // 静默成功，刷新组件数据
                NotifyDataChanged();          // 主窗口列表联动
            }
        }
        catch { /* 离线静默，下轮重试 */ }
        finally { _syncing = false; }
    }
}
