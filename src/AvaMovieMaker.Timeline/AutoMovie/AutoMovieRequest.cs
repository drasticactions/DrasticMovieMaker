using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.AutoMovie;

public sealed record AutoMovieSource(MediaItem Media, SourceClip Clip, SourceAnalysis? Analysis = null);

public sealed record AutoMovieRequest
{
    public required AutoMovieStyle Style { get; init; }

    public required IReadOnlyList<AutoMovieSource> Sources { get; init; }

    public string Title { get; init; } = string.Empty;

    public MediaItem? Music { get; init; }

    public MusicAnalysis? MusicAnalysis { get; init; }

    public double AudioLevels { get; init; }

    public string Author { get; init; } = string.Empty;

    public int Seed { get; init; } = Environment.TickCount;
}

public enum AutoMovieOutcome
{
    Created,

    NotEnoughContent,

    NoUsableContent,

    MusicTooShort,

    MusicTooQuiet,
}
