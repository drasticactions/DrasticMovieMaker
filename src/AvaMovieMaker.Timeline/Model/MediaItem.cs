using System.Text.Json.Serialization;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed record MediaItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public MediaKind Kind { get; init; }

    public string Path { get; init => field = value ?? string.Empty; } = string.Empty;

    public string? RelativePath { get; init; }

    public string Name { get; init => field = value ?? string.Empty; } = string.Empty;

    public MediaTime Duration { get; init; }

    public VideoProperties? Video { get; init; }

    public AudioProperties? Audio { get; init; }

    public IReadOnlyList<SourceClip> Clips { get; init => field = value ?? []; } = [];

    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.Now;

    public int ImportBatch { get; init; }

    public long FileSize { get; init; }

    public DateTime LastWriteTimeUtc { get; init; }

    public DateTimeOffset? DateTaken { get; init; }

    [JsonIgnore]
    public bool Missing { get; init; }

    public SourceClip? Clip(Guid id) => Clips.FirstOrDefault(c => c.Id == id);

    public bool HasAudio => Audio is not null;
}
