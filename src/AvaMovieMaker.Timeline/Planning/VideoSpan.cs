using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Rendering.Plan;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Planning;

public sealed record VideoSpan
{
    public required Guid ClipId { get; init; }

    public required MediaTime Start { get; init; }

    public required MediaTime Length { get; init; }

    public MediaTime End => Start + Length;

    public string? Path { get; init; }

    public bool IsPicture { get; init; }

    public TitleContent? Title { get; init; }

    public MediaTime SourceIn { get; init; }

    public double Speed { get; init; } = 1.0;

    public IReadOnlyList<string> Effects { get; init; } = [];

    public string? TransitionId { get; init; }

    public MediaTime TransitionLength { get; init; }

    public bool FadeIn { get; init; }

    public bool FadeOut { get; init; }

    public int Seed { get; init; }

    public static readonly MediaTime VideoFadeLength = MediaTime.FromSeconds(1);

    public MediaTime SourceTimeAt(MediaTime t) => SourceIn + (t - Start) * Speed;

    public ClipInput InputAt(MediaTime t)
    {
        double local = (t - Start).Seconds;
        double len = Length.Seconds;
        ClipSource source = Title is not null ? new TitleSource(Title, local, len)
            : Path is null ? BlackSource.Instance
            : new MediaSource(Path, IsPicture ? MediaTime.Zero : SourceTimeAt(t), IsPicture);
        var effects = Effects.Select(e => new EffectInstance(e, local, len)).ToList();
        float level = 1f;
        double fade = Math.Min(VideoFadeLength.Seconds, len / 2);
        if (FadeIn && fade > 0)
        {
            level = Math.Min(level, (float)Math.Clamp(local / fade, 0, 1));
        }

        if (FadeOut && fade > 0)
        {
            level = Math.Min(level, (float)Math.Clamp((len - local) / fade, 0, 1));
        }

        return new ClipInput(source, effects, Seed, level);
    }
}
