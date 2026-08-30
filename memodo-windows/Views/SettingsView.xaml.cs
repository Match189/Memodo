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
            DarkChk.IsChecked = s.ThemeDark;
            foreach (ComboBoxItem it in ThemeBox.Items)
                if ((string)it.Tag == s.ThemeStyle) { ThemeBox.SelectedItem = it; break; }
        };
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedItem is ComboBoxItem it)
        {
            ThemeService.Apply(System.Enum.TryParse<ThemeStyle>((string)it.Tag, out var st) ? st : ThemeStyle.Hybrid,
                               ThemeService.Dark);
        }
    }

    private void Dark_Changed(object sender, RoutedEventArgs e)
    {
        ThemeService.Dark = DarkChk.IsChecked == true;
        ThemeService.Apply();
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

    /// 蓝图 §52：JSON 全量导出（第一版必须有，防数据锁死）
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"memodo-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            Filter = "JSON 备份|*.json",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = ExportService.ExportJson(AppHost.Services.GetRequiredService<Data.AppDatabase>());
            System.IO.File.WriteAllText(dlg.FileName, json);
            ExportStatus.Text = "已导出：" + dlg.FileName;
        }
        catch (Exception ex)
        {
            ExportStatus.Text = "导出失败：" + ex.Message;
        }
    }
}
