using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Probing;

public sealed record MediaInfo
{
    public required string Path { get; init; }

    public MediaKind Kind { get; init; }

    public MediaTime Duration { get; init; }

    public VideoStreamInfo? Video { get; init; }

    public AudioStreamInfo? Audio { get; init; }

    public string Container { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public DateTime LastWriteTimeUtc { get; init; }

    public DateTimeOffset? DateTaken { get; init; }
}
