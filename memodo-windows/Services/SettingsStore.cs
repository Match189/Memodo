using System.IO;
using System.Text.Json;
using System.Windows;

namespace Memodo.Windows.Services;

/// <summary>本地设置（同步地址/账号/主题/桌面组件开关等），JSON 持久化到 AppData。</summary>
public sealed class AppSettings
{
    public string ServerUrl { get; set; } = "";
    public string AccountEmail { get; set; } = "";
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
    /// <summary>组件内卡片布局（本机视觉状态，不进同步协议——蓝图 §11 平台分离）。</summary>
    public Dictionary<string, WidgetCardPos> WidgetLayouts { get; set; } = new();
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
}
