using AvaMovieMaker.Rendering.Compositing;
using SkiaSharp;

namespace AvaMovieMaker.Effects;

internal static class EffectShaders
{
    public static SKRuntimeEffect Get(string name) =>
        RenderContext.Effect("fx:" + name, () => SkslSources.Get(name));

    public static readonly string[] Names =
        ["wipe", "dissolve", "pixelate_transition", "page_curl", "color", "convolve", "watercolor", "distort", "film"];
}
