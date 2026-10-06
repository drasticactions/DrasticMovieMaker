using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaMovieMaker.Converters;
using AvaMovieMaker.Effects;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Rendering.Plan;
using SkiaSharp;

namespace AvaMovieMaker.Controls;

public sealed class CatalogSample : Control
{
    public static readonly StyledProperty<string> ItemIdProperty = AvaloniaProperty.Register<CatalogSample, string>(nameof(ItemId), string.Empty);
    public static readonly StyledProperty<bool> IsTransitionProperty = AvaloniaProperty.Register<CatalogSample, bool>(nameof(IsTransition));

    private const int W = 192, H = 144;
    private static readonly ConcurrentDictionary<string, Task<Bitmap>> Cache = new(StringComparer.Ordinal);
    private static readonly Lazy<Task<RenderDevice>> Device = new(() => Task.Run(() => RenderDevice.Create(preferGpu: false)));
    private Bitmap? _image;

    static CatalogSample() => AffectsRender<CatalogSample>(ItemIdProperty, IsTransitionProperty);

    public string ItemId
    {
        get => GetValue(ItemIdProperty);
        set => SetValue(ItemIdProperty, value);
    }

    public bool IsTransition
    {
        get => GetValue(IsTransitionProperty);
        set => SetValue(IsTransitionProperty, value);
    }

