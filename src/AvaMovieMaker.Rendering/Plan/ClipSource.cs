using AvaMovieMaker.Time;

namespace AvaMovieMaker.Rendering.Plan;

public abstract record ClipSource;

public sealed record MediaSource(string Path, MediaTime SourceTime, bool IsPicture) : ClipSource;

public sealed record TitleSource(object Content, double LocalTime, double Duration) : ClipSource;

public sealed record BlackSource : ClipSource
{
    public static readonly BlackSource Instance = new();
}
