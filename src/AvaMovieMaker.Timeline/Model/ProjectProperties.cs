namespace AvaMovieMaker.Timeline.Model;

public sealed record ProjectProperties
{
    public string Title { get; init => field = value ?? string.Empty; } = string.Empty;

    public string Author { get; init => field = value ?? string.Empty; } = string.Empty;

    public string Copyright { get; init => field = value ?? string.Empty; } = string.Empty;

    public string Rating { get; init => field = value ?? string.Empty; } = string.Empty;

    public string Description { get; init => field = value ?? string.Empty; } = string.Empty;
}
