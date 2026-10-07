using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AvaMovieMaker.Controls;

public sealed class Glyph : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<Glyph, string>(nameof(Kind), string.Empty);

    /// <summary>Set on a container whose background stays dark in both themes, such as the toolbar, so glyphs inside use their light colors.</summary>
    public static readonly AttachedProperty<bool> OnDarkProperty = AvaloniaProperty.RegisterAttached<Glyph, Control, bool>("OnDark", inherits: true);

    static Glyph()
    {
        AffectsRender<Glyph>(KindProperty, IsEffectivelyEnabledProperty, OnDarkProperty);
        AffectsMeasure<Glyph>(KindProperty);
    }

    public Glyph() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public static bool GetOnDark(Control control) => control.GetValue(OnDarkProperty);

    public static void SetOnDark(Control control, bool value) => control.SetValue(OnDarkProperty, value);

    private enum Hue
    {
        Blue,
        Green,
        Orange,
        Gold,
        Red,
        Gray,
        Purple,
    }

    // Top and bottom gradient stops for light and dark backgrounds; every stop keeps 3:1 contrast against the
    // surfaces glyphs sit on in its theme.
    private static readonly Dictionary<Hue, (uint Top, uint Bottom, uint DarkTop, uint DarkBottom)> Hues = new()
    {
        [Hue.Blue] = (0x3272CC, 0x1A4AA8, 0xA4CCFF, 0x74ACF6),
        [Hue.Green] = (0x2E8424, 0x1D6614, 0xA2E48E, 0x62C44E),
        [Hue.Orange] = (0xB05E06, 0x844404, 0xFFC878, 0xF2A040),
        [Hue.Gold] = (0x9A6E00, 0x704E00, 0xFFE27A, 0xEBBE2C),
        [Hue.Red] = (0xD0483A, 0x992418, 0xFFA698, 0xF6867A),
        [Hue.Gray] = (0x66728A, 0x3C4658, 0xD6DCE6, 0xA4AEBE),
        [Hue.Purple] = (0x8656C6, 0x56308C, 0xD2B6F6, 0xB896EA),
    };

    private static readonly Dictionary<string, (string Icon, Hue Hue)> Icons = new()
    {
        ["import"] = ("video-add-line", Hue.Green),
        ["undo"] = ("arrow-go-back-line", Hue.Blue),
        ["redo"] = ("arrow-go-forward-line", Hue.Blue),
        ["automovie"] = ("magic-line", Hue.Gold),
        ["publish"] = ("share-forward-box-line", Hue.Green),
        ["tasks"] = ("task-line", Hue.Blue),
        ["collections"] = ("folder-video-line", Hue.Orange),
        ["view-options"] = ("layout-grid-line", Hue.Blue),
        ["split"] = ("scissors-cut-line", Hue.Gray),
        ["audio-from-video"] = ("film-line", Hue.Blue),
        ["audio-music"] = ("music-2-line", Hue.Purple),
        ["wizard-back"] = ("arrow-left-circle-line", Hue.Blue),
        ["computer"] = ("computer-line", Hue.Gray),
        ["narrate"] = ("mic-line", Hue.Red),
        ["audio-levels"] = ("volume-up-line", Hue.Blue),
        ["dvd"] = ("album-line", Hue.Purple),
        ["cd"] = ("album-line", Hue.Gray),
        ["email"] = ("mail-line", Hue.Gold),
        ["camera"] = ("vidicon-line", Hue.Gray),
        ["zoom-in"] = ("zoom-in-line", Hue.Blue),
        ["zoom-out"] = ("zoom-out-line", Hue.Blue),
        ["align-left"] = ("align-left", Hue.Gray),
        ["align-center"] = ("align-center", Hue.Gray),
        ["align-right"] = ("align-right", Hue.Gray),
        ["expand"] = ("add-box-line", Hue.Blue),
        ["collapse"] = ("checkbox-indeterminate-line", Hue.Blue),
        ["transitions"] = ("swap-box-line", Hue.Green),
        ["app"] = ("movie-2-fill", Hue.Blue),
    };

    private static readonly Dictionary<string, Geometry> Geometries = new();

    private static readonly Color Blue = Color.FromRgb(0x1E, 0x4F, 0xC0);
    private static readonly Color LightBlue = Color.FromRgb(0x5E, 0x9B, 0xF0);

    private static IBrush EffectBox => Bands((0, 0x7686A4), (0.4, 0x6C7E9F), (0.6, 0x8A9CBB), (1, 0x8298B9));
    private static Pen EffectBoxPen => new(new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0x80)));
    private static IBrush EffectStar => Vertical(Colors.White, Color.FromRgb(0xE4, 0xE4, 0xE6));
    private static Pen EffectStarPen => new(new SolidColorBrush(Color.FromRgb(0x48, 0x5E, 0x7C)), 0.75);

    protected override Size MeasureOverride(Size availableSize) => SizeOf(Kind);

    private static Size SizeOf(string kind) => kind switch
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

    private static IBrush Fill(Hue hue, bool dark)
    {
        (uint top, uint bottom, uint darkTop, uint darkBottom) = Hues[hue];
        return Vertical(Color.FromUInt32(0xFF000000 | (dark ? darkTop : top)), Color.FromUInt32(0xFF000000 | (dark ? darkBottom : bottom)));
    }

    private static Geometry Icon(string name)
    {
        if (!Geometries.TryGetValue(name, out Geometry? g))
        {
            g = Geometry.Parse(GlyphPaths.Remix[name]);
            Geometries[name] = g;
        }

        return g;
    }

    // Draws a 24-unit icon scaled into the given box.
    private static void DrawIcon(DrawingContext ctx, string name, Rect box, IBrush fill, IPen? pen = null)
    {
        using DrawingContext.PushedState t = ctx.PushTransform(Matrix.CreateScale(box.Width / 24, box.Height / 24) * Matrix.CreateTranslation(box.X, box.Y));
        ctx.DrawGeometry(fill, pen, Icon(name));
    }

    private static bool IsDark(Control host) => GetOnDark(host) || host.ActualThemeVariant == ThemeVariant.Dark;

    /// <summary>Draws a glyph at its natural size for a control that renders glyphs itself, using that control's theme.</summary>
    internal static void Draw(DrawingContext ctx, string kind, Control host) => DrawKind(ctx, kind, IsDark(host));

    public override void Render(DrawingContext ctx)
    {
        double a = IsEffectivelyEnabled ? 1 : 0.4;
        using DrawingContext.PushedState o = ctx.PushOpacity(a);
        DrawKind(ctx, Kind, IsDark(this));
    }

    private static void DrawKind(DrawingContext ctx, string kind, bool dark)
    {
        if (Icons.TryGetValue(kind, out (string Icon, Hue Hue) icon))
        {
            DrawIcon(ctx, icon.Icon, new Rect(SizeOf(kind)), Fill(icon.Hue, dark));
            return;
        }

        switch (kind)
        {
            case "font-grow":
            case "font-shrink":
                DrawIcon(ctx, "text", new Rect(-2.5, 2, 14, 14), Fill(Hue.Blue, dark));
                DrawIcon(ctx, kind == "font-grow" ? "arrow-up-s-fill" : "arrow-down-s-fill", new Rect(5.5, kind == "font-grow" ? -2 : -3, 12, 12), Fill(Hue.Blue, dark));
                return;

            case "drop-arrow":
                DrawIcon(ctx, "arrow-down-s-fill", new Rect(-3, 2, 13, 13), Fill(Hue.Gray, dark));
                return;

            case "star-multi":
                ctx.DrawRectangle(EffectBox, EffectBoxPen, new Rect(1.5, 1.5, 22, 22), 1, 1);
                DrawIcon(ctx, "sparkling-fill", new Rect(2, 2, 21, 21), EffectStar, EffectStarPen);
                return;

            case "star":
            case "star-on":
            {
                bool on = kind == "star-on";
                ctx.DrawRectangle(on ? EffectBox : Vertical(Color.FromRgb(0xFC, 0xFC, 0xFC), Color.FromRgb(0xE0, 0xE2, 0xE6)),
                    on ? EffectBoxPen : new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0xAC, 0xB4))), new Rect(1.5, 1.5, 22, 22), 1, 1);
                DrawIcon(ctx, "star-fill", new Rect(2, 2, 21, 21), on ? EffectStar : Vertical(Colors.White, Color.FromRgb(0xF0, 0xF0, 0xF0)),
                    on ? EffectStarPen : new Pen(new SolidColorBrush(Color.FromRgb(0x9C, 0xA0, 0xA8)), 0.75));
                return;
            }

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
                if (kind == "play-round")
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
                bool next = kind == "frame-next";
                var fill = Vertical(LightBlue, Blue);
                ctx.DrawGeometry(fill, null, Geometry.Parse(next ? "M11,6 L18,11 L11,16 Z" : "M17,6 L10,11 L17,16 Z"));
                ctx.DrawRectangle(fill, null, next ? new Rect(19, 6, 2.5, 10) : new Rect(6.5, 6, 2.5, 10));
                break;
            }

            case "rewind":
                ctx.DrawRectangle(Fill(Hue.Blue, dark), null, new Rect(3, 4, 2, 10));
                ctx.DrawGeometry(Fill(Hue.Blue, dark), null, Geometry.Parse("M14,4 L6,9 L14,14 Z"));
                break;
            case "play":
                ctx.DrawGeometry(Fill(Hue.Blue, dark), null, Geometry.Parse("M5,3 L15,9 L5,15 Z"));
                break;
            case "pause":
                ctx.DrawRectangle(Fill(Hue.Blue, dark), null, new Rect(4, 3, 3.5, 12));
                ctx.DrawRectangle(Fill(Hue.Blue, dark), null, new Rect(10.5, 3, 3.5, 12));
                break;
            case "menu-play":
            case "menu-stop":
            case "menu-rewind":
            case "menu-prev":
            case "menu-next":
            {
                IBrush ink = new SolidColorBrush(dark ? Color.FromRgb(0xA4, 0xAE, 0xBE) : Color.FromRgb(0x5A, 0x6E, 0x96));
                string g = kind switch
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
