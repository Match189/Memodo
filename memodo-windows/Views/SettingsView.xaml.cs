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

            // 同步
            foreach (ComboBoxItem it in ProviderBox.Items)
                if ((string)it.Tag == s.SyncProvider) { ProviderBox.SelectedItem = it; break; }
            ProviderBox.SelectedItem ??= ProviderBox.Items[0];
            WebDavUrlBox.Text = string.IsNullOrEmpty(s.WebDavUrl)
                ? "https://dav.jianguoyun.com/dav/" : s.WebDavUrl;
            WebDavUserBox.Text = s.WebDavUser;
            if (SecretProtector.Unprotect(s.WebDavPassProtected) is { Length: > 0 } savedPwd)
                WebDavPwdBox.Password = savedPwd;
            UpdateSyncStatus(null);

            // 外观
            DarkChk.IsChecked = s.ThemeDark;
            foreach (ComboBoxItem it in ThemeBox.Items)
                if ((string)it.Tag == s.ThemeStyle) { ThemeBox.SelectedItem = it; break; }

            // 桌面组件
            StartWidgetChk.IsChecked = s.ShowWidgetOnStartup;
            TopmostChk.IsChecked = s.WidgetTopmost;
        };
    }

    // ---------- 同步 ----------
    private void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderBox.SelectedItem is not ComboBoxItem it) return;
        var provider = (string)it.Tag;
        SettingsStore.Current.SyncProvider = provider;
        SettingsStore.Save();
        WebDavPanel.Visibility = provider == "webdav" ? Visibility.Visible : Visibility.Collapsed;
        ServerPanel.Visibility = provider == "server" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveWebDav()
    {
        var s = SettingsStore.Current;
        s.SyncProvider = "webdav";
        s.WebDavUrl = WebDavUrlBox.Text.Trim();
        s.WebDavUser = WebDavUserBox.Text.Trim();
        if (WebDavPwdBox.Password.Length > 0)
            s.WebDavPassProtected = SecretProtector.Protect(WebDavPwdBox.Password);
        SettingsStore.Save();
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsStore.Current;
        if (s.SyncProvider == "webdav")
        {
            SaveWebDav();
            if (string.IsNullOrWhiteSpace(s.WebDavUrl) || string.IsNullOrWhiteSpace(s.WebDavUser))
            {
                UpdateSyncStatus("请先填写 WebDAV 地址与账号"); return;
            }
            UpdateSyncStatus("同步中…");
            var (tasks, memos, err) = await Engine.RunWebDavAsync();
            UpdateSyncStatus(err is null
                ? $"同步完成 ✓  云端共 {tasks} 条待办 / {memos} 条备忘"
                : "同步失败：" + err);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(s.ServerUrl))
            {
                UpdateSyncStatus("请先填写服务器地址"); return;
            }
            Sync.ServerUrl = s.ServerUrl;
            UpdateSyncStatus("同步中…");
            var (pulled, pushed, err) = await Engine.RunAsync(PwdBox.Password);
            UpdateSyncStatus(err is null
                ? $"同步完成：推送 {pushed} 条，拉取 {pulled} 条"
                : "同步失败：" + err);
        }
    }

    private void UpdateSyncStatus(string? message)
    {
        var last = SettingsStore.Current.LastSyncAt;
        var lastText = last > 0
            ? $"上次同步：{DateTimeOffset.FromUnixTimeMilliseconds(last).ToLocalTime():yyyy-MM-dd HH:mm}"
            : "尚未同步";
        SyncStatus.Text = message is null ? lastText : $"{message}（{lastText}）";
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsStore.Current;
        s.ServerUrl = ServerUrlBox.Text.Trim();
        s.AccountEmail = EmailBox.Text.Trim();
        SettingsStore.Save();
        Sync.ServerUrl = s.ServerUrl;
        UpdateSyncStatus("登录中…");
        var (ok, err) = await Sync.LoginAsync(s.AccountEmail, PwdBox.Password);
        UpdateSyncStatus(ok ? "登录成功 ✓" : "登录失败：" + err);
    }

    // ---------- 外观 ----------
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

    // ---------- 桌面组件 ----------
    private void StartWidget_Changed(object sender, RoutedEventArgs e)
    {
        SettingsStore.Current.ShowWidgetOnStartup = StartWidgetChk.IsChecked == true;
        SettingsStore.Save();
        // 启动项本身要重启才体现，但立即把置顶等打到已开组件
        App.Tray?.ApplyWidgetSettings();
    }

    private void Topmost_Changed(object sender, RoutedEventArgs e)
    {
        SettingsStore.Current.WidgetTopmost = TopmostChk.IsChecked == true;
        SettingsStore.Save();
        App.Tray?.ApplyWidgetSettings(); // 立即生效到已打开的组件
    }

    // ---------- 数据 ----------
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
