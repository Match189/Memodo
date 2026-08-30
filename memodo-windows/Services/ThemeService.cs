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
    /// 板面调色（Flutter board_theme.dart 移植）：底/噪点/暗角，Hybrid 沿用 Cork 底。
    /// </summary>
    public static (Color Base, Color Alt, Color Noise, Color Vignette) BoardPalette(ThemeStyle style, bool dark)
    {
        return style switch
        {
            ThemeStyle.Glass => dark
                ? (Col("14181C"), Col("14181C"), Col(0x22, "FFFFFF"), Col(0x99, "000000"))
                : (Col("DFE7EC"), Col("DFE7EC"), Col(0x22, "FFFFFF"), Col(0x33, "546E7A")),
            _ => dark
                ? (Col("2B211A"), Col("241B15"), Col(0x26, "4A3826"), Col(0x88, "000000"))
                : (Col("D9B38C"), Col("C9A176"), Col(0x33, "A97C50"), Col(0x55, "6B4A2F")),
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
            : (Col("FDF8EC"), Col("E2D6C2"), Col("33302B"), Col("8A8070")); // Muted Paper（暗色下仍保纸感）
        r["CardSurface"] = new SolidColorBrush(card);
        r["CardBorder"] = new SolidColorBrush(cardBorder);
        r["TextPrimary"] = new SolidColorBrush(textPri);
        r["TextSecondary"] = new SolidColorBrush(textSec);

        // ---- Glass 面板（现代感：工具栏/侧栏/弹层）----
        r["GlassPanel"] = new SolidColorBrush(dark ? Col("262D33") : Col("F2FBFD"));
        r["GlassBorder"] = new SolidColorBrush(dark ? Col("3A444C") : Col("BFE0E8"));

        // ---- 品牌与语义 ----
        r["Accent"] = new SolidColorBrush(dark ? Col("3FB6D4") : Col("0080A0"));
        r["AppBackground"] = new SolidColorBrush(dark ? Col("20262B") : Col("F5F7F8"));
        r["SidebarBackground"] = new SolidColorBrush(dark ? Col("2A3138") : Col("EAF1F3"));
        r["SubtleText"] = r["TextSecondary"];
        r["Danger"] = new SolidColorBrush(dark ? Col("FF6B6B") : Col("B00020"));

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
