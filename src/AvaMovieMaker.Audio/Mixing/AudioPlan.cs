namespace AvaMovieMaker.Audio.Mixing;

public sealed record AudioPlan(IReadOnlyList<AudioInput> Inputs, long DurationSamples, double Levels)
{
    public static readonly AudioPlan Empty = new([], 0, 0);
}
