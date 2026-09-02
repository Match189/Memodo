using System;
using System.IO;
using System.Threading;
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
    private static Mutex? _mutex;
    private static bool _quitting;

    /// <summary>托盘退出前调用：放行 Closing 拦截，真正结束进程。</summary>
    public static void RequestQuit()
    {
        _quitting = true;
        Current.Shutdown();
    }

    /// <summary>数据变更广播：小组件/同步等改动后，主窗口列表据此刷新。</summary>
    public static event Action? DataChanged;
    public static void NotifyDataChanged() => DataChanged?.Invoke();

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例检查：已运行则提醒并退出
        _mutex = new Mutex(true, "Global\\MemodoApp_SingleInstance", out var created);
        if (!created)
        {
            MessageBox.Show(
                LocalizationService.T("app_already_running"),
                LocalizationService.T("app_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

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

        ThemeService.Apply();        // 设计系统：Cork/Glass/Hybrid × Dark
        LocalizationService.Apply(); // 双语（DESIGN_APPLE.md / 用户裁定）

        var win = new ShellWindow();
        win.Loaded += (_, _) =>
        {
            try { WindowChrome.ApplyFrameless(win); WindowChrome.ApplyMica(win); } catch { }
            // 一次性恢复历史归档数据（归档功能已移除；只跑一次，避免与跨端同步互相覆盖）
            try
            {
                var flagPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "app.memodo", "unarchive_done.flag");
                if (!System.IO.File.Exists(flagPath))
                {
                    var taskRepo = AppHost.Services.GetRequiredService<Repositories.TaskRepository>();
                    var memoRepo = AppHost.Services.GetRequiredService<Repositories.MemoRepository>();
                    taskRepo.UnarchiveAll();
                    memoRepo.UnarchiveAll();
                    System.IO.File.WriteAllText(flagPath, DateTimeOffset.Now.ToString("O"));
                }
            } catch { }
            Tray = new TrayService();
            Tray.Attach(win);
            if (SettingsStore.Current.ShowWidgetOnStartup) Tray.ShowWidget();
        };
        win.Closing += (_, args) =>
        {
            // 正在退出（托盘退出/系统关机）时不拦截，否则进程变僵尸
            if (_quitting) return;
            if (CloseToTray)
            {
                args.Cancel = true;
                win.Hide();
            }
        };
        win.Show();
        StartAutoSync();
    }

    /// <summary>自动同步：启动一次 + 按用户设置的间隔（WebDAV 通道，静默）。</summary>
    private void StartAutoSync()
    {
        if (_syncTimer is null)
        {
            _syncTimer = new DispatcherTimer();
            _syncTimer.Tick += async (_, _) => await RunAutoSync();
        }
        _syncTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(SettingsStore.Current.AutoSyncIntervalMinutes, 1, 120));
        _syncTimer.Start();
        _ = RunAutoSync();
    }

    /// <summary>设置页修改间隔后调用。</summary>
    public static void RestartAutoSyncTimer()
    {
        if (Current is App app)
        {
            app._syncTimer?.Stop();
            app.StartAutoSync();
        }
    }

    /// <summary>自动同步：启动一次 + 按用户设置的间隔（WebDAV 与服务器通道均生效，静默）。</summary>
    private async System.Threading.Tasks.Task RunAutoSync()
    {
        if (_syncing) return;
        var s = SettingsStore.Current;
        if (!s.AutoSync) return;
        _syncing = true;
        try
        {
            string? err = null;
            if (s.SyncProvider == "webdav")
            {
                // 必填项不完整时静默跳过，等用户补全
                if (string.IsNullOrWhiteSpace(s.WebDavUser) || string.IsNullOrEmpty(s.WebDavPassProtected)) return;
                var engine = AppHost.Services.GetRequiredService<SyncEngine>();
                var (_, _, e) = await engine.RunWebDavAsync();
                err = e;
            }
            else if (s.SyncProvider == "server")
            {
                if (string.IsNullOrWhiteSpace(s.ServerUrl) || string.IsNullOrEmpty(s.ServerPassProtected)) return;
                var engine = AppHost.Services.GetRequiredService<SyncEngine>();
                var (_, _, e) = await engine.RunAsync(SecretProtector.Unprotect(s.ServerPassProtected));
                err = e;
            }
            else return;

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
