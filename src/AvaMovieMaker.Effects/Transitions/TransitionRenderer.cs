using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Plan;
using SkiaSharp;

namespace AvaMovieMaker.Effects.Transitions;

internal static class TransitionRenderer
{
    public static SKImage Render(RenderContext ctx, SKImage a, SKImage b, TransitionInstance t)
    {
        TransitionInfo info = TransitionCatalog.Get(t.TransitionId);
        float p = (float)Math.Clamp(t.Progress, 0, 1);
        return info.Family switch
        {
            TransitionFamily.Wipe => Wipe(ctx, a, b, info, p),
            TransitionFamily.Dissolve => Dissolve(ctx, a, b, info, p, t.Seed),
            TransitionFamily.Pixelate => ctx.Run(EffectShaders.Get("pixelate_transition"), u =>
            {
                u["progress"] = p;
                u["maxBlock"] = info.MaxBlock;
            }, ("a", a), ("b", b)),
            TransitionFamily.Plane => PlaneTransitions.Render(ctx, a, b, info, p),
            TransitionFamily.Particles => ParticleTransitions.Render(ctx, a, b, info, p, t.Seed),
            _ => b,
        };
    }

    private static SKImage Wipe(RenderContext ctx, SKImage a, SKImage b, TransitionInfo info, float p) =>
        ctx.Run(EffectShaders.Get("wipe"), u =>
        {
            u["progress"] = p;
            u["softness"] = info.Softness;
            u["shape"] = (int)info.Shape;
            u["reverse"] = info.Reverse ? 1f : 0f;
            u["flip"] = new[] { info.FlipX ? 1f : 0f, info.FlipY ? 1f : 0f };
            u["count"] = (float)Math.Max(1, info.Count);
            u["grid"] = new[] { (float)Math.Max(1, info.Columns), Math.Max(1, info.Rows) };
            u["variant"] = info.Variant switch
            {
                "left" => 1f,
                "right" => 2f,
                _ => 0f,
            };
        }, ("a", a), ("b", b));

    private static SKImage Dissolve(RenderContext ctx, SKImage a, SKImage b, TransitionInfo info, float p, int seed) =>
        ctx.Run(EffectShaders.Get("dissolve"), u =>
        {
            u["progress"] = p;
            u["pattern"] = info.Variant switch
            {
                "rough" => 1,
                "bars-horizontal" => 2,
                "bars-vertical" => 3,
                _ => 0,
            };
            u["seed"] = (float)(seed % 1000);
        }, ("a", a), ("b", b));
}
