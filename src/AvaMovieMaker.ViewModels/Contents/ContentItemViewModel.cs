using System.Globalization;
using AvaMovieMaker.Media;
using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace AvaMovieMaker.ViewModels.Contents;

public sealed partial class ContentItemViewModel : ObservableObject
{
    public ContentItemViewModel(MediaItem media, SourceClip? clip)
    {
        Media = media;
        Clip = clip;
    }

    public MediaItem Media { get; }

    public SourceClip? Clip { get; }

    public string Key => $"{Media.Id}/{Clip?.Id}";

    public string Name => Clip is not null && Media.Clips.Count > 1 ? Clip.Name : Media.Name;

    public MediaKind Kind => Media.Kind;

    public bool IsMissing => Media.Missing;

    public MediaTime Duration => Media.Kind == MediaKind.Picture ? MediaTime.Zero : Clip?.Duration ?? Media.Duration;

    public string DurationText => Media.Kind == MediaKind.Picture ? string.Empty : TimeFormat.Format(Duration);

    public string StartText => Media.Kind == MediaKind.Picture ? string.Empty : TimeFormat.Format(Clip?.Start ?? MediaTime.Zero);

    public string EndText => Media.Kind == MediaKind.Picture ? string.Empty : TimeFormat.Format(Clip?.End ?? Media.Duration);

    public string Dimensions => Media.Video is { } v ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Dimensions, v.Width, v.Height) : string.Empty;

    public string DateTaken => Media.DateTaken?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty;

    public string TypeText => Media.Kind switch
    {
        MediaKind.Video => Strings.KindVideo,
        MediaKind.Audio => Strings.KindAudio,
        _ => Strings.KindPicture,
    };

    public string FileName => System.IO.Path.GetFileName(Media.Path);

    public string ToolTip => Media.Kind == MediaKind.Picture ? Name : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ContentToolTip, Name, DurationText);

    [ObservableProperty]
    public partial SKBitmap? Thumbnail { get; private set; }

    [ObservableProperty]
    public partial bool IsRenaming { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    public async Task LoadThumbnailAsync(ThumbnailService service, int width, int height, bool reload = false)
    {
        if ((Thumbnail is not null && !reload) || Media.Missing || Media.Kind == MediaKind.Audio)
        {
            return;
        }

        MediaTime at = Media.Kind == MediaKind.Picture ? MediaTime.Zero
            : Thumbnailer.RepresentativeTime(Clip?.Start ?? MediaTime.Zero, Duration);
        Thumbnail = await service.GetAsync(Media.Path, at, width, height).ConfigureAwait(true) ?? (reload ? Thumbnail : null);
    }
}
