using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed record TitleClip
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public MediaTime Start { get; init; }

    public MediaTime Duration { get; init; }

    public TitleContent Content { get; init; } = new();

    public MediaTime End => Start + Duration;
}
