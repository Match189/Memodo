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
    public string Theme { get; set; } = "System"; // System | Light | Dark
    public bool WidgetTopmost { get; set; } = true;
    public long LastPullCursor { get; set; } = 0;
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
