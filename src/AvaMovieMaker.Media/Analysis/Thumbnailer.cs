using System.Globalization;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;
using SkiaSharp;

namespace AvaMovieMaker.Media.Analysis;

public static class Thumbnailer
{
    public static MediaTime RepresentativeTime(MediaTime clipIn, MediaTime clipDuration) =>
        clipIn + MediaTime.Min(MediaTime.FromSeconds(1), clipDuration / 2);

    public static SKBitmap? Get(string path, MediaTime time, int width, int height, DecoderPool? pool = null, bool useCache = true)
    {
        string name = string.Create(CultureInfo.InvariantCulture, $"thumb-{time.Ticks}-{width}x{height}.png");
        string? cacheFile = null;
        if (useCache)
        {
            try
            {
                cacheFile = MediaCache.FileFor(path, name);
                if (FileStore.Current.Exists(cacheFile))
                {
                    using Stream cachedStream = FileStore.Current.OpenRead(cacheFile);
                    SKBitmap? cached = SKBitmap.Decode(cachedStream);
                    if (cached is not null)
                    {
                        return cached;
                    }
                }
            }
            catch (IOException)
            {
                cacheFile = null;
            }
        }

        SKBitmap? bmp = Render(path, time, width, height, pool);
        if (bmp is not null && cacheFile is not null)
        {
            try
            {
                using SKData png = bmp.Encode(SKEncodedImageFormat.Png, 90);
                using Stream fs = FileStore.Current.Create(cacheFile);
                png.SaveTo(fs);
            }
            catch (IOException e)
            {
                Log.Debug("thumbs", $"Cache write failed: {e.Message}");
            }
        }

        return bmp;
    }

    public static SKBitmap? Render(string path, MediaTime time, int width, int height, DecoderPool? pool = null)
    {
        DecodedFrame? frame;
        if (pool is not null)
        {
            using DecoderPool.Lease lease = pool.Acquire(path);
            frame = lease.Decoder.GetFrame(time);
        }
        else
        {
            using var dec = new VideoDecoder(path, allowHardware: false);
            frame = dec.GetFrame(time);
        }

        if (frame is null)
        {
            return null;
        }

        using (frame)
        {
            using SKBitmap full = ToBitmap(frame);
            return Fit(full, frame.Rotation, frame.FlipHorizontal, frame.SampleAspect, width, height);
        }
    }

    public static SKBitmap ToBitmap(DecodedFrame frame)
    {
        byte[] bgra = frame.ToBgra(out int w, out int h);
        var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, bmp.GetPixels(), bgra.Length);
        return bmp;
    }

    public static SKBitmap Fit(SKBitmap src, int rotation, bool flip, double sampleAspect, int width, int height)
    {
        var dst = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Black);
        double sw = src.Width * sampleAspect;
        double sh = src.Height;
        if (rotation is 90 or 270)
        {
            (sw, sh) = (sh, sw);
        }

        double scale = Math.Min(width / sw, height / sh);
        float dw = (float)(sw * scale);
        float dh = (float)(sh * scale);
        canvas.Translate(width / 2f, height / 2f);
        if (flip)
        {
            canvas.Scale(-1, 1);
        }

        canvas.RotateDegrees(rotation);
        var rect = rotation is 90 or 270 ? new SKRect(-dh / 2, -dw / 2, dh / 2, dw / 2) : new SKRect(-dw / 2, -dh / 2, dw / 2, dh / 2);
        using SKImage image = SKImage.FromBitmap(src);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, rect, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        return dst;
    }
}
