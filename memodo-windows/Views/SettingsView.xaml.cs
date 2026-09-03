using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

public partial class SettingsView : UserControl
{
    private bool _ready;
    private SyncService Sync => AppHost.Services.GetRequiredService<SyncService>();
    private SyncEngine Engine => AppHost.Services.GetRequiredService<SyncEngine>();

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var s = SettingsStore.Current;
            _ready = false; // 页面缓存后 Loaded 会再次触发，回填期间屏蔽事件处理器

            // 语言选项：动态扫描 locales/ 目录
            LangBox.Items.Clear();
            var langNames = new Dictionary<string, string> { { "zh", "中文" }, { "en", "English" } };
            foreach (var lang in LocalizationService.AvailableLanguages)
            {
                var display = langNames.TryGetValue(lang, out var name) ? name : lang.ToUpper();
                LangBox.Items.Add(new ComboBoxItem { Content = display, Tag = lang });
            }
            foreach (ComboBoxItem it in LangBox.Items)
                if ((string)it.Tag == s.Language) { LangBox.SelectedItem = it; break; }
            LangBox.SelectedItem ??= LangBox.Items[0];

            // 同步
            foreach (ComboBoxItem it in ProviderBox.Items)
                if ((string)it.Tag == s.SyncProvider) { ProviderBox.SelectedItem = it; break; }
            ProviderBox.SelectedItem ??= ProviderBox.Items[0];
            WebDavUrlBox.Text = string.IsNullOrEmpty(s.WebDavUrl)
                ? "https://dav.jianguoyun.com/dav/" : s.WebDavUrl;
            WebDavUserBox.Text = s.WebDavUser;
            if (SecretProtector.Unprotect(s.WebDavPassProtected) is { Length: > 0 } savedPwd)
                WebDavPwdBox.Password = savedPwd;
            AutoSyncChk.IsChecked = s.AutoSync;
            // 同步间隔：回填已保存值（手输数字不触发 SelectionChanged，直接设 Text）
            IntervalBox.Text = s.AutoSyncIntervalMinutes.ToString();
            // 服务器同步：回填已保存的地址/账号/密码（与 WebDAV 一致，否则重启全空）
            ServerUrlBox.Text = s.ServerUrl;
            EmailBox.Text = s.AccountEmail;
            if (SecretProtector.Unprotect(s.ServerPassProtected) is { Length: > 0 } savedServerPwd)
                PwdBox.Password = savedServerPwd;
            // E2EE 口令回填 + 状态标注
            SyncPassBox.Password = s.SyncPassphrase;
            UpdateSyncPassState();
            UpdateSyncStatus(null);

            // 外观
            DarkChk.IsChecked = s.ThemeDark;
            foreach (ComboBoxItem it in ThemeBox.Items)
                if ((string)it.Tag == s.ThemeStyle) { ThemeBox.SelectedItem = it; break; }

