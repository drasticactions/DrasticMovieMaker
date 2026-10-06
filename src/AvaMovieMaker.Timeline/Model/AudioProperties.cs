namespace AvaMovieMaker.Timeline.Model;

public sealed record AudioProperties
{
    public int SampleRate { get; init; }

    public int Channels { get; init; }

    public string Codec { get; init; } = string.Empty;
}
