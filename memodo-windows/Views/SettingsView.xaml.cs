using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

public partial class SettingsView : UserControl
{
    private SyncService Sync => AppHost.Services.GetRequiredService<SyncService>();
    private SyncEngine Engine => AppHost.Services.GetRequiredService<SyncEngine>();

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var s = SettingsStore.Current;
            ServerUrlBox.Text = s.ServerUrl;
            EmailBox.Text = s.AccountEmail;
            StartWidgetChk.IsChecked = s.ShowWidgetOnStartup;
            TopmostChk.IsChecked = s.WidgetTopmost;
        };
    }

    private void SaveBasic()
    {
        var s = SettingsStore.Current;
        s.ServerUrl = ServerUrlBox.Text.Trim();
        s.AccountEmail = EmailBox.Text.Trim();
        SettingsStore.Save();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        SaveBasic();
        if (string.IsNullOrWhiteSpace(SettingsStore.Current.ServerUrl))
        {
            SyncStatus.Text = "请先填写服务器地址，例如 http://localhost:8000";
            return;
        }
        Sync.ServerUrl = SettingsStore.Current.ServerUrl;
        SyncStatus.Text = "登录中…";
        var (ok, err) = await Sync.LoginAsync(SettingsStore.Current.AccountEmail, PwdBox.Password);
        SyncStatus.Text = ok ? "登录成功 ✓" : "登录失败：" + err;
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        SaveBasic();
        if (string.IsNullOrWhiteSpace(SettingsStore.Current.ServerUrl))
        {
            SyncStatus.Text = "请先填写服务器地址";
            return;
        }
        Sync.ServerUrl = SettingsStore.Current.ServerUrl;
        SyncStatus.Text = "同步中…";
        var (pulled, pushed, err) = await Engine.RunAsync(PwdBox.Password);
        SyncStatus.Text = err is null
            ? $"同步完成：推送 {pushed} 条，拉取 {pulled} 条"
            : "同步失败：" + err;
    }

    private void StartWidget_Changed(object sender, RoutedEventArgs e)
    {
        SettingsStore.Current.ShowWidgetOnStartup = StartWidgetChk.IsChecked == true;
        SettingsStore.Save();
    }

    private void Topmost_Changed(object sender, RoutedEventArgs e)
    {
        SettingsStore.Current.WidgetTopmost = TopmostChk.IsChecked == true;
        SettingsStore.Save();
    }
}
