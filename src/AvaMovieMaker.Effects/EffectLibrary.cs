using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Effects.Transitions;
using AvaMovieMaker.Effects.VideoEffects;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Plan;
using SkiaSharp;

namespace AvaMovieMaker.Effects;

public sealed class EffectLibrary : IEffectLibrary
{
    public static readonly EffectLibrary Instance = new();

    public SKImage ApplyEffect(RenderContext ctx, SKImage input, EffectInstance effect, int seed) =>
        EffectRenderer.Apply(ctx, input, effect, seed);

    public SKImage ApplyTransition(RenderContext ctx, SKImage a, SKImage b, TransitionInstance transition) =>
        TransitionRenderer.Render(ctx, a, b, transition);

    public void DrawTitle(RenderContext ctx, SKCanvas canvas, object content, double localTime, double duration, SKImage? video, bool fullFrame)
    {
        if (content is TitleContent title)
        {
            TitleAnimator.Draw(canvas, ctx.Width, ctx.Height, title, localTime, duration, video, fullFrame);
        }
    }

    public void PrepareTitle(object content)
    {
        if (content is TitleContent title)
        {
            TitleFonts.Resolve(title.Font);
        }
    }
}
