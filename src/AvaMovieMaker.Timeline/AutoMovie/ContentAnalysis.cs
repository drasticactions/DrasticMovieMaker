using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.AutoMovie;

public readonly record struct Subshot(int Shot, MediaTime Start, MediaTime Length, double Quality, double Motion = 0)
{
    public MediaTime End => Start + Length;
}

public sealed record SourceAnalysis
{
    public IReadOnlyList<Subshot> Subshots { get; init; } = [];

    public double PictureQuality { get; init; }

    public static SourceAnalysis Picture(double quality) => new() { PictureQuality = quality };

    public static SourceAnalysis Video(IReadOnlyList<Subshot> subshots) => new() { Subshots = subshots };
}

public readonly record struct Beat(MediaTime Time, double Strength, double Raw);

public sealed record MusicAnalysis(MediaTime Start, MediaTime End, IReadOnlyList<Beat> Beats)
{
    public MediaTime Length => End - Start;
}
