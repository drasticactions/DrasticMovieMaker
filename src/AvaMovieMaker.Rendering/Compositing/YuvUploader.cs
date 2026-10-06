using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Rendering.Gpu;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Compositing;

public static class YuvUploader
{
    private static SKRuntimeEffect Effect => RenderContext.Effect("yuv_to_rgb",
        () => SkslSources.Get("yuv_to_rgb"));

    public sealed class Planes : IDisposable
    {
        public required SKImage[] Images { get; init; }

        public required DecodedFrame Frame { get; init; }

        public void Dispose()
        {
            foreach (SKImage i in Images)
            {
                i.Dispose();
            }
        }
    }

    public static Planes Upload(RenderDevice device, DecodedFrame f)
    {
        if (f.IsHardware && device.DmaBuf is { } importer && device.Context is { } context
            && importer.TryImport(context, f) is { } gpu)
        {
            return new Planes { Images = gpu, Frame = f };
        }

        var images = new SKImage[f.PlaneCount];
        for (int i = 0; i < f.PlaneCount; i++)
        {
            FramePlane p = f.Plane(i);
            SKColorType ct = (f.Format, i) switch
            {
                (FramePixelFormat.Rgba, _) => SKColorType.Rgba8888,
                (FramePixelFormat.Bgra, _) => SKColorType.Bgra8888,
                (FramePixelFormat.Yuv420P, _) => SKColorType.Gray8,
                (FramePixelFormat.Nv12, 0) => SKColorType.Gray8,
                (FramePixelFormat.Nv12, _) => SKColorType.Rg88,
                (FramePixelFormat.Yuv420P10, _) => SKColorType.Alpha16,
                (FramePixelFormat.P010, 0) => SKColorType.Alpha16,
                _ => SKColorType.Rg1616,
            };
            SKAlphaType at = ct is SKColorType.Rgba8888 or SKColorType.Bgra8888 ? SKAlphaType.Unpremul
                : ct == SKColorType.Alpha16 ? SKAlphaType.Premul : SKAlphaType.Opaque;
            var info = new SKImageInfo(p.Width, p.Height, ct, at);
            SKImage raster = SKImage.FromPixelCopy(info, p.Data, p.Stride)
                ?? throw new InvalidOperationException($"Could not upload a {ct} plane.");
            images[i] = device.Upload(raster);
        }

        return new Planes { Images = images, Frame = f };
    }

    public static SKShader Shader(Planes planes, SKMatrix local)
    {
        DecodedFrame f = planes.Frame;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        if (f.Format is FramePixelFormat.Rgba or FramePixelFormat.Bgra)
        {
            return planes.Images[0].ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling, local);
        }

        SKRuntimeEffect fx = Effect;
        using var u = new SKRuntimeEffectUniforms(fx);
        int layout = f.Format switch
        {
            FramePixelFormat.Yuv420P => 0,
            FramePixelFormat.Nv12 => 1,
            FramePixelFormat.Yuv420P10 => 2,
            _ => 3,
        };
        u["planeLayout"] = layout;
        u["chromaScale"] = new[] { ((f.Width + 1) / 2) / (float)f.Width, ((f.Height + 1) / 2) / (float)f.Height };

        u["chromaOffset"] = new[] { 0.25f, 0f };
        (float kr, float kb) = f.ColorSpace switch
        {
            FrameColorSpace.Bt709 => (0.2126f, 0.0722f),
            FrameColorSpace.Bt2020 => (0.2627f, 0.0593f),
            _ => (0.299f, 0.114f),
        };
        float kg = 1 - kr - kb;
        u["coef"] = new[] { 2 * (1 - kr), 2 * kb * (1 - kb) / kg, 2 * kr * (1 - kr) / kg, 2 * (1 - kb) };
        u["range"] = f.FullRange
            ? new[] { 1f, 0f, 1f, 128f / 255f }
            : new[] { 255f / 219f, 16f / 255f, 255f / 224f, 128f / 255f };
        u["alphaOne"] = 1f;

        using var ch = new SKRuntimeEffectChildren(fx);
        using SKShader y = planes.Images[0].ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling);
        using SKShader us = planes.Images[1].ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling);
        using SKShader vs = planes.Images[planes.Images.Length > 2 ? 2 : 1].ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling);
        ch["y"] = y;
        ch["u"] = us;
        ch["v"] = vs;
        return fx.ToShader(u, ch, local);
    }
}
