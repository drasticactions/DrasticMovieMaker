using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace AvaMovieMaker.ViewModels.Storyboard;

public sealed partial class StoryboardCellViewModel : ObservableObject
{
    public StoryboardCellViewModel(VideoClip clip, int index, string name)
    {
        Clip = clip;
        Index = index;
        Name = name;
    }

    public VideoClip Clip { get; }

    public int Index { get; }

    public string Name { get; }

    public bool HasEffects => Clip.Effects.Count > 0;

    public string EffectsText => string.Join(", ", Clip.Effects.Select(e => EffectCatalog.Find(e.EffectId)?.Name ?? e.EffectId));

    public bool HasTransition => Clip.TransitionIn is not null;

    public string? TransitionName => Clip.TransitionIn is { } t ? TransitionCatalog.Find(t.TransitionId)?.Name : null;

    public bool ShowTransitionSlot => Index > 0;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial SKBitmap? Thumbnail { get; private set; }

    public async Task LoadThumbnailAsync(ThumbnailService service, MediaItem? media, int width, int height)
    {
        if (Clip.Kind == VideoClipKind.Title && Clip.Title is not null)
        {
            Thumbnail = ThumbnailService.TitleThumbnail(Clip.Title, width, height);
            return;
        }

        if (media is null || media.Missing)
        {
            return;
        }

        Time.MediaTime at = Clip.Kind == VideoClipKind.Picture ? Time.MediaTime.Zero : Thumbnailer.RepresentativeTime(Clip.In, Clip.Out - Clip.In);
        Thumbnail = await service.GetAsync(media.Path, at, width, height).ConfigureAwait(true) ?? Thumbnail;
    }
}
