namespace AvaMovieMaker.Timeline.Model;

public sealed record EffectRef(string EffectId)
{
    public IReadOnlyDictionary<string, double>? Parameters { get; init; }
}
