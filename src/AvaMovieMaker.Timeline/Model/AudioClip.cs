using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed record AudioClip
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid MediaId { get; init; }

    public Guid SourceClipId { get; init; }

    public MediaTime In { get; init; }

    public MediaTime Out { get; init; }

    public MediaTime? RangeIn { get; init; }

    public MediaTime? RangeOut { get; init; }

    public MediaTime Start { get; init; }

    public AudioSettings Audio { get; init => field = value ?? AudioSettings.Default; } = AudioSettings.Default;

    public MediaTime Length => Out - In;

    public MediaTime End => Start + Length;
}
