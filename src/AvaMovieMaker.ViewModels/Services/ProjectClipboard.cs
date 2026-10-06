using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.ViewModels.Services;

public sealed class ProjectClipboard
{
    public IReadOnlyList<VideoClip> Video { get; private set; } = [];

    public IReadOnlyList<AudioClip> Audio { get; private set; } = [];

    public IReadOnlyList<TitleClip> Titles { get; private set; } = [];

    public IReadOnlyList<MediaItem> Media { get; private set; } = [];

    public string? TransitionId { get; private set; }

    public string? EffectId { get; private set; }

    public IReadOnlyList<(MediaItem Media, SourceClip? Clip)> Items { get; private set; } = [];

    public bool IsEmpty => Video.Count == 0 && Audio.Count == 0 && Titles.Count == 0 && Items.Count == 0 && TransitionId is null && EffectId is null;

    public event EventHandler? Changed;

    public void Set(IReadOnlyList<VideoClip> video, IReadOnlyList<AudioClip> audio, IReadOnlyList<TitleClip> titles, IReadOnlyList<MediaItem> media)
    {
        Clear();
        Video = video;
        Audio = audio;
        Titles = titles;
        Media = media;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCatalog(string? transitionId, string? effectId)
    {
        Clear();
        TransitionId = transitionId;
        EffectId = effectId;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetItems(IReadOnlyList<(MediaItem Media, SourceClip? Clip)> items)
    {
        Clear();
        Items = items;
        Media = [.. items.Select(i => i.Media).Distinct()];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Clear()
    {
        Video = [];
        Audio = [];
        Titles = [];
        Media = [];
        Items = [];
        TransitionId = null;
        EffectId = null;
    }
}
