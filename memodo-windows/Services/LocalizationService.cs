using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Memodo.Windows.Services;

/// <summary>
/// 国际化服务：从 locales/*.json 加载字符串，支持热切换。
/// 字符串以 S_* 键写入 Application.Resources，XAML 用 DynamicResource（切换即时生效），
/// 代码菜单用 T(key)。语言偏好存 SettingsStore，不进同步协议。
/// </summary>
public static class LocalizationService
{
    public static event Action? LanguageChanged;

    private static Dictionary<string, string> _strings = new();
    private static readonly Dictionary<string, Dictionary<string, string>> _cache = new();

    /// <summary>当前语言代码（zh / en / ...）。</summary>
    public static string Lang
    {
        get
        {
            var lang = SettingsStore.Current.Language;
            if (string.IsNullOrEmpty(lang))
            {
                lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                    ? "zh" : "en";
                SettingsStore.Current.Language = lang;
                SettingsStore.Save();
            }
            return lang;
        }
        set { SettingsStore.Current.Language = value; SettingsStore.Save(); }
    }

    /// <summary>可用语言列表（扫描 locales/ 目录）。</summary>
    public static IReadOnlyList<string> AvailableLanguages => GetAvailableLanguages();

    /// <summary>代码取串：T("nav_todo")。</summary>
    public static string T(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;

    /// <summary>加载并应用指定语言。</summary>
    public static void Apply()
    {
        var lang = Lang;
        _strings = LoadLanguage(lang);

        var r = Application.Current.Resources;
        foreach (var kv in _strings)
        {
            r["S_" + kv.Key] = kv.Value;
        }

        LanguageChanged?.Invoke();
    }

    /// <summary>加载指定语言的 JSON 文件，带缓存。</summary>
    private static Dictionary<string, string> LoadLanguage(string lang)
    {
        if (_cache.TryGetValue(lang, out var cached))
            return cached;

        var json = ReadLocaleFile(lang);
        if (json == null)
            return new Dictionary<string, string>();

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        _cache[lang] = dict;
        return dict;
    }

    /// <summary>从嵌入资源或文件系统读取 locale JSON。
    /// 外置 locales/ 优先（可覆盖翻译），缺失时回退到程序集嵌入资源
    /// （单文件发布下不带 locales/ 文件夹也能正常显示文字）。</summary>
    private static string? ReadLocaleFile(string lang)
    {
        // 优先从应用程序目录下的 locales/ 读取
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(appDir, "locales", $"{lang}.json");
        if (File.Exists(path))
            return File.ReadAllText(path);

        // 回退：程序集嵌入资源（csproj 以 <Resource Include="locales\*.json" /> 嵌入，
        // WPF 将其收入 g.resources 容器，键形如 "locales/en.json"）
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var container = assembly.GetManifestResourceStream("Memodo.Windows.g.resources");
            if (container != null)
            {
                using var reader = new System.Resources.ResourceReader(container);
                foreach (System.Collections.DictionaryEntry entry in reader)
                {
                    var key = entry.Key as string;
                    if (key != null && key.Replace('\\', '/').Equals($"locales/{lang}.json", StringComparison.OrdinalIgnoreCase)
                        && entry.Value is System.IO.Stream bytes)
                        using (var sr = new StreamReader(bytes))
                            return sr.ReadToEnd();
                }
            }
        }
        catch { }

        // 回退：尝试从项目根目录的 locales/ 读取（开发环境）
        var projectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "locales", $"{lang}.json");
        if (File.Exists(projectPath))
            return File.ReadAllText(projectPath);

        return null;
    }

    /// <summary>扫描 locales/ 目录获取所有可用语言。</summary>
    private static IReadOnlyList<string> GetAvailableLanguages()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localesDir = Path.Combine(appDir, "locales");
        if (!Directory.Exists(localesDir))
            return new[] { "zh", "en" }; // 默认

        var files = Directory.GetFiles(localesDir, "*.json");
        var langs = new List<string>();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrEmpty(name))
                langs.Add(name);
        }
        return langs.Count > 0 ? langs.ToArray() : new[] { "zh", "en" };
    }

    /// <summary>清除缓存（语言文件更新后调用）。</summary>
    public static void ClearCache() => _cache.Clear();
}
