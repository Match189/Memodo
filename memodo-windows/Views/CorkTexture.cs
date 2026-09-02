using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>
/// 板面背景 v2（语言中立的程序化纹理，无图片资产，Viewbox 缩放与分辨率无关）：
/// 四段对角渐变 + 两处柔光池 + 软木颗粒斑 + 细砂噪点 + 图钉点阵 + 四角暗角。
/// 固定种子（20260829）保证重建不闪烁。自定义背景图仍优先（用户裁定 #7）。
/// </summary>
public static class CorkTexture
{
    private sealed record Palette(Color[] Stops, Color Pool, Color Fleck, Color Grid, Color Vignette, Color Noise);

    /// <param name="bgPath">自定义背景图（用户裁定 #7）；空=程序化软木/玻璃纹理。</param>
    /// <param name="bgStretch">壁纸缩放模式。</param>
    public static FrameworkElement Create(ThemeStyle style, bool dark, string bgPath = "", string bgStretch = "UniformToFill")
    {
        var (_, _, noiseFallback, vignette) = ThemeService.BoardPalette(style, dark);
        var p = PaletteFor(style, dark, vignette, noiseFallback);

        var root = new Grid();

        // 自定义背景图（UniformToFill + 压暗层保证便签对比度）
        if (!string.IsNullOrEmpty(bgPath) && File.Exists(bgPath))
        {
            try
            {
                // 用 FileStream 避免 Uri 对非 ASCII 路径的兼容问题
                BitmapImage bmp;
                using (var fs = new FileStream(bgPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = fs;
                    bmp.EndInit();
                }
                bmp.Freeze();
                var stretchMode = bgStretch switch
                {
                    "Uniform" => Stretch.Uniform,
                    "Stretch" => Stretch.Fill,
                    "None" => Stretch.None,
                    _ => Stretch.UniformToFill,
                };
                root.Children.Add(new Image
                {
                    Source = bmp,
                    Stretch = stretchMode,
                });
                root.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(
                        (byte)(dark ? 0x66 : 0x40), 0, 0, 0)),
                });
                root.Children.Add(Vignette(p.Vignette));
                return root;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CorkTexture] bg load FAILED: {bgPath} — {ex.Message}"); }
        }

        // ---- 1. 四段对角渐变（左上亮 → 右下深）----
        root.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(p.Stops[0], 0.0),
                    new GradientStop(p.Stops[1], 0.42),
                    new GradientStop(p.Stops[2], 0.72),
                    new GradientStop(p.Stops[3], 1.0),
                },
            },
        });

        var canvas = new Canvas { Width = 1000, Height = 1000 };
        var rng = new Random(20260829);

        // ---- 2. 柔光池：左上主光 + 右下补光（径向渐变模拟柔焦，无 BlurEffect 开销）----
        canvas.Children.Add(Pool(620, 460, -140, -130, p.Pool));
        canvas.Children.Add(Pool(520, 400, 640, 700, Color.FromArgb((byte)(p.Pool.A / 2), p.Pool.R, p.Pool.G, p.Pool.B)));

        // ---- 3. 软木颗粒斑：亮斑随机铺（极淡）；暗斑只落四角固定位+抖动（远离主光池，消除污渍感）----
        (double, double)[] corners = { (70, 70), (930, 90), (80, 930), (920, 910) };
        var darkIdx = 0;
        for (var i = 0; i < 14; i++)
        {
            var fr = 40 + rng.NextDouble() * 60;
            if (i % 2 == 0)
            {
                var fx = rng.NextDouble() * 1000;
                var fy = rng.NextDouble() * 1000;
                canvas.Children.Add(Pool(fr * 2, fr * 2, fx - fr, fy - fr, p.Pool));
            }
            else
            {
                var (cx, cy) = corners[darkIdx++ % corners.Length];
                var fx = cx + (rng.NextDouble() - 0.5) * 120;
                var fy = cy + (rng.NextDouble() - 0.5) * 120;
                var c = Color.FromArgb((byte)(p.Fleck.A * 0.35), p.Fleck.R, p.Fleck.G, p.Fleck.B);
                canvas.Children.Add(Pool(fr * 2, fr * 2, fx - fr, fy - fr, c));
            }
        }

        // ---- 4. 细砂噪点（尺寸与透明度双重随机）----
        for (var i = 0; i < 600; i++)
        {
            var dx = rng.NextDouble() * 1000;
            var dy = rng.NextDouble() * 1000;
            var r = 0.9 + rng.NextDouble() * 1.7;
            var a = 0.05 + rng.NextDouble() * 0.11;
            var dot = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(p.Noise.A * a / 0.15 > 255 ? 255 : p.Noise.A * a / 0.15),
                    p.Noise.R, p.Noise.G, p.Noise.B)),
            };
            Canvas.SetLeft(dot, dx);
            Canvas.SetTop(dot, dy);
            canvas.Children.Add(dot);
        }

        // ---- 5. 图钉点阵：50px 间距的微点，含蓄暗示钉位网格 ----
        for (var gx = 25; gx < 1000; gx += 50)
        {
            for (var gy = 25; gy < 1000; gy += 50)
            {
                var gdot = new Ellipse
                {
                    Width = 2.2, Height = 2.2,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)(p.Grid.A * 0.55), p.Grid.R, p.Grid.G, p.Grid.B)),
                };
                Canvas.SetLeft(gdot, gx - 1.1);
                Canvas.SetTop(gdot, gy - 1.1);
                canvas.Children.Add(gdot);
            }
        }

        root.Children.Add(new Viewbox { Child = canvas, Stretch = Stretch.UniformToFill });

        // ---- 6. 四角暗角 ----
        root.Children.Add(Vignette(p.Vignette));

        return root;
    }

    private static Border Vignette(Color vignette) => new()
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
    };

    /// <summary>柔光池/颗粒斑：径向渐变椭圆（中心不透明→边缘透明）。</summary>
    private static Ellipse Pool(double w, double h, double x, double y, Color c) => new()
    {
        Width = w, Height = h,
        Fill = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(c, 0),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
            },
        },
    };

    private static Palette PaletteFor(ThemeStyle style, bool dark, Color vignette, Color noiseFallback) =>
        style switch
        {
            ThemeStyle.Glass => dark
                ? new Palette(
                    new[] { Col("242C33"), Col("1E252B"), Col("191F24"), Col("141A1F") },
                    Col("24AFD4E8"), Col("10FFFFFF"), Col("12FFFFFF"), vignette, Col(0x20, "AFD4E8"))
                : new Palette(
                    new[] { Col("F4F9FB"), Col("EAF2F5"), Col("DDEBEF"), Col("CFE0E5") },
                    Col("55FFFFFF"), Col("0D546E7A"), Col("0A546E7A"), vignette, Col(0x22, "FFFFFF")),
            _ => dark
                ? new Palette(
                    new[] { Col("41321F"), Col("372A1C"), Col("2D2317"), Col("241C11") },
                    Col("2AFFE0B8"), Col("12FFFFFF"), Col("12FFFFFF"), vignette, Col(0x28, "6B5233"))
                : new Palette(
                    new[] { Col("F7EDD9"), Col("EAD3AC"), Col("D9B38C"), Col("C9A176") },
                    Col("66FFF3DC"), Col("146B4A2F"), Col("0F6B4A2F"), vignette, Col(0x22, "000000")),
        };

    private static Color Col(string hex)
    {
        var v = Convert.ToUInt32(hex, 16);
        return Color.FromArgb(0xFF, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    private static Color Col(byte a, string hex)
    {
        var v = Convert.ToUInt32(hex, 16);
        return Color.FromArgb(a, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }
}
