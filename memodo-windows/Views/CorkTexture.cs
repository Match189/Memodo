using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>
/// 板面背景（Flutter board_background.dart 移植）：
/// 底色渐变 + 固定种子低频噪点（Random(20260829)，重建不闪烁）+ 四角暗角。
/// 无图片资产，Viewbox 缩放与分辨率无关。
/// </summary>
public static class CorkTexture
{
    public static FrameworkElement Create(ThemeStyle style, bool dark)
    {
        var (baseC, altC, noise, vignette) = ThemeService.BoardPalette(style, dark);

        var root = new Grid();

        // 底色渐变（左上→右下，base → lerp(base, vignette, .25)）
        root.Children.Add(new Border
        {
            Background = new LinearGradientBrush(baseC, Lerp(baseC, vignette, 0.25), 45),
        });

        // 低频噪点：固定种子；设计画布 1000x1000，数量与面积比例换算后 Viewbox 拉伸
        var canvas = new Canvas { Width = 1000, Height = 1000 };
        var rng = new Random(20260829);
        for (var i = 0; i < 600; i++)
        {
            var dx = rng.NextDouble() * 1000;
            var dy = rng.NextDouble() * 1000;
            var r = 1.0 + rng.NextDouble() * 2.2;
            var dot = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(noise),
            };
            Canvas.SetLeft(dot, dx);
            Canvas.SetTop(dot, dy);
            canvas.Children.Add(dot);
        }
        root.Children.Add(new Viewbox { Child = canvas, Stretch = Stretch.UniformToFill });

        // 四角暗角
        root.Children.Add(new Border
        {
            Background = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0),
                    new GradientStop(Colors.Transparent, 0.55),
                    new GradientStop(vignette, 1),
                },
            },
        });

        return root;
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)(a.A + (b.A - a.A) * t),
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));
}
