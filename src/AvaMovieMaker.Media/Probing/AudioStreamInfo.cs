namespace AvaMovieMaker.Media.Probing;

public sealed record AudioStreamInfo
{
    public int StreamIndex { get; init; }

    public int SampleRate { get; init; }

    public int Channels { get; init; }

    public string Codec { get; init; } = string.Empty;

    public string Layout { get; init; } = string.Empty;
}
