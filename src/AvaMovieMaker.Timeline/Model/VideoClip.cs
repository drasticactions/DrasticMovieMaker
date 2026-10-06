using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed record VideoClip
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public VideoClipKind Kind { get; init; }

    public Guid MediaId { get; init; }

    public Guid SourceClipId { get; init; }

    public MediaTime In { get; init; }

    public MediaTime Out { get; init; }

    public MediaTime? RangeIn { get; init; }

    public MediaTime? RangeOut { get; init; }

    public MediaTime StillDuration { get; init; }

    public IReadOnlyList<EffectRef> Effects { get; init; } = [];

    public TransitionRef? TransitionIn { get; init; }

    public AudioSettings Audio { get; init; } = AudioSettings.Default;

    public bool VideoFadeIn { get; init; }

    public bool VideoFadeOut { get; init; }

    public TitleContent? Title { get; init; }

    public double Speed => EffectCatalog.SpeedOf(Effects.Select(e => e.EffectId));

    public MediaTime Length => Kind == VideoClipKind.Video ? (Out - In) / Speed : StillDuration;

    public bool IsStill => Kind != VideoClipKind.Video;
}
