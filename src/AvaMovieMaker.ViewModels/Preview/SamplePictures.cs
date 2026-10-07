using AvaMovieMaker.IO;
using AvaMovieMaker.Timeline.Model;
using SkiaSharp;

namespace AvaMovieMaker.ViewModels.Preview;

public static class SamplePictures
{
    private const int Version = 3;
    private static readonly Lock Gate = new();

    public static Photo Dawn { get; } = new("fuji-dawn", 0.515f, 0.45f);

    public static Photo Sakura { get; } = new("fuji-sakura", 0.49f, 0.55f);

    public static (string First, string Second) For(AspectRatio aspect)
    {
        (int num, int den) = AspectRatios.Ratio(aspect);
        (int w, int h) = num >= den ? (640, (int)Math.Round(640.0 * den / num)) : ((int)Math.Round(640.0 * num / den), 640);
        string tag = $"{num}x{den}";
        return (Ensure($"{Dawn.Name}-{tag}-v{Version}.png", w, h, Dawn), Ensure($"{Sakura.Name}-{tag}-v{Version}.png", w, h, Sakura));
    }

    public static (string First, string Second) Other(AspectRatio aspect) =>
        For(AspectRatios.IsPortrait(aspect) || aspect == AspectRatio.Square1x1 ? AspectRatio.Widescreen16x9 : AspectRatio.Vertical9x16);

    private static string Ensure(string name, int w, int h, Photo photo)
    {
        string dir = Path.Combine(AppPaths.CacheDir, "samples");
        string path = Path.Combine(dir, name);
        lock (Gate)
        {
            if (FileStore.Current.Exists(path))
            {
                return path;
            }

            using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bmp))
            {
                photo.Draw(canvas, w, h);
            }

            using SKData data = bmp.Encode(SKEncodedImageFormat.Png, 100);
            string temp = path + ".tmp";
            using (Stream fs = FileStore.Current.Create(temp))
            {
                data.SaveTo(fs);
            }

            FileStore.Current.Move(temp, path);
            return path;
        }
    }

    public sealed class Photo(string name, float focusX, float focusY)
    {
        private readonly Lazy<SKImage> _image = new(() =>
        {
            using Stream s = typeof(SamplePictures).Assembly.GetManifestResourceStream($"AvaMovieMaker.ViewModels.Preview.Samples.{name}.jpg")
                ?? throw new InvalidOperationException($"The sample photo {name} is missing.");
            using SKData data = SKData.Create(s);
            return SKImage.FromEncodedData(data).ToRasterImage(ensurePixelData: true);
        });

        public string Name => name;

        public void Draw(SKCanvas c, int w, int h)
        {
            SKImage img = _image.Value;
            float scale = Math.Max((float)w / img.Width, (float)h / img.Height);
            float cw = w / scale, ch = h / scale;
            float x = Math.Clamp(img.Width * focusX - cw / 2, 0, img.Width - cw);
            float y = Math.Clamp(img.Height * focusY - ch / 2, 0, img.Height - ch);
            using var paint = new SKPaint { IsAntialias = true };
            c.DrawImage(img, SKRect.Create(x, y, cw, ch), SKRect.Create(0, 0, w, h), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        }
    }
}
