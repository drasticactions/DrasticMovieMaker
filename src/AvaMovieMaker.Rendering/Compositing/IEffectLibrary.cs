using AvaMovieMaker.Rendering.Plan;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Compositing;

public interface IEffectLibrary
{
    SKImage ApplyEffect(RenderContext ctx, SKImage input, EffectInstance effect, int seed);

    SKImage ApplyTransition(RenderContext ctx, SKImage a, SKImage b, TransitionInstance transition);

    void DrawTitle(RenderContext ctx, SKCanvas canvas, object content, double localTime, double duration, SKImage? video, bool fullFrame);

    void PrepareTitle(object content)
    {
    }

    void WarmUp(RenderContext ctx, SKImage input)
    {
    }
}
