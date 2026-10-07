using AvaMovieMaker.IO;
using AvaMovieMaker.Timeline.Model;
using SkiaSharp;

namespace AvaMovieMaker.ViewModels.Preview;

public static class SamplePictures
{
    private const int Version = 1;
    private static readonly Lock Gate = new();

    public static (string First, string Second) For(AspectRatio aspect)
    {
        (int num, int den) = AspectRatios.Ratio(aspect);
        (int w, int h) = num >= den ? (640, (int)Math.Round(640.0 * den / num)) : ((int)Math.Round(640.0 * num / den), 640);
        string tag = $"{num}x{den}";
        return (Ensure($"flower-{tag}-v{Version}.png", w, h, DrawFlower), Ensure($"daisies-{tag}-v{Version}.png", w, h, DrawDaisies));
    }

    // Pictures of the other orientation, so fitting them to the frame shows a visible difference.
    public static (string First, string Second) Other(AspectRatio aspect) =>
        For(AspectRatios.IsPortrait(aspect) || aspect == AspectRatio.Square1x1 ? AspectRatio.Widescreen16x9 : AspectRatio.Vertical9x16);

    private static string Ensure(string name, int w, int h, Action<SKCanvas, int, int> draw)
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
                draw(canvas, w, h);
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

    private static void DrawFlower(SKCanvas c, int w, int h)
    {
        var center = new SKPoint(w * 0.5f, h * 0.52f);
        float r = Math.Min(w, h) * 0.42f;
        using (var bg = SKShader.CreateRadialGradient(center, Math.Max(w, h) * 0.75f,
            [new SKColor(0x9C, 0xB8, 0x48), new SKColor(0x46, 0x6E, 0x22), new SKColor(0x1C, 0x34, 0x12)], [0f, 0.55f, 1f], SKShaderTileMode.Clamp))
        using (var p = new SKPaint { Shader = bg })
        {
            c.DrawRect(0, 0, w, h, p);
        }

        using (var leaf = new SKPaint { Color = new SKColor(0x5A, 0x8E, 0x2E, 200), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6) })
        {
            c.Save();
            c.RotateDegrees(-28, w * 0.18f, h * 0.8f);
            c.DrawOval(new SKRect(w * 0.02f, h * 0.68f, w * 0.42f, h * 0.9f), leaf);
            c.Restore();
        }

        Petals(c, center, r, 26, 0.17f, new SKColor(0xF0, 0x3A, 0x2A), new SKColor(0xA8, 0x10, 0x18), 0);
        Petals(c, center, r * 0.62f, 20, 0.2f, new SKColor(0xFF, 0x5A, 0x3C), new SKColor(0xC4, 0x1E, 0x1E), 7);
        using (var disc = SKShader.CreateRadialGradient(center, r * 0.24f, [new SKColor(0x8A, 0x6A, 0x18), new SKColor(0x3A, 0x24, 0x08)], SKShaderTileMode.Clamp))
        using (var p = new SKPaint { Shader = disc, IsAntialias = true })
        {
            c.DrawCircle(center, r * 0.24f, p);
        }

        using var dot = new SKPaint { Color = new SKColor(0xF4, 0xD0, 0x40), IsAntialias = true };
        for (int i = 0; i < 90; i++)
        {
            double a = i * 2.39996323, d = Math.Sqrt(i / 90.0) * r * 0.22;
            c.DrawCircle(center.X + (float)(Math.Cos(a) * d), center.Y + (float)(Math.Sin(a) * d), 1.6f, dot);
        }
    }

    private static void DrawDaisies(SKCanvas c, int w, int h)
    {
        using (var sky = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h * 0.5f), [new SKColor(0x2E, 0x6C, 0xC8), new SKColor(0xB4, 0xD8, 0xF4)], SKShaderTileMode.Clamp))
        using (var p = new SKPaint { Shader = sky })
        {
            c.DrawRect(0, 0, w, h, p);
        }

        using (var cloud = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 210), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4) })
        {
            c.DrawOval(new SKRect(w * 0.08f, h * 0.08f, w * 0.34f, h * 0.2f), cloud);
            c.DrawOval(new SKRect(w * 0.18f, h * 0.04f, w * 0.3f, h * 0.16f), cloud);
            c.DrawOval(new SKRect(w * 0.62f, h * 0.12f, w * 0.9f, h * 0.22f), cloud);
        }

        using (var meadow = SKShader.CreateLinearGradient(new SKPoint(0, h * 0.45f), new SKPoint(0, h), [new SKColor(0x7C, 0xB4, 0x3C), new SKColor(0x2E, 0x6A, 0x1E)], SKShaderTileMode.Clamp))
        using (var p = new SKPaint { Shader = meadow, IsAntialias = true })
        using (var outline = new SKPathBuilder())
        {
            outline.MoveTo(0, h * 0.5f);
            outline.CubicTo(w * 0.3f, h * 0.4f, w * 0.6f, h * 0.56f, w, h * 0.46f);
            outline.LineTo(w, h);
            outline.LineTo(0, h);
            outline.Close();
            using SKPath hill = outline.Detach();
            c.DrawPath(hill, p);
        }

        var rng = new Random(7);
        for (int i = 0; i < 26; i++)
        {
            float depth = (float)rng.NextDouble();
            float y = h * (0.52f + depth * 0.4f);
            float x = w * (float)rng.NextDouble();
            float r = Math.Min(w, h) * (0.02f + depth * 0.07f);
            using var stem = new SKPaint { Color = new SKColor(0x2A, 0x5A, 0x16), StrokeWidth = Math.Max(1, r * 0.12f), IsAntialias = true };
            c.DrawLine(x, y, x + r * 0.2f, Math.Min(h, y + r * 3), stem);
            Petals(c, new SKPoint(x, y), r, 14, 0.24f, new SKColor(0xFF, 0xA8, 0x24), new SKColor(0xE0, 0x62, 0x0C), i);
            using var disc = new SKPaint { Color = new SKColor(0x7A, 0x46, 0x0C), IsAntialias = true };
            c.DrawCircle(x, y, r * 0.28f, disc);
        }
    }

    private static void Petals(SKCanvas c, SKPoint center, float radius, int count, float width, SKColor tip, SKColor baseColor, float turn)
    {
        using var shader = SKShader.CreateRadialGradient(center, radius, [baseColor, tip, tip], [0.1f, 0.7f, 1f], SKShaderTileMode.Clamp);
        using var fill = new SKPaint { Shader = shader, IsAntialias = true };
        using var edge = new SKPaint { Color = baseColor.WithAlpha(120), IsAntialias = true, IsStroke = true, StrokeWidth = 1 };
        for (int i = 0; i < count; i++)
        {
            c.Save();
            c.RotateDegrees(turn + i * 360f / count, center.X, center.Y);
            var petal = new SKRect(center.X - radius * width / 2, center.Y - radius, center.X + radius * width / 2, center.Y);
            c.DrawOval(petal, fill);
            c.DrawOval(petal, edge);
            c.Restore();
        }
    }
}
