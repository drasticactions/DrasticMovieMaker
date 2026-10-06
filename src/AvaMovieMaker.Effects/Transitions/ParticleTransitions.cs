using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Rendering.Compositing;
using SkiaSharp;

namespace AvaMovieMaker.Effects.Transitions;

internal static class ParticleTransitions
{
    public static SKImage Render(RenderContext ctx, SKImage a, SKImage b, TransitionInfo info, float p, int seed)
    {
        int w = ctx.Width;
        int h = ctx.Height;
        int n = Math.Max(4, info.Particles);
        int cols = Math.Max(2, (int)Math.Round(Math.Sqrt(n * (double)w / h)));
        int rows = Math.Max(2, (int)Math.Round(n / (double)cols));
        float tw = w / (float)cols;
        float th = h / (float)rows;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        return ctx.Draw(canvas =>
        {
            canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
            using var paint = new SKPaint { IsAntialias = true };

            using var stillTiles = new SKPathBuilder();
            var moving = new List<(SKRect Src, float X, float Y, float Rot, float Scale, float Alpha)>();
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int k = j * cols + i;
                    float r1 = Hash(seed, k, 1), r2 = Hash(seed, k, 2), r3 = Hash(seed, k, 3);
                    float cx = (i + 0.5f) * tw;
                    float cy = (j + 0.5f) * th;
                    float start = info.Variant switch
                    {
                        "right" => (1f - cx / w) * 0.5f,
                        "whirl-top" => cy / h * 0.6f,
                        "in" => r1 * 0.1f,
                        _ => r1 * 0.3f,
                    };
                    float t = Math.Clamp((p - start) / Math.Max(0.05f, 1f - start), 0f, 1f);
                    if (info.Variant == "in")
                    {
                        t = (MathF.Exp(t * 3f) - 1f) / (MathF.Exp(3f) - 1f);
                    }

                    if (t >= 1f)
                    {
                        continue;
                    }

                    if (t <= 0f)
                    {
                        stillTiles.AddRect(new SKRect(i * tw, j * th, (i + 1) * tw, (j + 1) * th));
                        continue;
                    }

                    float dx = 0, dy = 0, rot = 0, scale = 1, alpha = 1 - t * t;
                    switch (info.Variant)
                    {
                        case "in":
                            scale = 1 + t * 4;
                            dx = (cx - w / 2f) * t * 2;
                            dy = (cy - h / 2f) * t * 2;
                            rot = (r2 - 0.5f) * 360 * t;
                            break;
                        case "right":
                            dx = t * w * (1 + r2);
                            dy = (r3 - 0.5f) * h * 0.3f * t;
                            rot = 120 * t * (r2 + 0.3f);
                            break;
                        case "up-left":
                        case "up-right":
                            dx = (info.Variant == "up-left" ? -1 : 1) * t * w * (0.6f + r2);
                            dy = -t * h * (0.6f + r3) + t * t * h * 0.3f;
                            rot = (r2 - 0.5f) * 540 * t;
                            break;
                        default:
                        {
                            float ox = cx - w / 2f, oy = cy - h / 2f;
                            float ang = t * MathF.PI * (1.5f + r2);
                            float grow = 1 + t * 2.5f;
                            dx = (ox * MathF.Cos(ang) - oy * MathF.Sin(ang)) * grow - ox;
                            dy = (ox * MathF.Sin(ang) + oy * MathF.Cos(ang)) * grow - oy;
                            rot = 400 * 0.4f * t * (r3 + 0.5f);
                            scale = 1 - t * 0.5f;
                            break;
                        }
                    }

                    moving.Add((new SKRect(i * tw, j * th, (i + 1) * tw, (j + 1) * th), cx + dx, cy + dy, rot, scale, Math.Clamp(alpha, 0, 1)));
                }
            }

            using SKPath still = stillTiles.Detach();
            if (!still.IsEmpty)
            {
                canvas.Save();
                canvas.ClipPath(still, SKClipOperation.Intersect, antialias: false);
                canvas.DrawImage(a, 0, 0, SKSamplingOptions.Default);
                canvas.Restore();
            }

            var dst = new SKRect(-tw / 2, -th / 2, tw / 2, th / 2);
            foreach ((SKRect src, float x, float y, float rot, float scale, float alpha) in moving)
            {
                paint.Color = SKColors.White.WithAlpha((byte)(255 * alpha));
                canvas.Save();
                canvas.Translate(x, y);
                canvas.RotateDegrees(rot);
                canvas.Scale(scale);
                canvas.DrawImage(a, src, dst, sampling, paint);
                canvas.Restore();
            }
        });
    }

    private static float Hash(int seed, int k, int salt)
    {
        uint x = (uint)(seed * 73856093) ^ (uint)(k * 19349663) ^ (uint)(salt * 83492791);
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return (x & 0xFFFFFF) / (float)0x1000000;
    }
}
