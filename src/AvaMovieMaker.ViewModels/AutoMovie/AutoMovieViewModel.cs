using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.AutoMovie;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.AutoMovie;

public sealed partial class AutoMovieViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly ContentsViewModel _contents;
    private readonly IMessageBoxes _messages;
    private readonly Settings.AppSettings _settings;

    public AutoMovieViewModel(ProjectSession session, ContentsViewModel contents, IMessageBoxes messages, Settings.AppSettings settings)
    {
        _session = session;
        _contents = contents;
        _messages = messages;
        _settings = settings;
        SelectedStyle = AutoMovieStyles.Default;
    }

    public IReadOnlyList<AutoMovieStyle> Styles => AutoMovieStyles.All;

    [ObservableProperty]
    public partial AutoMovieStyle SelectedStyle { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial MediaItem? Music { get; set; }

    [ObservableProperty]
    public partial double AudioLevels { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = string.Empty;

    public IReadOnlyList<MediaItem> MusicChoices => _session.Project.Media.Where(m => m.Kind == MediaKind.Audio && !m.Missing).ToList();

    public event EventHandler? Closed;

    public event EventHandler? Created;

    public Func<Task<MediaItem?>>? BrowseMusic { get; set; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(MusicChoices));
        if (Music is { } m && !_session.Project.Media.Contains(m))
        {
            Music = null;
        }
    }

    public void Open()
    {
        Refresh();
        Page = 0;
        if (_contents.HasSelection && _contents.Selection.FirstOrDefault(s => s.Media.Kind == MediaKind.Audio && !s.Media.Missing).Media is { } music)
        {
            Music = music;
        }
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (BrowseMusic is not null && await BrowseMusic() is { } music)
        {
            Refresh();
            Music = music;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStylePage), nameof(IsTitlePage), nameof(IsMusicPage), nameof(Heading))]
    public partial int Page { get; set; }

    public bool IsStylePage => Page == 0;

    public bool IsTitlePage => Page == 1;

    public bool IsMusicPage => Page == 2;

    public string Heading => Page switch
    {
        1 => Strings.EnterTitleText,
        2 => Strings.AutoMovieHeadingMusic,
        _ => Strings.AutoMovieHeadingStyle,
    };

    [RelayCommand]
    private void ShowStyles() => Page = 0;

    [RelayCommand]
    private void ShowTitle() => Page = 1;

    [RelayCommand]
    private void ShowMusic() => Page = 2;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        var sources = (_contents.Selection.Count >= 2 ? _contents.Selection : _contents.Items.Select(i => (i.Media, i.Clip)).ToList())
            .Where(s => s.Media.Kind != MediaKind.Audio && !s.Media.Missing)
            .Select(s => (s.Media, Clip: s.Clip ?? s.Media.Clips.FirstOrDefault() ?? new SourceClip { End = s.Media.Duration }))
            .ToList();
        if (sources.Count == 0)
        {
            await _messages.ShowAsync(new MessageRequest(Strings.AutoMovieNeedsClips, MessageButtons.Ok, MessageIcon.Warning));
            return;
        }

        double picture = Math.Clamp(_session.Project.Settings.PictureDuration.Seconds, 2, 15);
        if (sources.Sum(s => s.Media.Kind == MediaKind.Picture ? picture : s.Clip.Duration.Seconds) < AutoMovieBuilder.MinimumSeconds)
        {
            await Fail(AutoMovieOutcome.NotEnoughContent);
            return;
        }

        if (Music is { } song && song.Duration.Seconds <= 30)
        {
            await Fail(AutoMovieOutcome.MusicTooShort);
            return;
        }

        IsBusy = true;
        try
        {
            Status = Strings.AutoMovieAnalyzingVideo;
            List<AutoMovieSource> analyzed;
            try
            {
                analyzed = await Task.Run(() => sources.Select(s => new AutoMovieSource(s.Media, s.Clip, Analyze(s.Media, s.Clip))).ToList());
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Error("automovie", "Video analysis failed", e);
                await _messages.ShowAsync(new MessageRequest(Strings.AutoMovieAnalyzeFailed, MessageButtons.Ok, MessageIcon.Error));
                return;
            }

            MusicAnalysis? beats = null;
            if (Music is { } m)
            {
                Status = Strings.AutoMovieAnalyzingMusic;
                try
                {
                    beats = await Task.Run(() => MusicAnalyzer.Analyze(m.Path));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Log.Error("automovie", "Music analysis failed", e);
                    await _messages.ShowAsync(new MessageRequest(Strings.AutoMovieMusicFailed, MessageButtons.Ok, MessageIcon.Error));
                    return;
                }
            }

            Status = Strings.AutoMovieCreating;
            string author = _session.Project.Properties.Author is { Length: > 0 } a ? a : _settings.DefaultAuthor;
            var request = new AutoMovieRequest
            {
                Style = SelectedStyle,
                Sources = analyzed,
                Title = Title,
                Music = Music,
                MusicAnalysis = beats,
                AudioLevels = AudioLevels,
                Author = author,
                Seed = Environment.TickCount & 0x7FFFFFFF,
            };
            AutoMovieOutcome outcome = AutoMovieBuilder.Build(_session.Editor, request);
            if (outcome == AutoMovieOutcome.Created)
            {
                Created?.Invoke(this, EventArgs.Empty);
                Closed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await Fail(outcome);
            }
        }
        catch (Exception e)
        {
            Log.Error("automovie", "AutoMovie failed", e);
            await _messages.ShowAsync(new MessageRequest(e.Message, MessageButtons.Ok, MessageIcon.Error));
        }
        finally
        {
            IsBusy = false;
            Status = string.Empty;
        }
    }

    private Task Fail(AutoMovieOutcome outcome) => _messages.ShowAsync(new MessageRequest(outcome switch
    {
        AutoMovieOutcome.NoUsableContent => Strings.AutoMovieNoUsableContent,
        AutoMovieOutcome.MusicTooShort => Strings.AutoMovieMusicTooShort,
        AutoMovieOutcome.MusicTooQuiet => Strings.AutoMovieMusicTooQuiet,
        _ => Strings.AutoMovieNotEnoughContent,
    }, MessageButtons.Ok, MessageIcon.Error));

    private SourceAnalysis? Analyze(MediaItem media, SourceClip clip)
    {
        if (!FileStore.Current.Exists(media.Path))
        {
            return null;
        }

        var key = (media.Path, media.LastWriteTimeUtc, clip.Start, clip.End);
        lock (_analyzes)
        {
            if (_analyzes.TryGetValue(key, out SourceAnalysis? cached))
            {
                return cached;
            }
        }

        SourceAnalysis result = media.Kind == MediaKind.Picture
            ? ContentAnalyzer.AnalyzePicture(media.Path)
            : ContentAnalyzer.AnalyzeVideo(media.Path, clip.Start, clip.End);
        lock (_analyzes)
        {
            _analyzes[key] = result;
        }

        return result;
    }

    private readonly Dictionary<(string, DateTime, MediaTime, MediaTime), SourceAnalysis> _analyzes = [];

    partial void OnSelectedStyleChanged(AutoMovieStyle value) => AudioLevels = value.MusicLevel / 50.0 - 1;

    private bool CanCreate() => !IsBusy;

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, EventArgs.Empty);
}
