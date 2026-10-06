using AvaMovieMaker.Time;

namespace AvaMovieMaker.Rendering.Plan;

public sealed record FramePlan(MediaTime Time, ClipInput A, ClipInput? B, TransitionInstance? Transition, IReadOnlyList<OverlayTitle> Titles)
{
    public static FramePlan Black(MediaTime time) => new(time, ClipInput.Black, null, null, []);
}
