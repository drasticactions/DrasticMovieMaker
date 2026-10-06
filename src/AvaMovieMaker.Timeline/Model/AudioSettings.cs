namespace AvaMovieMaker.Timeline.Model;

public sealed record AudioSettings
{
    public static readonly AudioSettings Default = new();

    public const double MaxVolume = 1.5;

    public double Volume { get; init; } = 1.0;

    public bool Mute { get; init; }

    public bool FadeIn { get; init; }

    public bool FadeOut { get; init; }
}
