namespace AvaMovieMaker.Timeline.Model;

public sealed record ProjectProperties
{
    public string Title { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string Copyright { get; init; } = string.Empty;

    public string Rating { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
