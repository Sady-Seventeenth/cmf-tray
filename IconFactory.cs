using System.Drawing;
using System.Drawing.Drawing2D;

namespace CmfTray;

/// <summary>Draws the tray icon. A few styles to choose from; monochrome so it reads on the taskbar.</summary>
internal static class IconFactory
{
    public enum Style { Earbud, Headphone, DotRing, BudWaves }

    // Nothing/CMF earbuds logo (embedded so no external file is needed at runtime). viewBox 0 0 800 800.
    private const string SvgPath1 = "M170.244 157.028C241.553 111.281 330.501 122.737 368.915 182.615C402.984 235.72 385.86 309.075 331.807 356.904V602.965C331.807 640.52 301.362 670.965 263.807 670.965H260.807C223.251 670.965 192.807 640.52 192.807 602.965V445.253C128.134 467.231 95.611 439.561 72.1201 410.356C18.9422 344.245 70.8721 279.009 94.9688 265.426C101.958 224.402 128.424 183.857 170.244 157.028Z";
    private const string SvgPath2 = "M430.383 182.614C468.797 122.736 557.745 111.281 629.054 157.028C670.874 183.857 697.339 224.402 704.328 265.426C728.424 279.008 780.356 344.244 727.178 410.356C703.687 439.561 671.164 467.231 606.491 445.253V602.965C606.491 640.52 576.046 670.965 538.491 670.965H535.491C497.936 670.965 467.491 640.52 467.491 602.965V356.904C413.437 309.075 396.314 235.719 430.383 182.614Z";

    /// <summary>Render the embedded Nothing/CMF earbuds SVG to a tray icon via WPF.</summary>
    public static Icon MakeFromSvg(bool connected, int size = 32)
    {
        var geo = new System.Windows.Media.GeometryGroup();
        geo.Children.Add(System.Windows.Media.Geometry.Parse(SvgPath1));
        geo.Children.Add(System.Windows.Media.Geometry.Parse(SvgPath2));

        var color = connected
            ? System.Windows.Media.Colors.White
            : System.Windows.Media.Color.FromRgb(150, 150, 150);
        var brush = new System.Windows.Media.SolidColorBrush(color);

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            double scale = size / 800.0;
            dc.PushTransform(new System.Windows.Media.ScaleTransform(scale, scale));
            dc.DrawGeometry(brush, null, geo);
            dc.Pop();
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);

        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var ms = new System.IO.MemoryStream();
        enc.Save(ms);
        ms.Position = 0;
        using var gdi = new Bitmap(ms);
        return Icon.FromHandle(gdi.GetHicon());
    }

    public static Icon Make(Style style, bool connected, int size = 32)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var color = connected ? Color.White : Color.FromArgb(150, 150, 150);
            Draw(style, g, size, color);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static void Draw(Style style, Graphics g, int s, Color color)
    {
        float k = s / 32f;
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 2.6f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        switch (style)
        {
            case Style.Earbud:
                // Bud + stem silhouette.
                g.FillEllipse(brush, 6 * k, 3 * k, 15 * k, 15 * k);
                using (var stem = Rounded(11.5f * k, 14 * k, 6 * k, 15 * k, 3 * k))
                    g.FillPath(brush, stem);
                break;

            case Style.Headphone:
                // Headband arc + two ear cups.
                g.DrawArc(pen, 6.5f * k, 7 * k, 19 * k, 19 * k, 180, 180);
                using (var l = Rounded(4.5f * k, 15.5f * k, 6 * k, 11 * k, 2.5f * k)) g.FillPath(brush, l);
                using (var r = Rounded(21.5f * k, 15.5f * k, 6 * k, 11 * k, 2.5f * k)) g.FillPath(brush, r);
                break;

            case Style.DotRing:
                // Nothing/CMF dot-matrix style ring, with a centre dot.
                float cx = 16 * k, cy = 16 * k, radius = 11 * k, dot = 1.9f * k;
                for (int i = 0; i < 12; i++)
                {
                    double a = i * Math.PI / 6;
                    float x = cx + (float)Math.Cos(a) * radius;
                    float y = cy + (float)Math.Sin(a) * radius;
                    g.FillEllipse(brush, x - dot, y - dot, dot * 2, dot * 2);
                }
                g.FillEllipse(brush, cx - 2.2f * k, cy - 2.2f * k, 4.4f * k, 4.4f * k);
                break;

            case Style.BudWaves:
                // A bud with sound waves.
                g.FillEllipse(brush, 6 * k, 11 * k, 11 * k, 11 * k);
                using (var wp = new Pen(color, 2.2f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawArc(wp, 12 * k, 8 * k, 10 * k, 16 * k, -55, 110);
                    g.DrawArc(wp, 15 * k, 5 * k, 13 * k, 22 * k, -50, 100);
                }
                break;
        }
    }

    private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        float d = r * 2;
        var p = new GraphicsPath();
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
