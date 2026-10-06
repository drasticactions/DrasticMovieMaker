using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Rendering.Compositing;
using SkiaSharp;

namespace AvaMovieMaker.Effects.Transitions;

internal static class PlaneTransitions
{
    public static SKImage Render(RenderContext ctx, SKImage a, SKImage b, TransitionInfo info, float p)
    {
        if (info.Variant.StartsWith("curl", StringComparison.Ordinal))
        {
            return ctx.Run(EffectShaders.Get("page_curl"), u =>
            {
                u["progress"] = p;
                u["fromRight"] = info.Variant == "curl-left" ? 1f : 0f;
            }, ("a", a), ("b", b));
        }

        int w = ctx.Width;
        int h = ctx.Height;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        return ctx.Draw(canvas =>
        {
            using var paint = new SKPaint { IsAntialias = true };
            switch (info.Variant)
            {
                case "flip":
                {
                    double angle = p * Math.PI;
                    bool showB = angle > Math.PI / 2;
                    SKMatrix m = Projection.RotateY(showB ? angle - Math.PI : angle, w, h);
                    canvas.Save();
                    canvas.Concat(m);
                    canvas.DrawImage(showB ? b : a, 0, 0, sampling, paint);
                    canvas.Restore();
                    break;
                }

                case "roll":
                    canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
                    canvas.Save();
                    canvas.RotateDegrees(Ease(p) * 90f, w, h);
                    canvas.DrawImage(a, 0, 0, sampling, paint);
                    canvas.Restore();
                    break;
                case "shrink":
                {
                    canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
                    float s = 1f - Ease(p);
                    paint.Color = SKColors.White.WithAlpha((byte)(255 * (p < 0.5f ? 1f : 1f - (p - 0.5f) / 0.5f)));
                    DrawScaled(canvas, a, w, h, s, 0, 0, 0, sampling, paint);
                    break;
                }

                case "slide":
                    canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
                    canvas.DrawImage(a, 0, -p * h, sampling, paint);
                    break;
                case "slide-center":
                    canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
                    DrawScaled(canvas, a, w, h, 1f - 0.5f * p, 0, -p * h, 0, sampling, paint);
                    break;
                case "spin":
                {
                    canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
                    float s = 1f - Ease(p);
                    paint.Color = SKColors.White.WithAlpha((byte)(255 * (p < 0.2f ? 1f : 1f - (p - 0.2f) / 0.8f)));
                    DrawScaled(canvas, a, w, h, s, 0, 0, -360f * p, sampling, paint);
                    break;
                }

                default:
                    canvas.DrawImage(b, 0, 0, SKSamplingOptions.Default);
                    break;
            }
        });
    }

    private static void DrawScaled(SKCanvas canvas, SKImage img, int w, int h, float scale, float dx, float dy, float degrees, SKSamplingOptions sampling, SKPaint paint)
    {
        if (scale <= 0.001f)
        {
            return;
        }

        canvas.Save();
        canvas.Translate(w / 2f + dx, h / 2f + dy);
        canvas.RotateDegrees(degrees);
        canvas.Scale(scale);
        canvas.Translate(-w / 2f, -h / 2f);
        canvas.DrawImage(img, 0, 0, sampling, paint);
        canvas.Restore();
    }

    internal static float Ease(float t) => t * t * (3 - 2 * t);
}
