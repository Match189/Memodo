using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memodo.Windows.Views;

/// <summary>
/// 图钉（蓝图 §16 品牌元素）：2.5D 钉帽（径向渐变+高光）+ 针杆。
/// 代码构建，供 Board 卡与桌面组件复用；不依赖图片资源。
/// </summary>
public static class PinFactory
{
    public static readonly string[] Colors = { "red", "yellow", "blue", "green" };

    public static Color Resolve(string? name) => name switch
    {
        "yellow" => Color.FromRgb(0xE6, 0xB4, 0x22),
        "blue"   => Color.FromRgb(0x2F, 0x7F, 0xD6),
        "green"  => Color.FromRgb(0x3E, 0xA6, 0x5B),
        _        => Color.FromRgb(0xD6, 0x45, 0x45), // red 默认
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

        // 钉帽
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

        return root;
    }
}
