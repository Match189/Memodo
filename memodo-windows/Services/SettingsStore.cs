using System.IO;
using System.Text.Json;
using System.Windows;

namespace Memodo.Windows.Services;

/// <summary>本地设置（同步地址/账号/主题/桌面组件开关等），JSON 持久化到 AppData。</summary>
public sealed class AppSettings
{
    public string ServerUrl { get; set; } = "";
    public string AccountEmail { get; set; } = "";

    // ---- 同步（蓝图 §41-§45）----
    public string SyncProvider { get; set; } = "webdav"; // webdav | server
    public string WebDavUrl { get; set; } = "https://dav.jianguoyun.com/dav/";
    public string WebDavUser { get; set; } = "";
    /// <summary>DPAPI(CurrentUser) 加密后的应用密码；settings.json 中不落明文（蓝图 §53）。</summary>
    public string WebDavPassProtected { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public long LastSyncAt { get; set; } = 0;

    public bool ShowWidgetOnStartup { get; set; } = true;
    public string ThemeStyle { get; set; } = "Hybrid"; // Cork | Glass | Hybrid（蓝图 §17）
    public bool ThemeDark { get; set; } = false;
    public bool WidgetTopmost { get; set; } = true;
    public long LastPullCursor { get; set; } = 0;
    public double WidgetX { get; set; } = -1;  // -1 = 未记录，走系统默认
    public double WidgetY { get; set; } = -1;
    public double WidgetW { get; set; } = 380;
    public double WidgetH { get; set; } = 520;
    public bool WidgetLocked { get; set; } = false;
    /// <summary>组件材质不透明度 30-100（Flutter Phase 2 移植）。</summary>
    public int WidgetOpacity { get; set; } = 90;
    /// <summary>组件毛玻璃（BLURBEHIND）；false=透明渐变。</summary>
    public bool WidgetAcrylic { get; set; } = true;
    /// <summary>附着桌面层（WorkerW，实验特性，Flutter Phase 3 移植）。</summary>
    public bool WidgetAttachDesktop { get; set; } = false;
    /// <summary>自动同步（启动时 + 每 3 分钟，仅 WebDAV 通道）。</summary>
    public bool AutoSync { get; set; } = true;
    /// <summary>自动同步间隔（分钟，用户可设）。</summary>
    public int AutoSyncIntervalMinutes { get; set; } = 3;
    /// <summary>界面语言：zh / en（用户裁定：双语可切换）。</summary>
    public string Language { get; set; } = "zh";
    /// <summary>组件显示方式（蓝图：钉板 / 传统列表 可切换）。</summary>
    public string WidgetViewMode { get; set; } = "board"; // board | list
    /// <summary>主窗口显示方式：传统列表 / 钉板画布。</summary>
    public string MainViewMode { get; set; } = "list"; // list | board
    /// <summary>组件内卡片布局（本机视觉状态，不进同步协议——蓝图 §11 平台分离）。</summary>
    public Dictionary<string, WidgetCardPos> WidgetLayouts { get; set; } = new();
    /// <summary>组件钉板自定义背景图（本机视觉，不进同步协议；空=软木纹理）。</summary>
    public string BoardBgPath { get; set; } = "";

    /// <summary>设备标识（LWW 平局决胜，§19/§47）；首次访问自动生成。</summary>
    public string EnsureDeviceId()
    {
        if (string.IsNullOrEmpty(DeviceId))
        {
            DeviceId = "win-" + Guid.NewGuid().ToString("N")[..8];
            SettingsStore.Save();
        }
        return DeviceId;
    }
}

public static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "app.memodo", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var txt = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppSettings>(txt);
                if (s is not null) return s;
            }
        }
        catch { /* 损坏则用默认 */ }
        return new AppSettings();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 忽略写入失败 */ }
    }
}

/// <summary>桌面组件内单张卡片的摆位（本机 kv）。</summary>
public sealed class WidgetCardPos
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 150;
    public double H { get; set; } = 96;
    /// <summary>便签纸色（按类型色系默认，可逐条设置）。</summary>
    public string NoteColor { get; set; } = "";
    /// <summary>便签不透明度 0.3-1.0（可逐条设置）。</summary>
    public double NoteOpacity { get; set; } = 1.0;
}

/// <summary>机密保护（蓝图 §53）：DPAPI CurrentUser，settings.json 不落明文。</summary>
public static class SecretProtector
{
    public static string Protect(string plain)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plain);
        return Convert.ToBase64String(
            System.Security.Cryptography.ProtectedData.Protect(bytes, null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser));
    }

    public static string Unprotect(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try
        {
            var bytes = System.Security.Cryptography.ProtectedData.Unprotect(
                Convert.FromBase64String(encrypted), null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }
}
