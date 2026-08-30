using System.Windows;
using System.Windows.Media;

namespace Memodo.Windows.Services;

/// <summary>
/// 设计系统（蓝图 §13-§17/§39/§62）：Cork + Glass + Paper + Pin。
/// Cork=空间感(软木底) / Glass=现代感(毛玻璃面板) / Paper=内容(纸卡) / Pin=品牌。
/// 三主题 × 深/浅 通过运行时覆盖 Application.Resources 实现，XAML 一律用 DynamicResource。
/// </summary>
public enum ThemeStyle { Cork, Glass, Hybrid }

public static class ThemeService
{
    /// <summary>主题切换事件：板面纹理等需重绘的地方订阅。</summary>
    public static event Action? ThemeChanged;

    public static ThemeStyle Style
    {
        get => Enum.TryParse<ThemeStyle>(SettingsStore.Current.ThemeStyle, out var s) ? s : ThemeStyle.Hybrid;
        set { SettingsStore.Current.ThemeStyle = value.ToString(); SettingsStore.Save(); }
    }

    public static bool Dark
    {
        get => SettingsStore.Current.ThemeDark;
        set { SettingsStore.Current.ThemeDark = value; SettingsStore.Save(); }
    }

    public static void Apply() => Apply(Style, Dark);

    /// <summary>
    /// 板面调色（设计文档 Corkboard：135° 软木渐变 #C9A66B→#A88950 + 8% 噪点 + 内阴影）。
    /// </summary>
    public static (Color Base, Color Alt, Color Noise, Color Vignette) BoardPalette(ThemeStyle style, bool dark)
    {
        return style switch
        {
            ThemeStyle.Glass => dark
                ? (Col("14181C"), Col("14181C"), Col(0x22, "FFFFFF"), Col(0x99, "000000"))
                : (Col("DFE7EC"), Col("DFE7EC"), Col(0x22, "FFFFFF"), Col(0x33, "546E7A")),
            _ => dark
                ? (Col("2B211A"), Col("241B15"), Col(0x1A, "4A3826"), Col(0x88, "000000"))
                : (Col("C9A66B"), Col("A88950"), Col(0x0F, "000000"), Col(0x2E, "000000")),
        };
    }

    /// <summary>组件材质着色（setSurface GradientColor 用，ABGR 组装在调用方）。</summary>
    public static Color SurfaceTint => Dark ? Col("14181C") : Col("FDFBFA");

    public static void Apply(ThemeStyle style, bool dark)
    {
        Style = style; Dark = dark;
        var r = Application.Current.Resources;

        // ---- Cork 底（空间感）----
        var (board, boardAlt) = style switch
        {
            ThemeStyle.Cork   => dark ? (Col("5C4430"), Col("4A3626")) : (Col("D9B38C"), Col("C9A176")),
            ThemeStyle.Glass  => dark ? (Col("1E2429"), Col("242B31")) : (Col("EAF3F6"), Col("DDEAEE")),
            _                 => dark ? (Col("4A3B2C"), Col("3D3125")) : (Col("E8D5BC"), Col("DCC5A6")), // Hybrid
        };
        r["BoardBackground"] = new SolidColorBrush(board);
        r["BoardBackgroundAlt"] = new SolidColorBrush(boardAlt);

        // ---- Paper 卡（内容）----
        var (card, cardBorder, textPri, textSec) = dark
            ? (Col("33302B"), Col("4A453E"), Col("ECE7DF"), Col("A8A199"))
            : (Col("FFFFFF"), Col("E2DDD3"), Col("2C2820"), Col("7A756A")); // 设计文档：暖纸白 + 细边
        r["CardSurface"] = new SolidColorBrush(card);
        r["CardBorder"] = new SolidColorBrush(cardBorder);
        r["TextPrimary"] = new SolidColorBrush(textPri);
        r["TextSecondary"] = new SolidColorBrush(textSec);

        // ---- Apple 灰阶令牌（DESIGN_APPLE.md §1.1）----
        r["Background"] = new SolidColorBrush(dark ? Col("1C1C1E") : Col("F2F2F7"));
        r["Surface"] = new SolidColorBrush(dark ? Col("2C2C2E") : Col("FFFFFF"));
        r["SurfaceElevated"] = new SolidColorBrush(dark ? Col("3A3A3C") : Col("FFFFFF"));
        r["Separator"] = new SolidColorBrush(dark ? Col(0x99, "545458") : Col(0x1F, "3C3C43"));
        r["Label"] = new SolidColorBrush(dark ? Col("FFFFFF") : Col("1C1C1E"));
        r["SecondaryLabel"] = new SolidColorBrush(dark ? Col(0x99, "EBEBF5") : Col(0x99, "3C3C43"));
        r["TertiaryLabel"] = new SolidColorBrush(dark ? Col(0x4D, "EBEBF5") : Col(0x4D, "3C3C43"));
        r["Fill"] = new SolidColorBrush(dark ? Col(0x3D, "767680") : Col(0x1F, "767680"));
        r["CheckRing"] = new SolidColorBrush(dark ? Col("55555A") : Col("C7C7CC"));
        r["AccentSoft"] = new SolidColorBrush(dark ? Col(0x2E, "E89A62") : Col(0x1F, "D4763B"));

        // ---- 品牌与语义（暖橙 tint + iOS 红）----
        r["Accent"] = new SolidColorBrush(dark ? Col("E89A62") : Col("D4763B"));
        r["AppBackground"] = new SolidColorBrush(dark ? Col("1C1C1E") : Col("F2F2F7"));
        r["SidebarBackground"] = new SolidColorBrush(dark ? Col("222226") : Col("ECECF1"));
        r["SubtleText"] = r["TextSecondary"];
        r["Danger"] = new SolidColorBrush(dark ? Col("FF453A") : Col("FF3B30"));

        ThemeChanged?.Invoke();
    }

    private static Color Col(string hex)
    {
        var v = System.Convert.ToUInt32(hex, 16);
        return Color.FromArgb(0xFF, (byte)(v >> 16 & 0xFF), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF));
    }

    private static Color Col(byte a, string hex)
    {
        var v = System.Convert.ToUInt32(hex, 16);
        return Color.FromArgb(a, (byte)(v >> 16 & 0xFF), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF));
    }
}
