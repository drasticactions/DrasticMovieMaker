using System.Collections.Concurrent;
using AvaMovieMaker.Rendering.Gpu;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Compositing;

public sealed class RenderContext
{
    private static readonly ConcurrentDictionary<string, SKRuntimeEffect> Effects = new(StringComparer.Ordinal);

    public RenderContext(RenderDevice device, int width, int height, double pixelAspect = 1.0)
    {
        Device = device;
        Width = width;
        Height = height;
        PixelAspect = pixelAspect;
    }

    public RenderDevice Device { get; }

    public int Width { get; }

    public int Height { get; }

    public double PixelAspect { get; }

    public double DisplayAspect => Width * PixelAspect / Height;

    public SKRect Bounds => new(0, 0, Width, Height);

    public static SKRuntimeEffect Effect(string key, Func<string> source) =>
        Effects.GetOrAdd(key, k =>
        {
            SKRuntimeEffect? fx = SKRuntimeEffect.CreateShader(source(), out string? error);
            return fx ?? throw new InvalidOperationException($"SkSL {k}: {error}");
        });

    public SKSurface NewSurface() => Device.CreateSurface(Width, Height);

    public SKImage Draw(Action<SKCanvas> draw, SKColor? clear = null)
    {
        using SKSurface s = NewSurface();
        s.Canvas.Clear(clear ?? SKColors.Black);
        draw(s.Canvas);
        return s.Snapshot();
    }

    public static SKShader Sample(SKImage image, SKMatrix? local = null) =>
        local is { } m
            ? image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), m)
            : image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));

    public SKImage Run(SKRuntimeEffect effect, Action<SKRuntimeEffectUniforms>? uniforms, params (string Name, SKImage Image)[] children)
    {
        using var u = new SKRuntimeEffectUniforms(effect);
        if (u.Contains("resolution"))
        {
            u["resolution"] = new[] { (float)Width, Height };
        }

        if (u.Contains("aspect"))
        {
            u["aspect"] = (float)DisplayAspect;
        }

        uniforms?.Invoke(u);
        using var c = new SKRuntimeEffectChildren(effect);
        var owned = new List<SKShader>();
        foreach ((string name, SKImage image) in children)
        {
            SKShader sh = Sample(image);
            owned.Add(sh);
            c[name] = sh;
        }

        using SKShader shader = effect.ToShader(u, c);
        using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src };
        SKImage result = Draw(canvas => canvas.DrawRect(Bounds, paint));
        foreach (SKShader sh in owned)
        {
            sh.Dispose();
        }

        return result;
    }
}