            // 桌面组件
            StartWidgetChk.IsChecked = s.ShowWidgetOnStartup;
            TopmostChk.IsChecked = s.WidgetTopmost;
            OpacitySlider.Value = s.WidgetOpacity;
            OpacityValue.Text = s.WidgetOpacity + "%";
            foreach (ComboBoxItem it in TimeFormatBox.Items)
                if ((string)it.Tag == s.TimeFormat) { TimeFormatBox.SelectedItem = it; break; }
            ApplyAboutPath();
            foreach (ComboBoxItem it in BgStretchBox.Items)
                if ((string)it.Tag == s.BoardBgStretch) { BgStretchBox.SelectedItem = it; break; }
            BgPathText.Text = s.BoardBgPath;
            RefreshBgPreview();
            _ready = true;
        };
        // 关于卡路径行是代码拼接的（覆盖了 DynamicResource），语言热切换后需重取词
        LocalizationService.LanguageChanged += ApplyAboutPath;
        ApplyAboutHomepage();
    }

    /// <summary>关于卡「数据库路径」行。</summary>
    private void ApplyAboutPath() =>
        AboutPath.Text = LocalizationService.T("about_db") + ": " + System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "app.memodo");

    /// <summary>关于卡「项目主页」行（语言热切换时重取词）。</summary>
    private void ApplyAboutHomepage() =>
        AboutHomepage.Text = LocalizationService.T("about_homepage") + ": " + ProjectHome.Url;

    private void AboutHomepage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = ProjectHome.Url,
            UseShellExecute = true,
        });

    private void Lang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || LangBox.SelectedItem is not ComboBoxItem it) return;
        SettingsStore.Current.Language = (string)it.Tag;
        SettingsStore.Save();
        LocalizationService.Apply();     // DynamicResource 文本即时切换；代码菜单经事件重建
        App.NotifyDataChanged();         // 触发列表/组件刷新
    }

    private void TimeFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || TimeFormatBox.SelectedItem is not ComboBoxItem it) return;
        SettingsStore.Current.TimeFormat = (string)it.Tag;
        SettingsStore.Save();
        App.NotifyDataChanged();         // 刷新列表/钉板时间显示
    }

    /// <summary>只允许数字输入。</summary>
    private void IntervalBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        foreach (var ch in e.Text)
            if (!char.IsDigit(ch)) { e.Handled = true; return; }
    }

    /// <summary>禁止粘贴非数字内容。</summary>
    private void IntervalBox_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        var text = e.SourceDataObject.GetData(typeof(string)) as string;
        if (text == null || !text.All(char.IsDigit)) e.CancelCommand();
    }

    /// <summary>保存间隔设置（合法性校验后落盘并重启定时器）。</summary>
    private void SaveInterval()
    {
        var text = IntervalBox.Text?.Trim();
        if (!int.TryParse(text, out var minutes) || minutes < 1 || minutes > 120) return;
        if (minutes == SettingsStore.Current.AutoSyncIntervalMinutes) return;
        SettingsStore.Current.AutoSyncIntervalMinutes = minutes;
        SettingsStore.Current.AutoSyncIntervalUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SettingsStore.Save();
        App.RestartAutoSyncTimer();
    }

    private void Interval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        SaveInterval();
    }

    /// <summary>手输数字不会触发 SelectionChanged：失焦时校验保存（重启后仍保留）。</summary>
    private void IntervalBox_LostFocus(object sender, RoutedEventArgs e) => SaveInterval();

    /// <summary>输入框内回车立即保存。</summary>
    private void IntervalBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) SaveInterval();
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

    /// <summary>保存服务器同步信息（地址/账号/密码 DPAPI 加密），与 SaveWebDav 对齐。</summary>
    private void SaveServer()
    {
        var s = SettingsStore.Current;
        s.SyncProvider = "server";
        s.ServerUrl = ServerUrlBox.Text.Trim();
        s.AccountEmail = EmailBox.Text.Trim();
        if (PwdBox.Password.Length > 0)
            s.ServerPassProtected = SecretProtector.Protect(PwdBox.Password);
        SettingsStore.Save();
    }

    /// <summary>服务器输入框失焦即保存（避免只填不点按钮导致重启丢失）。</summary>
    private void ServerField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_ready || SettingsStore.Current.SyncProvider != "server") return;
        SaveServer();
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsStore.Current;
        if (s.SyncProvider == "webdav")
        {
            SaveWebDav();
            if (string.IsNullOrWhiteSpace(s.WebDavUrl) || string.IsNullOrWhiteSpace(s.WebDavUser))
            {
                UpdateSyncStatus(LocalizationService.T("sync_fill_webdav_first")); return;
            }
            UpdateSyncStatus(LocalizationService.T("syncing"));
            var (tasks, memos, err) = await Engine.RunWebDavAsync();
            UpdateSyncStatus(err is null
                ? string.Format(LocalizationService.T("sync_ok_webdav_detail"), tasks, memos)
                : string.Format(LocalizationService.T("sync_fail_detail"), err));
            if (err is null) App.NotifyDataChanged();
        }
        else
        {
            SaveServer(); // 立即同步前先落盘（与 WebDAV 分支一致）
            if (string.IsNullOrWhiteSpace(s.ServerUrl))
            {
                UpdateSyncStatus(LocalizationService.T("sync_fill_server_first")); return;
            }
            Sync.ServerUrl = s.ServerUrl;
            UpdateSyncStatus(LocalizationService.T("syncing"));
            var (pulled, pushed, err) = await Engine.RunAsync(PwdBox.Password);
            UpdateSyncStatus(err is null
                ? string.Format(LocalizationService.T("sync_ok_server_detail"), pushed, pulled)
                : string.Format(LocalizationService.T("sync_fail_detail"), err));
            if (err is null) App.NotifyDataChanged();
        }
    }

    private void UpdateSyncStatus(string? message)
    {
        var last = SettingsStore.Current.LastSyncAt;
        var lastText = last > 0
            ? LocalizationService.T("sync_last_time").Replace("{time}", DateTimeOffset.FromUnixTimeMilliseconds(last).ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            : LocalizationService.T("sync_not_configured");
        SyncStatus.Text = message is null ? lastText : $"{message} ({lastText})";
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        SaveServer();
        var s = SettingsStore.Current;
        Sync.ServerUrl = s.ServerUrl;
        UpdateSyncStatus(LocalizationService.T("sync_login_ing"));
        var (ok, err) = await Sync.LoginAsync(s.AccountEmail, PwdBox.Password);
        UpdateSyncStatus(ok ? LocalizationService.T("login_ok") : LocalizationService.T("login_fail") + err);
    }

    /// <summary>注册新账号（成功后自动登录）。</summary>
    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        SaveServer();
        var s = SettingsStore.Current;
        Sync.ServerUrl = s.ServerUrl;
        UpdateSyncStatus(LocalizationService.T("sync_login_ing"));
        var (ok, err) = await Sync.RegisterAsync(s.AccountEmail, PwdBox.Password);
        if (!ok) { UpdateSyncStatus(LocalizationService.T("login_fail") + err); return; }
        var (lok, lerr) = await Sync.LoginAsync(s.AccountEmail, PwdBox.Password);
        UpdateSyncStatus(lok ? LocalizationService.T("login_ok") : LocalizationService.T("login_fail") + lerr);
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

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValue is not null)
        {
            SettingsStore.Current.WidgetOpacity = (int)e.NewValue;
            OpacityValue.Text = ((int)e.NewValue).ToString() + "%";
            if (_ready) SettingsStore.Save();
            App.Tray?.ApplyWidgetSettings();
        }
    }

    private void AutoSync_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        SettingsStore.Current.AutoSync = AutoSyncChk.IsChecked == true;
        SettingsStore.Save();
    }

    private void ToggleWidget_Click(object sender, RoutedEventArgs e)
    {
        if (App.Tray?.WidgetWindow?.IsVisible == true)
        {
            App.Tray?.WidgetWindow?.Hide();
            ToggleWidgetBtn.Content = LocalizationService.T("widget_show");
        }
        else
        {
            App.Tray?.ShowWidget();
            ToggleWidgetBtn.Content = LocalizationService.T("widget_hide");
        }
    }

    // ---------- 数据 ----------
    /// 蓝图 §52：JSON 全量导出（第一版必须有，防数据锁死）
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"memodo-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            Filter = LocalizationService.T("file_filter_json"),
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = ExportService.ExportJson(AppHost.Services.GetRequiredService<Data.AppDatabase>());
            System.IO.File.WriteAllText(dlg.FileName, json);
            ExportStatus.Text = LocalizationService.T("export_ok").Replace("{path}", dlg.FileName);
        }
        catch (Exception ex)
        {
            ExportStatus.Text = LocalizationService.T("export_fail").Replace("{error}", ex.Message);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LocalizationService.T("file_filter_json"),
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = System.IO.File.ReadAllText(dlg.FileName);
            var taskRepo = AppHost.Services.GetRequiredService<TaskRepository>();
            var memoRepo = AppHost.Services.GetRequiredService<MemoRepository>();
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (data is null) { ExportStatus.Text = LocalizationService.T("import_fail").Replace("{error}", "empty data"); return; }
            int count = 0;
            if (data.TryGetValue("tasks", out var tasksEl) && tasksEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in tasksEl.EnumerateArray())
                {
                    var t = new TaskItem
                    {
                        Id = item.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                        Title = item.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "",
                        Description = item.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "",
                        Completed = item.TryGetProperty("completed", out var cEl) && cEl.GetInt32() != 0,
                        Priority = item.TryGetProperty("priority", out var pEl) ? pEl.GetInt32() : 0,
                        CreatedAt = item.TryGetProperty("created_at", out var caEl) ? caEl.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        UpdatedAt = item.TryGetProperty("updated_at", out var uaEl) ? uaEl.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    };
                    if (item.TryGetProperty("due_date", out var ddEl) && ddEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                        t.DueDate = ddEl.GetInt64();
                    if (item.TryGetProperty("deleted_at", out var delEl) && delEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                        t.DeletedAt = delEl.GetInt64();
                    if (item.TryGetProperty("archived_at", out var arcEl) && arcEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                        t.ArchivedAt = arcEl.GetInt64();
                    taskRepo.UpsertFromSync(t);
                    count++;
                }
            }
            if (data.TryGetValue("memos", out var memosEl) && memosEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in memosEl.EnumerateArray())
                {
                    var m = new MemoItem
                    {
                        Id = item.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                        Title = item.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "",
                        Content = item.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "",
                        Completed = item.TryGetProperty("completed", out var compEl) && compEl.ValueKind != System.Text.Json.JsonValueKind.Null && compEl.GetInt32() != 0,
                        ShowOnBoard = !item.TryGetProperty("show_on_board", out var sbEl) || sbEl.ValueKind == System.Text.Json.JsonValueKind.Null || sbEl.GetInt32() != 0,
                        CreatedAt = item.TryGetProperty("created_at", out var caEl) ? caEl.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        UpdatedAt = item.TryGetProperty("updated_at", out var uaEl) ? uaEl.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    };
                    if (item.TryGetProperty("deleted_at", out var delEl) && delEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                        m.DeletedAt = delEl.GetInt64();
                    if (item.TryGetProperty("archived_at", out var arcEl) && arcEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                        m.ArchivedAt = arcEl.GetInt64();
                    memoRepo.UpsertFromSync(m);
                    count++;
                }
            }
            ExportStatus.Text = LocalizationService.T("import_ok") + $" ({count})";
            App.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            ExportStatus.Text = LocalizationService.T("import_fail").Replace("{error}", ex.Message);
        }
    }

    // ---------- 钉板背景图 ----------
    /// <summary>预览画框：无图时隐藏，有图时按当前壁纸模式缩放。</summary>
    private void RefreshBgPreview()
    {
        var path = SettingsStore.Current.BoardBgPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            BgPreviewBorder.Visibility = Visibility.Collapsed;
            BgPreview.Source = null;
            return;
        }
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            BgPreview.Source = bmp;
            BgPreviewBorder.Visibility = Visibility.Visible;
        }
        catch { BgPreviewBorder.Visibility = Visibility.Collapsed; }
    }

    private void BgChoose_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.T("board_pick"),
            Filter = LocalizationService.T("file_filter_image"),
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "app.memodo");
            System.IO.Directory.CreateDirectory(dir);
            var dest = System.IO.Path.Combine(dir, "board-bg" + System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant());
            System.IO.File.Copy(dlg.FileName, dest, overwrite: true);
            SettingsStore.Current.BoardBgPath = dest;
            SettingsStore.Save();
            BgPathText.Text = dest;
            RefreshBgPreview();
            App.NotifyDataChanged();
            if (App.Tray?.WidgetWindow != null) App.Tray.WidgetWindow.ApplySettings();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(LocalizationService.T("bg_set_fail") + ex.Message, LocalizationService.T("app_title"));
        }
    }

    private void BgReset_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Current.BoardBgPath = "";
        SettingsStore.Save();
        BgPathText.Text = "";
        RefreshBgPreview();
        App.NotifyDataChanged();
        if (App.Tray?.WidgetWindow != null) App.Tray.WidgetWindow.ApplySettings();
    }

    private void BgStretch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || BgStretchBox.SelectedItem is not ComboBoxItem it) return;
        SettingsStore.Current.BoardBgStretch = (string)it.Tag;
        SettingsStore.Save();
        RefreshBgPreview();
        if (App.Tray?.WidgetWindow != null) App.Tray.WidgetWindow.ApplySettings();
    }

    // ---------- 端到端加密口令（两种通道共用） ----------
    private void SyncPass_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var s = SettingsStore.Current;
        s.SyncPassphraseProtected = SyncPassBox.Password.Length > 0
            ? SecretProtector.Protect(SyncPassBox.Password) : "";
        SettingsStore.Save();
        UpdateSyncPassState();
    }

    private void UpdateSyncPassState() =>
        SyncPassState.Text = SyncPassBox.Password.Length > 0
            ? LocalizationService.T("sync_passphrase_on")
            : LocalizationService.T("sync_passphrase_off");
}
