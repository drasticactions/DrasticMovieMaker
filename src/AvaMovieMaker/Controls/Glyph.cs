using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvaMovieMaker.Controls;

public sealed class Glyph : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<Glyph, string>(nameof(Kind), string.Empty);

    static Glyph()
    {
        AffectsRender<Glyph>(KindProperty, IsEffectivelyEnabledProperty);
        AffectsMeasure<Glyph>(KindProperty);
    }

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static readonly HashSet<string> Pictures =
    [
        "import", "undo", "redo", "automovie", "publish", "tasks", "collections", "view-options", "split",
        "audio-from-video", "audio-music", "wizard-back", "computer", "narrate", "audio-levels", "dvd", "cd", "email", "camera", "app",
    ];

    private static readonly int[] PictureSizes = [16, 24, 32, 48, 64];
    private static readonly Dictionary<(string Kind, int Size), Bitmap> PictureCache = new();

    private static readonly Color Blue = Color.FromRgb(0x1E, 0x4F, 0xC0);
    private static readonly Color LightBlue = Color.FromRgb(0x5E, 0x9B, 0xF0);

    private static IBrush EffectBox => Bands((0, 0x7686A4), (0.4, 0x6C7E9F), (0.6, 0x8A9CBB), (1, 0x8298B9));
    private static Pen EffectBoxPen => new(new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0x80)));
    private static IBrush EffectStar => Vertical(Colors.White, Color.FromRgb(0xE4, 0xE4, 0xE6));
    private static Pen EffectStarPen => new(new SolidColorBrush(Color.FromRgb(0x48, 0x5E, 0x7C)), 1);

    protected override Size MeasureOverride(Size availableSize) => Kind switch
    {
        "play-round" or "pause-round" => new Size(35, 43),
        "frame-prev" or "frame-next" => new Size(28, 22),
        "zoom-in" or "zoom-out" or "rewind" or "play" or "pause" => new Size(18, 18),
        "audio-from-video" or "audio-music" or "computer" or "dvd" or "cd" or "email" or "camera" or "app" => new Size(32, 32),
        "wizard-back" => new Size(24, 24),
        "star" or "star-on" or "star-multi" => new Size(25, 25),
        "drop-arrow" => new Size(7, 16),
        _ => new Size(16, 16),
    };

    private static IBrush Vertical(Color top, Color bottom) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, 1) },
    };

    private static IBrush Bands(params (double At, uint Rgb)[] stops)
    {
        var b = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative) };
        foreach ((double at, uint rgb) in stops)
        {
            b.GradientStops.Add(new GradientStop(Color.FromUInt32(0xFF000000 | rgb), at));
        }

        return b;
    }

    private static Geometry Star(double cx, double cy, double r, double ri, double tilt = 0)
    {
        var g = new StreamGeometry();
        using (StreamGeometryContext s = g.Open())
        {
            for (int i = 0; i < 10; i++)
            {
                double a = (i * 36 + tilt) * Math.PI / 180, d = i % 2 == 0 ? r : ri;
                var p = new Point(cx + d * Math.Sin(a), cy - d * Math.Cos(a));
                if (i == 0)
                {
                    s.BeginFigure(p, true);
                }
                else
                {
                    s.LineTo(p);
                }
            }

            s.EndFigure(true);
        }

        return g;
    }

    internal static Bitmap Picture(string kind, double pixels)
    {
        int size = PictureSizes.FirstOrDefault(s => s >= pixels, PictureSizes[^1]);
        if (!PictureCache.TryGetValue((kind, size), out Bitmap? b))
        {
            using Stream s = AssetLoader.Open(new Uri($"avares://AvaMovieMaker/Assets/Icons/{kind}-{size}.png"));
            b = new Bitmap(s);
            PictureCache[(kind, size)] = b;
        }

        return b;
    }

    public override void Render(DrawingContext ctx)
    {
        double a = IsEffectivelyEnabled ? 1 : 0.4;
        using DrawingContext.PushedState o = ctx.PushOpacity(a);
        if (Pictures.Contains(Kind))
        {
            Size size = MeasureOverride(default);
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Bitmap b = Picture(Kind, Math.Max(size.Width, size.Height) * scale);
            using DrawingContext.PushedState q = ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality });
            ctx.DrawImage(b, new Rect(b.Size), new Rect(size));
            return;
        }

        switch (Kind)
        {
            case "play-round":
            case "pause-round":
            {
                var c = new Point(17.5, 21.5);
                ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xEC, 0xF1, 0xF9)), new Pen(new SolidColorBrush(Color.FromRgb(0xDA, 0xDE, 0xE5))), c, 20, 20);
                ctx.DrawEllipse(Bands((0, 0xF0F0FA), (0.06, 0xD8D8F0), (0.12, 0xB0B2DE), (0.2, 0x7C86CE), (0.28, 0x566CC3), (0.44, 0x3A5CBC), (0.47, 0x0A2DA9), (0.5, 0x0014A0),
                        (0.54, 0x0024A4), (0.61, 0x0045BB), (0.69, 0x0064D8), (0.77, 0x1A96F0), (0.84, 0x36BAF8), (0.9, 0x5EDCFF), (0.96, 0x84F0FF), (1, 0x60A8E0)),
                    new Pen(Bands((0, 0x8C9AD6), (0.35, 0x4058C0), (0.5, 0x0008A0), (0.85, 0x2048A8), (1, 0x485898))), c, 17.2, 17.2);
                using (ctx.PushClip(new Rect(0, 0, 35, 20.5)))
                {
                    ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(0xA0, 0xB0, 0xCC, 0xF4)), 1.4), c, 15.8, 15.8);
                }

                var face = Bands((0, 0xFFFFFF), (0.4, 0xFAFAFA), (0.47, 0xF4F4F4), (0.6, 0xDEDEDE), (0.73, 0xC8C8C8), (0.86, 0xB4B4B4), (1, 0xA8A8A8));
                if (Kind == "play-round")
                {
                    ctx.DrawGeometry(face, null, Geometry.Parse("M13,13.8 L26.3,21.5 L13,29.1 Z"));
                }
                else
                {
                    ctx.DrawRectangle(face, null, new Rect(11.5, 14, 4.5, 15), 1, 1);
                    ctx.DrawRectangle(face, null, new Rect(19, 14, 4.5, 15), 1, 1);
                }

                break;
            }

            case "frame-prev":
            case "frame-next":
            {
                bool next = Kind == "frame-next";
                var fill = Vertical(LightBlue, Blue);
                ctx.DrawGeometry(fill, null, Geometry.Parse(next ? "M11,6 L18,11 L11,16 Z" : "M17,6 L10,11 L17,16 Z"));
                ctx.DrawRectangle(fill, null, next ? new Rect(19, 6, 2.5, 10) : new Rect(6.5, 6, 2.5, 10));
                break;
            }

            case "rewind":
                ctx.DrawRectangle(new SolidColorBrush(Blue), null, new Rect(3, 4, 2, 10));
                ctx.DrawGeometry(new SolidColorBrush(Blue), null, Geometry.Parse("M14,4 L6,9 L14,14 Z"));
                break;
            case "play":
                ctx.DrawGeometry(new SolidColorBrush(Blue), null, Geometry.Parse("M5,3 L15,9 L5,15 Z"));
                break;
            case "pause":
                ctx.DrawRectangle(new SolidColorBrush(Blue), null, new Rect(4, 3, 3.5, 12));
                ctx.DrawRectangle(new SolidColorBrush(Blue), null, new Rect(10.5, 3, 3.5, 12));
                break;
            case "zoom-in":
            case "zoom-out":
            {
                var pen = new Pen(new SolidColorBrush(Blue), 2);
                ctx.DrawEllipse(Brushes.White, pen, new Point(10, 7.5), 5.5, 5.5);
                ctx.DrawLine(new Pen(new SolidColorBrush(Blue), 3, lineCap: PenLineCap.Round), new Point(5.5, 12), new Point(2, 15.5));
                ctx.DrawLine(new Pen(new SolidColorBrush(Blue), 1.8), new Point(7.5, 7.5), new Point(12.5, 7.5));
                if (Kind == "zoom-in")
                {
                    ctx.DrawLine(new Pen(new SolidColorBrush(Blue), 1.8), new Point(10, 5), new Point(10, 10));
                }

                break;
            }

            case "expand":
            case "collapse":
            {
                ctx.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Blue)), new Rect(2.5, 2.5, 10, 10));
                ctx.DrawLine(new Pen(new SolidColorBrush(Blue), 1.4), new Point(5, 7.5), new Point(10, 7.5));
                if (Kind == "expand")
                {
                    ctx.DrawLine(new Pen(new SolidColorBrush(Blue), 1.4), new Point(7.5, 5), new Point(7.5, 10));
                }

                break;
            }

            case "star-multi":
            {
                ctx.DrawRectangle(EffectBox, EffectBoxPen, new Rect(1.5, 1.5, 22, 22), 1, 1);
                ctx.DrawGeometry(EffectStar, EffectStarPen, Star(9.9, 11, 8, 4.1));
                ctx.DrawGeometry(EffectStar, EffectStarPen, Star(16.3, 16.6, 5.7, 2.9, -6));
                break;
            }

            case "star":
            case "star-on":
            {
                bool on = Kind == "star-on";
                ctx.DrawRectangle(on ? EffectBox : Vertical(Color.FromRgb(0xFC, 0xFC, 0xFC), Color.FromRgb(0xE0, 0xE2, 0xE6)),
                    on ? EffectBoxPen : new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0xAC, 0xB4))), new Rect(1.5, 1.5, 22, 22), 1, 1);
                ctx.DrawGeometry(on ? EffectStar : Vertical(Colors.White, Color.FromRgb(0xF0, 0xF0, 0xF0)),
                    on ? EffectStarPen : new Pen(new SolidColorBrush(Color.FromRgb(0x9C, 0xA0, 0xA8)), 1), Star(12, 12, 9, 4.6));
                break;
            }
            case "font-grow":
            case "font-shrink":
            {
                bool grow = Kind == "font-grow";
                var ink = new SolidColorBrush(Color.FromRgb(0x1E, 0x3C, 0x8C));
                ctx.DrawGeometry(null, new Pen(ink, 1.6), Geometry.Parse(grow ? "M1,14 L5.5,3 L10,14 M3,10 L8,10" : "M2,14 L5.5,6 L9,14 M3.6,11 L7.4,11"));
                ctx.DrawGeometry(ink, null, Geometry.Parse(grow ? "M11,6 L13.5,2 L16,6 Z" : "M11,2 L16,2 L13.5,6 Z"));
                break;
            }

            case "align-left":
            case "align-center":
            case "align-right":
            {
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)));
                double[] widths = [12, 8, 12, 8, 12];
                for (int i = 0; i < widths.Length; i++)
                {
                    double w = widths[i];
                    double x = Kind switch { "align-left" => 2, "align-right" => 14 - w, _ => 8 - w / 2 };
                    ctx.DrawLine(pen, new Point(x, 3.5 + i * 2.5), new Point(x + w, 3.5 + i * 2.5));
                }

                break;
            }

            case "drop-arrow":
                ctx.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)), null, Geometry.Parse("M0,7 L7,7 L3.5,10.5 Z"));
                break;

            case "transitions":
                ctx.DrawRectangle(Vertical(Color.FromRgb(0x6C, 0xD4, 0x5C), Color.FromRgb(0x1E, 0x8C, 0x2A)), new Pen(new SolidColorBrush(Color.FromRgb(0x16, 0x5E, 0x1E))), new Rect(1.5, 1.5, 13, 13));
                ctx.DrawGeometry(Brushes.White, null, Geometry.Parse("M5.5,4 L11.5,8 L5.5,12 Z"));
                break;

            case "menu-play":
            case "menu-stop":
            case "menu-rewind":
            case "menu-prev":
            case "menu-next":
            {
                IBrush ink = new SolidColorBrush(Color.FromRgb(0x5A, 0x6E, 0x96));
                string g = Kind switch
                {
                    "menu-play" => "M4,3 L12,8 L4,13 Z",
                    "menu-stop" => "M4,4 L12,4 L12,12 L4,12 Z",
                    "menu-rewind" => "M3,3 L5,3 L5,13 L3,13 Z M13,3 L13,13 L6,8 Z",
                    "menu-prev" => "M3,3 L5,3 L5,13 L3,13 Z M12,4 L12,12 L6,8 Z",
                    _ => "M11,3 L13,3 L13,13 L11,13 Z M4,4 L10,8 L4,12 Z",
                };
                ctx.DrawGeometry(ink, null, Geometry.Parse(g));
                break;
            }
        }
    }
}