    protected override async void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == ItemIdProperty || change.Property == IsTransitionProperty) && !string.IsNullOrEmpty(ItemId))
        {
            string id = ItemId;
            bool transition = IsTransition;
            string key = (transition ? "t:" : "e:") + id;
            _image = null;
            Bitmap bmp = await Cache.GetOrAdd(key, _ => RenderSampleAsync(id, transition));
            if (key == (IsTransition ? "t:" : "e:") + ItemId)
            {
                _image = bmp;
                Dispatcher.UIThread.Post(InvalidateVisual);
            }
        }
    }

    public static Bitmap? TryGet(string id, bool transition, Action ready)
    {
        Task<Bitmap> t = Cache.GetOrAdd((transition ? "t:" : "e:") + id, _ => RenderSampleAsync(id, transition));
        if (t.IsCompletedSuccessfully)
        {
            return t.Result;
        }

        t.ContinueWith(_ => Dispatcher.UIThread.Post(ready), TaskScheduler.Default);
        return null;
    }

    private static async Task<Bitmap> RenderSampleAsync(string id, bool transition)
    {
        RenderDevice dev = await Device.Value.ConfigureAwait(false);
        return await dev.Thread.InvokeAsync(() =>
        {
            var ctx = new RenderContext(dev, W, H);
            using SKImage a = transition ? Panel(ctx, new SKColor(0xF4, 0xF6, 0xF4), new SKColor(0xD8, 0xDC, 0xD8)) : Landscape(ctx);
            using SKImage b = Panel(ctx, new SKColor(0x7C, 0xD0, 0x6C), new SKColor(0x2E, 0x9A, 0x2E));
            EffectInfo? info = transition ? null : EffectCatalog.Find(id);
            SKImage result = transition
                ? EffectLibrary.Instance.ApplyTransition(ctx, a, b, new TransitionInstance(id, 0.5, 5, 1.25))
                : info is { Operation: "ease" or "pan" } ? MotionSample(ctx, a, info)
                : EffectLibrary.Instance.ApplyEffect(ctx, a, new EffectInstance(id, SampleTime(info), 5), 5);
            using SKBitmap bmp = SKBitmap.FromImage(result);
            if (!ReferenceEquals(result, a))
            {
                result.Dispose();
            }

            return SkBitmapConverter.ToBitmap(bmp.Copy());
        }).ConfigureAwait(false);
    }

    private static double SampleTime(EffectInfo? info) => info switch
    {
        { Operation: "fade" } f when f.Values.Length > 0 && f.Values[0] > 0 => 0.45,
        { Operation: "fade" } => 4.55,
        _ => 2.5,
    };

    private static SKImage MotionSample(RenderContext ctx, SKImage still, EffectInfo info) => ctx.Draw(c =>
    {
        c.DrawImage(still, 0, 0, SKSamplingOptions.Default);
        float[] v = info.Values;
        (float x0, float y0, float z0, float x1, float y1, float z1) = info.Operation == "ease"
            ? (v.Length > 0 && v[0] > 0 ? (0.5f, 0.5f, 1.6f, 0.5f, 0.5f, 1f) : (0.5f, 0.5f, 1f, 0.5f, 0.5f, 1.6f))
            : (v[0], v[1], v[2], v[3], v[4], v[5]);
        SKRect View(float x, float y, float z) => SKRect.Create(W * (x - 0.5f / z), H * (y - 0.5f / z), W / z, H / z);
        SKRect from = View(x0, y0, z0), to = View(x1, y1, z1);
        SKRect frame = z1 >= z0 ? to : from;
        frame.Inflate(-1, -1);
        using var wash = new SKPaint { Color = new SKColor(255, 255, 255, 110) };
        using (var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd })
        {
            builder.AddRect(SKRect.Create(0, 0, W, H));
            builder.AddRect(frame);
            using SKPath outside = builder.Detach();
            c.DrawPath(outside, wash);
        }

        using var pen = new SKPaint { Color = SKColors.White, IsStroke = true, StrokeWidth = 2, IsAntialias = true };
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 90), IsStroke = true, StrokeWidth = 4, IsAntialias = true };
        c.DrawRect(frame, shadow);
        c.DrawRect(frame, pen);
        void Arrow(SKPoint a, SKPoint b)
        {
            c.DrawLine(a, b, shadow);
            c.DrawLine(a, b, pen);
            SKPoint d = b - a;
            float len = MathF.Max(1, d.Length);
            var u = new SKPoint(d.X / len * 7, d.Y / len * 7);
            var n = new SKPoint(-u.Y * 0.6f, u.X * 0.6f);
            using var outline = new SKPathBuilder();
            outline.MoveTo(b);
            outline.LineTo(b - u + n);
            outline.LineTo(b - u - n);
            outline.Close();
            using SKPath head = outline.Detach();
            using var fill = new SKPaint { Color = SKColors.White, IsAntialias = true };
            c.DrawPath(head, fill);
        }

        if (Math.Abs(x1 - x0) + Math.Abs(y1 - y0) < 0.01f)
        {
            bool grows = z1 > z0 || (z1 == z0 && info.Operation != "ease");
            SKRect inner = frame, outer = SKRect.Create(2, 2, W - 4, H - 4);
            foreach ((SKPoint i, SKPoint o) in new[] { (new SKPoint(inner.Left, inner.Top), new SKPoint(outer.Left + 8, outer.Top + 8)), (new SKPoint(inner.Right, inner.Top), new SKPoint(outer.Right - 8, outer.Top + 8)), (new SKPoint(inner.Left, inner.Bottom), new SKPoint(outer.Left + 8, outer.Bottom - 8)), (new SKPoint(inner.Right, inner.Bottom), new SKPoint(outer.Right - 8, outer.Bottom - 8)) })
            {
                if (grows)
                {
                    Arrow(i, o);
                }
                else
                {
                    Arrow(o, i);
                }
            }
        }
        else
        {
            Arrow(new SKPoint(from.MidX, from.MidY), new SKPoint(to.MidX, to.MidY));
        }
    });

    private static SKImage Panel(RenderContext ctx, SKColor top, SKColor bottom) => ctx.Draw(c =>
    {
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, H), [top, bottom], SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = shader };
        c.DrawRect(0, 0, W, H, p);
    });

    private static SKImage Landscape(RenderContext ctx) => ctx.Draw(c =>
    {
        using var sky = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, H * 0.6f), [new SKColor(0x4A, 0x8C, 0xE0), new SKColor(0xC8, 0xE4, 0xF8)], SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = sky, IsAntialias = true };
        c.DrawRect(0, 0, W, H, p);
        p.Shader = null;
        p.Color = new SKColor(0xFF, 0xD2, 0x40);
        c.DrawCircle(W * 0.75f, H * 0.25f, H * 0.12f, p);
        p.Color = new SKColor(0x4C, 0x9A, 0x3A);
        c.DrawOval(new SKRect(-W * 0.2f, H * 0.55f, W * 0.7f, H * 1.4f), p);
        p.Color = new SKColor(0x6C, 0xB4, 0x44);
        c.DrawOval(new SKRect(W * 0.3f, H * 0.62f, W * 1.3f, H * 1.5f), p);
        p.Color = new SKColor(0x6A, 0x46, 0x2A);
        c.DrawRect(W * 0.22f, H * 0.42f, W * 0.04f, H * 0.2f, p);
        p.Color = new SKColor(0x2E, 0x7A, 0x2E);
        c.DrawCircle(W * 0.24f, H * 0.38f, H * 0.12f, p);
    });

    public override void Render(DrawingContext context)
    {
        var r = new Rect(Bounds.Size);
        if (_image is { } img)
        {
            context.DrawImage(img, new Rect(img.Size), r);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(0xE0, 0xE4, 0xE8)), r);
        }
    }
}
