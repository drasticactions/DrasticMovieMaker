using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Plan;
using SkiaSharp;

namespace AvaMovieMaker.Effects.VideoEffects;

internal static class EffectRenderer
{
    public const double FadeSeconds = 1.0;

    public const double NoiseRate = 30000.0 / 1001.0;

    public static SKImage Apply(RenderContext ctx, SKImage input, EffectInstance e, int seed)
    {
        EffectInfo? info = EffectCatalog.Find(e.EffectId);
        if (info is null || info.Family is EffectFamily.Speed or EffectFamily.Framing)
        {
            return input;
        }

        float scale = Math.Min(ctx.Width, ctx.Height) / 480f;
        double t = e.LocalTime;
        double dur = Math.Max(e.ClipDuration, 0.001);
        double progress = Math.Clamp(t / dur, 0, 1);
        float[] v = info.Values;
        switch (info.Operation)
        {
            case "grayscale":
                return Color(ctx, input, 0, 0);
            case "sepia":
                return Color(ctx, input, 1, 0);
            case "brightness":
                return Color(ctx, input, 2, v[0] * 0.2f);
            case "posterize":
                return Color(ctx, input, 3, v[0]);
            case "threshold":
                return Color(ctx, input, 4, 0);
            case "hue":
                return Color(ctx, input, 5, (float)(progress * Math.PI * 2));
            case "fade":
            {
                double len = Math.Min(FadeSeconds, dur / 2);
                double level = v[0] > 0 ? Math.Clamp(t / len, 0, 1) : Math.Clamp((dur - t) / len, 0, 1);
                float c = v[1];
                return Color(ctx, input, 6, (float)level, c, c, c);
            }

            case "blur":
                return ctx.Draw(canvas =>
                {
                    using var filter = SKImageFilter.CreateBlur(3f * scale, 3f * scale, SKShaderTileMode.Clamp);
                    using var paint = new SKPaint { ImageFilter = filter };
                    canvas.DrawImage(input, 0, 0, SKSamplingOptions.Default, paint);
                });
            case "sharpen":
                return Convolve(ctx, input, 0, Math.Max(1f, scale));
            case "edges":
                return Convolve(ctx, input, 1, Math.Max(1f, scale));
            case "pixelate":
                return Convolve(ctx, input, 2, MathF.Round(12 * scale));
            case "watercolor":
                return ctx.Run(EffectShaders.Get("watercolor"), u => u["radius"] = 5f * scale, ("src", input));
            case "ripple":
            case "warp":
                return ctx.Run(EffectShaders.Get("distort"), u =>
                {
                    u["op"] = info.Operation == "ripple" ? 0 : 1;
                    u["time"] = (float)t;
                    u["duration"] = (float)dur;
                }, ("src", input));
            case "age":
            case "grain":
                return Film(ctx, input, info, t, seed);
            case "mirror":
                return Geometry(ctx, input, SKMatrix.CreateScale(v[0] > 0 ? -1 : 1, v[1] > 0 ? -1 : 1, ctx.Width / 2f, ctx.Height / 2f));
            case "rotate":
                return Geometry(ctx, input, SKMatrix.CreateRotationDegrees(v[0], ctx.Width / 2f, ctx.Height / 2f));
            case "spin":
                return Geometry(ctx, input, SKMatrix.CreateRotationDegrees((float)(progress * 360), ctx.Width / 2f, ctx.Height / 2f));
            case "ease":
            {
                double z = v[0] > 0 ? 1 + 0.25 * progress : 1.25 - 0.25 * progress;
                return Geometry(ctx, input, ViewMatrix(ctx, 0.5, 0.5, z));
            }

            case "pan":
            {
                double cx = Lerp(v[0], v[3], progress);
                double cy = Lerp(v[1], v[4], progress);
                double z = Lerp(v[2], v[5], progress);
                return Geometry(ctx, input, ViewMatrix(ctx, cx, cy, z));
            }

            default:
                return input;
        }
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static SKMatrix ViewMatrix(RenderContext ctx, double cx, double cy, double z)
    {
        z = Math.Max(1, z);
        double halfW = 0.5 / z, halfH = 0.5 / z;
        cx = Math.Clamp(cx, halfW, 1 - halfW);
        cy = Math.Clamp(cy, halfH, 1 - halfH);
        SKMatrix m = SKMatrix.CreateTranslation((float)(-(cx - halfW) * ctx.Width), (float)(-(cy - halfH) * ctx.Height));
        return m.PostConcat(SKMatrix.CreateScale((float)z, (float)z));
    }

    private static SKImage Geometry(RenderContext ctx, SKImage input, SKMatrix m) =>
        ctx.Draw(canvas =>
        {
            canvas.Concat(m);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawImage(input, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), paint);
        });

    private static SKImage Color(RenderContext ctx, SKImage input, int op, float amount, float r = 0, float g = 0, float b = 0) =>
        ctx.Run(EffectShaders.Get("color"), u =>
        {
            u["op"] = op;
            u["amount"] = amount;
            u["tint"] = new[] { r, g, b };
        }, ("src", input));

    private static SKImage Convolve(RenderContext ctx, SKImage input, int op, float step) =>
        ctx.Run(EffectShaders.Get("convolve"), u =>
        {
            u["op"] = op;
            u["stepSize"] = step;
        }, ("src", input));

    private static SKImage Film(RenderContext ctx, SKImage input, EffectInfo info, double t, int seed)
    {
        float scale = Math.Min(ctx.Width, ctx.Height) / 480f;
        long frame = (long)Math.Floor(t * NoiseRate + 1e-6);
        bool age = info.Operation == "age";
        float[] v = info.Values;
        SKImage src = input;
        bool ownsSrc = false;
        if (age && v[6] > 0)
        {
            float sigma = v[6] * 0.25f * scale;
            src = ctx.Draw(canvas =>
            {
                using var filter = SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp);
                using var paint = new SKPaint { ImageFilter = filter };
                canvas.DrawImage(input, 0, 0, SKSamplingOptions.Default, paint);
            });
            ownsSrc = true;
        }

        float shakeAmount = age ? v[5] * 8f * scale : 0f;
        float sx = shakeAmount * (Noise(seed, frame, 1) - 0.5f);
        float sy = shakeAmount * (Noise(seed, frame, 2) - 0.5f);
        SKImage result = ctx.Run(EffectShaders.Get("film"), u =>
        {
            u["op"] = age ? 0 : 1;
            u["frame"] = (float)(frame % 100000);
            u["seed"] = (float)(seed % 997);
            u["noiseAmount"] = age ? v[0] : 1f;
            u["splotches"] = age ? v[1] : 0f;
            u["lines"] = age ? v[2] : 0f;
            u["lint"] = age ? v[3] : 0f;
            u["levels"] = age ? v[4] : 256f;
            u["shake"] = new[] { sx, sy };
        }, ("src", src));
        if (ownsSrc)
        {
            src.Dispose();
        }

        return result;
    }

    private static float Noise(int seed, long frame, int salt)
    {
        ulong x = (ulong)(seed * 2654435761L) ^ (ulong)(frame * 40503) ^ (ulong)(salt * 2246822519L);
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdUL;
        x ^= x >> 33;
        return (x & 0xFFFFFF) / (float)0x1000000;
    }
}
