using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed record SourceClip
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public MediaTime Start { get; init; }

    public MediaTime End { get; init; }

    public MediaTime Duration => End - Start;
}
