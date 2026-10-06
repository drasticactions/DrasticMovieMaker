namespace AvaMovieMaker.Timeline.AutoMovie;

public sealed record AutoMovieStyle
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<string?> Transitions { get; init; } = [];

    public IReadOnlyList<string> Effects { get; init; } = [];

    public string? TitleAnimation { get; init; } = "fade-in-and-out";

    public string? CreditsAnimation { get; init; } = "credits-scroll-up-stacked";

    public uint? TitleBackground { get; init; }

    public double QualityWeight { get; init; } = 0.5;

    public bool AllowShaky { get; init; }

    public int MusicLevel { get; init; } = 30;

    public bool AlwaysChronological { get; init; }

    public bool MusicVideo { get; init; }

    public bool EndCard { get; init; }

    public bool Scoreboard { get; init; }
}
