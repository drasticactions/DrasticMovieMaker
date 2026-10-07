using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Rendering.Plan;
using AvaMovieMaker.Time;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Compositing;

public sealed class Compositor : IDisposable
{
    private const int MaxPictures = 16;
    private static readonly SKColorFilter Dim = SKColorFilter.CreateColorMatrix(
    [
        0.75f, 0, 0, 0, 0,
        0, 0.75f, 0, 0, 0,
        0, 0, 0.75f, 0, 0,
        0, 0, 0, 1, 0,
    ]);
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
                return DrawPicture(ctx, pic.Path, pic.Fit);
            case MediaSource video:
                using (DecodedFrame? f = _frames.GetFrame(video.Path, video.SourceTime))
                {
                    return f is null ? ctx.Draw(_ => { }) : DrawFrame(ctx, f, video.Fit);
                }

            case TitleSource title:
                return ctx.Draw(canvas => _effects.DrawTitle(ctx, canvas, title.Content, title.LocalTime, title.Duration, null, fullFrame: true));
            default:
                return ctx.Draw(_ => { });
        }
    }

    public SKImage DrawFrame(RenderContext ctx, DecodedFrame f, FrameFitMode mode = FrameFitMode.Fit)
    {
        using YuvUploader.Planes planes = YuvUploader.Upload(Device, f);
        return Framed(
            ctx,
            mode,
            f.Width,
            f.Height,
            cover => FrameFit.Matrix(f.Width, f.Height, f.SampleAspect, f.Rotation, f.FlipHorizontal, ctx.Width, ctx.Height, ctx.PixelAspect, cover),
            m => YuvUploader.Shader(planes, m));
    }

    private SKImage Framed(RenderContext ctx, FrameFitMode mode, int srcWidth, int srcHeight, Func<bool, SKMatrix> matrix, Func<SKMatrix, SKShader> shader)
    {
        SKImage? background = mode == FrameFitMode.Blur ? BlurredBackground(ctx, srcWidth, srcHeight, matrix(true), shader) : null;
        SKMatrix m = matrix(mode == FrameFitMode.Fill);
        using SKShader sharp = shader(m);
        SKRect rect = FrameFit.Rect(m, srcWidth, srcHeight, ctx.Width, ctx.Height);
        try
        {
            return ctx.Draw(canvas =>
            {
                if (background is not null)
                {
                    using var dim = new SKPaint { ColorFilter = Dim };
                    canvas.DrawImage(background, ctx.Bounds, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), dim);
                }

                using var paint = new SKPaint { Shader = sharp };
                canvas.DrawRect(rect, paint);
            });
        }
        finally
        {
            background?.Dispose();
        }
    }

    private SKImage BlurredBackground(RenderContext ctx, int srcWidth, int srcHeight, SKMatrix cover, Func<SKMatrix, SKShader> shader)
    {
        int qw = Math.Max(1, ctx.Width / 4), qh = Math.Max(1, ctx.Height / 4);
        SKMatrix small = cover.PostConcat(SKMatrix.CreateScale(qw / (float)ctx.Width, qh / (float)ctx.Height));
        using SKSurface sharpSurface = Device.CreateSurface(qw, qh);
        sharpSurface.Canvas.Clear(SKColors.Black);
        using (SKShader sh = shader(small))
        using (var paint = new SKPaint { Shader = sh })
        {
            sharpSurface.Canvas.DrawRect(FrameFit.Rect(small, srcWidth, srcHeight, qw, qh), paint);
        }

        using SKImage sharp = sharpSurface.Snapshot();
        float sigma = 0.02f * Math.Min(qw, qh);
        using SKSurface blurSurface = Device.CreateSurface(qw, qh);
        blurSurface.Canvas.Clear(SKColors.Black);
        using (SKImageFilter blur = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { ImageFilter = blur })
        {
            blurSurface.Canvas.DrawImage(sharp, 0, 0, SKSamplingOptions.Default, paint);
        }

        return blurSurface.Snapshot();
    }

    private SKImage DrawPicture(RenderContext ctx, string path, FrameFitMode mode)
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
        return Framed(
            ctx,
            mode,
            image.Width,
            image.Height,
            cover => FrameFit.Matrix(image.Width, image.Height, 1.0, entry.Rotation, entry.Flip, ctx.Width, ctx.Height, ctx.PixelAspect, cover),
            m => image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKCubicResampler.Mitchell), m));
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
