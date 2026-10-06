namespace AvaMovieMaker.Rendering.Plan;

public sealed record ClipInput(ClipSource Source, IReadOnlyList<EffectInstance> Effects, int Seed, float FadeLevel = 1f)
{
    public static readonly ClipInput Black = new(BlackSource.Instance, [], 0);
}
