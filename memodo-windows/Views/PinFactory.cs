using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memodo.Windows.Views;

/// <summary>
/// 图钉（蓝图 §16 品牌元素 × 设计文档配色）：
/// 四色图钉 = 分类（红/蓝/绿/黄），钉帽=圆形带高光+内阴影+投影，压在便签顶部。
/// </summary>
public static class PinFactory
{
    public static readonly string[] Colors = { "red", "blue", "green", "yellow" };

    public static Color Resolve(string? name) => name switch
    {
        "blue"   => Color.FromRgb(0x4A, 0x90, 0xE2),
        "green"  => Color.FromRgb(0x5C, 0xB8, 0x5C),
        "yellow" => Color.FromRgb(0xF0, 0xAD, 0x4E),
        _        => Color.FromRgb(0xE8, 0x5A, 0x4F), // red 默认
    };

    /// <summary>便签纸色（设计文档 5 色）：yellow/pink/blue/green/orange。</summary>
    public static readonly string[] NoteColors = { "yellow", "pink", "blue", "green", "orange" };

    public static Color ResolveNote(string? name) => name switch
    {
        "pink"   => Color.FromRgb(0xFC, 0xE4, 0xEC),
        "blue"   => Color.FromRgb(0xE3, 0xF2, 0xFD),
        "green"  => Color.FromRgb(0xE8, 0xF5, 0xE9),
        "orange" => Color.FromRgb(0xFF, 0xF3, 0xE0),
        "yellow" => Color.FromRgb(0xFF, 0xF9, 0xC4),
        _        => default, // 空 = 跟随主题纸面
    };

    /// <param name="size">钉帽直径(px)</param>
    public static UIElement Create(string? colorName, double size = 16)
    {
        var c = Resolve(colorName);
        var light = Color.FromRgb(
            (byte)Math.Min(255, c.R + 70), (byte)Math.Min(255, c.G + 70), (byte)Math.Min(255, c.B + 70));
        var dark = Color.FromRgb((byte)(c.R * 0.55), (byte)(c.G * 0.55), (byte)(c.B * 0.55));

        var root = new Grid
        {
            Width = size,
            Height = size * 1.55,
            IsHitTestVisible = false, // 不挡卡片拖拽
        };

        // 针杆
        var needle = new System.Windows.Shapes.Rectangle
        {
            Width = Math.Max(1.6, size * 0.12),
            Height = size * 0.62,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x9A, 0x9A, 0x9A), Color.FromRgb(0x5E, 0x5E, 0x5E), 90),
            RadiusX = 1, RadiusY = 1,
        };
        root.Children.Add(needle);

        // 钉帽：径向渐变 + 顶部高光小圆（设计文档 flat pin + inset highlight）
        var head = new System.Windows.Shapes.Ellipse
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Top,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4, ShadowDepth = 1.5, Opacity = 0.35,
            },
        };
        head.Fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.3),
            Center = new Point(0.5, 0.5),
            GradientStops =
            {
                new GradientStop(light, 0),
                new GradientStop(c, 0.55),
                new GradientStop(dark, 1),
            },
        };
        root.Children.Add(head);

        var hl = new System.Windows.Shapes.Ellipse
        {
            Width = size * 0.32, Height = size * 0.32,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(size * 0.16, size * 0.12, 0, 0),
            Fill = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
        };
        root.Children.Add(hl);

        return root;
    }
}
