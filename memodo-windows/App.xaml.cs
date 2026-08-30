using System;
using System.IO;
using System.Windows;
using Memodo.Windows.Services;
using Memodo.Windows.Views;

namespace Memodo.Windows;

public partial class App : Application
{
    public static TrayService? Tray { get; private set; }
    public static bool CloseToTray { get; set; } = true;

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
    }
}
