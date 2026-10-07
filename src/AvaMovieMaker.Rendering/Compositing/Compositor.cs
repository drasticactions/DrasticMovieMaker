using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Rendering.Plan;
using AvaMovieMaker.Time;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Compositing;

public sealed class Compositor : IDisposable
{
    private const int MaxPictures = 16;
    private readonly IEffectLibrary _effects;
    private IFrameProvider _frames;
    private readonly Dictionary<string, (SKImage Image, int Rotation, bool Flip, long Use)> _pictures = new(StringComparer.Ordinal);
    private long _clock;

    public Compositor(RenderDevice device, IEffectLibrary effects, IFrameProvider frames)
    {
        Device = device;
        _effects = effects;
        _frames = frames;
    }

    public RenderDevice Device { get; }

    public void WarmUp()
    {
        var ctx = new RenderContext(Device, 16, 16);
        using SKImage black = ctx.Draw(_ => { });
        _effects.WarmUp(ctx, black);
        Device.Flush();
    }

    public SKImage Render(PreparedFrame prepared)
    {
        IFrameProvider frames = _frames;
        _frames = prepared;
        try
        {
            return Render(prepared.Plan, prepared.Width, prepared.Height);
        }
        finally
        {
            _frames = frames;
            prepared.Release();
        }
    }

    public SKImage Render(FramePlan plan, int width, int height, double pixelAspect = 1.0)
    {
        var ctx = new RenderContext(Device, width, height, pixelAspect);
        SKImage frame;
        SKImage a = RenderClip(ctx, plan.A);
        if (plan.B is { } bInput && plan.Transition is { } t)
        {
            using SKImage b = RenderClip(ctx, bInput);
            frame = _effects.ApplyTransition(ctx, a, b, t);
            if (!ReferenceEquals(frame, a))
            {
                a.Dispose();
            }
        }
        else
        {
            frame = a;
        }

        if (plan.Titles.Count > 0)
        {
            SKImage under = frame;
            frame = ctx.Draw(canvas =>
            {
                canvas.DrawImage(under, 0, 0, SKSamplingOptions.Default);
                foreach (OverlayTitle title in plan.Titles)
                {
                    _effects.DrawTitle(ctx, canvas, title.Content, title.LocalTime, title.Duration, under, fullFrame: false);
                }
            });
            under.Dispose();
        }

        return frame;
    }

    private SKImage RenderClip(RenderContext ctx, ClipInput input)
    {
        SKImage image = RenderSource(ctx, input.Source);
        foreach (EffectInstance e in input.Effects)
        {
            SKImage next = _effects.ApplyEffect(ctx, image, e, input.Seed);
            if (!ReferenceEquals(next, image))
            {
                image.Dispose();
                image = next;
            }
        }

        if (input.FadeLevel < 1f)
        {
            float level = Math.Clamp(input.FadeLevel, 0f, 1f);
            SKImage src = image;
            image = ctx.Draw(canvas =>
            {
                using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(level * 255)) };
                canvas.DrawImage(src, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), paint);
            });
            src.Dispose();
        }

        return image;
    }

    private SKImage RenderSource(RenderContext ctx, ClipSource source)
    {
        switch (source)
        {
            case MediaSource { IsPicture: true } pic:
                return DrawPicture(ctx, pic.Path);
            case MediaSource video:
                using (DecodedFrame? f = _frames.GetFrame(video.Path, video.SourceTime))
                {
                    return f is null ? ctx.Draw(_ => { }) : DrawFrame(ctx, f);
                }

            case TitleSource title:
                return ctx.Draw(canvas => _effects.DrawTitle(ctx, canvas, title.Content, title.LocalTime, title.Duration, null, fullFrame: true));
            default:
                return ctx.Draw(_ => { });
        }
    }

    public SKImage DrawFrame(RenderContext ctx, DecodedFrame f)
    {
        using YuvUploader.Planes planes = YuvUploader.Upload(Device, f);
        SKMatrix m = FrameFit.Matrix(f.Width, f.Height, f.SampleAspect, f.Rotation, f.FlipHorizontal, ctx.Width, ctx.Height, ctx.PixelAspect);
        using SKShader shader = YuvUploader.Shader(planes, m);
        SKRect rect = FrameFit.Rect(m, f.Width, f.Height);
        return ctx.Draw(canvas =>
        {
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(rect, paint);
        });
    }

    private SKImage DrawPicture(RenderContext ctx, string path)
    {
        if (!_pictures.TryGetValue(path, out var entry))
        {
            using DecodedFrame? f = _frames.GetFrame(path, MediaTime.Zero);
            if (f is null)
            {
                return ctx.Draw(_ => { });
            }

            entry = (CapSize(UploadRgba(f)), f.Rotation, f.FlipHorizontal, 0);
            if (_pictures.Count >= MaxPictures)
            {
                string oldest = _pictures.MinBy(p => p.Value.Use).Key;
                _pictures[oldest].Image.Dispose();
                _pictures.Remove(oldest);
            }
        }

        entry.Use = ++_clock;
        _pictures[path] = entry;
        SKImage image = entry.Image;
        SKMatrix m = FrameFit.Matrix(image.Width, image.Height, 1.0, entry.Rotation, entry.Flip, ctx.Width, ctx.Height, ctx.PixelAspect);
        SKRect rect = FrameFit.Rect(m, image.Width, image.Height);
        return ctx.Draw(canvas =>
        {
            using SKShader shader = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKCubicResampler.Mitchell), m);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(rect, paint);
        });
    }

    private SKImage UploadRgba(DecodedFrame f)
    {
        FramePlane p = f.Plane(0);
        var info = new SKImageInfo(p.Width, p.Height, f.Format == FramePixelFormat.Bgra ? SKColorType.Bgra8888 : SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKImage raster = SKImage.FromPixelCopy(info, p.Data, p.Stride) ?? throw new InvalidOperationException("Picture upload failed.");
        return Device.Upload(raster);
    }

    private SKImage CapSize(SKImage img)
    {
        const int max = 4096;
        int big = Math.Max(img.Width, img.Height);
        if (big <= max)
        {
            return img;
        }

        double s = max / (double)big;
        int w = (int)Math.Round(img.Width * s);
        int h = (int)Math.Round(img.Height * s);
        using SKSurface surf = Device.CreateSurface(w, h);
        surf.Canvas.DrawImage(img, new SKRect(0, 0, w, h), new SKSamplingOptions(SKCubicResampler.Mitchell));
        img.Dispose();
        return surf.Snapshot();
    }

    public void ClearPictureCache()
    {
        foreach (var p in _pictures.Values)
        {
            p.Image.Dispose();
        }

        _pictures.Clear();
    }

    public static void ReadPixels(SKImage image, Span<byte> destination, bool bgra, GRContext? context = null)
    {
        var info = new SKImageInfo(image.Width, image.Height, bgra ? SKColorType.Bgra8888 : SKColorType.Rgba8888, SKAlphaType.Premul);
        unsafe
        {
            fixed (byte* p = destination)
            {
                if (!image.ReadPixels(info, (IntPtr)p, info.RowBytes, 0, 0))
                {
                    throw new InvalidOperationException("Readback failed.");
                }
            }
        }
    }

    public void Dispose() => ClearPictureCache();
}
