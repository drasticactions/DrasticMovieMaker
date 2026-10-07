using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using AvaMovieMaker.Timeline.Playback;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Preview;

public sealed partial class MonitorViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly EngineHost _engine;
    private readonly IDispatcher _dispatcher;
    private RenderPlan? _itemPlan;
    private (MediaItem Media, SourceClip? Clip)? _item;
    private bool _syncingPosition;
    private bool _hadDuration;
    private MediaTime _projectPosition;
    private IPreviewSurface? _surface;

    public MonitorViewModel(ProjectSession session, EngineHost engine, IDispatcher dispatcher)
    {
        _session = session;
        _engine = engine;
        _dispatcher = dispatcher;
        Playback.FrameReady += f =>
        {
            if (Surface is { } surface)
            {
                surface.Present(f);
            }
            else
            {
                f.Drop();
            }
        };

        Playback.PositionChanged += _ => _dispatcher.Post(() => SyncPosition(Playback.Position));
        Playback.StateChanged += (_, _) => _dispatcher.Post(SyncState);
        session.Changed += (_, _) =>
        {
            if (Target == PreviewTarget.Project)
            {
                Playback.Plan = session.Plan;
                HasVideo = ProjectHasVideo;
                RefreshCaption();
                SyncPosition(Playback.Position);
            }

            PlayProjectCommand.NotifyCanExecuteChanged();
            RewindCommand.NotifyCanExecuteChanged();
            SyncAspect();
        };
        session.Replaced += (_, _) =>
        {
            SyncAspect();
            ShowProject(seekStart: true);
        };
        _aspect = DisplayAspect;
        Playback.Plan = session.Plan;
        HasVideo = ProjectHasVideo;
        RenderingDescription = engine.Device.IsGpu ? string.Empty : Strings.SoftwareRendering;
        SyncPosition(MediaTime.Zero);
    }

    public PlaybackEngine Playback => _engine.Playback;

    public IPreviewSurface? Surface
    {
        get => _surface;
        set
        {
            if (ReferenceEquals(_surface, value))
            {
                return;
            }

            if (_surface is not null)
            {
                _surface.SharedFrameDeviceChanged -= OnSharedFrameDeviceChanged;
                _surface.ComposesFramesChanged -= OnComposesFramesChanged;
            }

            _surface = value;
            if (value is not null)
            {
                value.SharedFrameDeviceChanged += OnSharedFrameDeviceChanged;
                value.ComposesFramesChanged += OnComposesFramesChanged;
            }

            OnSharedFrameDeviceChanged(this, EventArgs.Empty);
            OnComposesFramesChanged(this, EventArgs.Empty);
        }
    }

    private async void OnSharedFrameDeviceChanged(object? sender, EventArgs e)
    {
        IPreviewSurface? surface = _surface;
        byte[]? device = surface?.SharedFrameDevice;
        try
        {
            await Playback.UseSharedFramesAsync(device).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn("preview", $"Switching shared frames failed: {ex.Message}");
            return;
        }

        if (device is not null && !Playback.UsesSharedFrames)
        {
            _dispatcher.Post(() =>
            {
                if (ReferenceEquals(surface, _surface) && ReferenceEquals(device, surface!.SharedFrameDevice))
                {
                    surface.SharedFramesRefused();
                }
            });
        }
    }

    private void OnComposesFramesChanged(object? sender, EventArgs e) => Playback.UiComposes = _surface?.ComposesFrames ?? false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectPlaying))]
    public partial PreviewTarget Target { get; private set; }

    [ObservableProperty]
    public partial string Caption { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseTip), nameof(PlayClipText), nameof(IsProjectPlaying))]
    public partial bool IsPlaying { get; private set; }

    public bool IsProjectPlaying => IsPlaying && Target == PreviewTarget.Project;

    public string PlayClipText => IsPlaying ? Strings.PauseClip : Strings.PlayClip;

    [ObservableProperty]
    public partial bool HasVideo { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public string SourceName { get; private set; } = string.Empty;

    public (MediaItem Media, SourceClip? Clip)? Item => _item;

    public MediaTime ProjectPosition => Target == PreviewTarget.Project ? Playback.Position : _projectPosition;

    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    [ObservableProperty]
    public partial double DurationSeconds { get; private set; }

    [ObservableProperty]
    public partial string TimeText { get; private set; } = string.Empty;

    public string RenderingDescription { get; }

    public string PlayPauseTip => IsPlaying ? Strings.PauseTip : Strings.PlayTip;

    public MediaTime Position => Playback.Position;

    public MediaTime Duration => Playback.Duration;

    public double DisplayAspect => _session.Project.Settings.DisplayAspect;

    private double _aspect;

    private void SyncAspect()
    {
        if (DisplayAspect != _aspect)
        {
            _aspect = DisplayAspect;
            OnPropertyChanged(nameof(DisplayAspect));
        }
    }

    public Func<string>? ProjectCaption { get; set; }

    public Func<int, bool>? ProjectStep { get; set; }

    public event EventHandler<MediaTime>? PositionChanged;

    public event EventHandler? SourceChanged;

    private bool ProjectHasVideo => _session.Project.VideoTrack.Count > 0 || _session.Project.TitleOverlayTrack.Count > 0;

    partial void OnPositionSecondsChanged(double value)
    {
        if (!_syncingPosition)
        {
            Playback.Scrub(MediaTime.FromSeconds(value));
            UpdateTimeText(MediaTime.FromSeconds(value));
        }
    }

    partial void OnHasVideoChanged(bool value) => FullScreenCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value) => FullScreenCommand.NotifyCanExecuteChanged();

    private void SyncPosition(MediaTime t)
    {
        _syncingPosition = true;
        DurationSeconds = Duration.Seconds;
        PositionSeconds = t.Seconds;
        _syncingPosition = false;
        UpdateTimeText(t);
        NotifyTransport();
        if (Target == PreviewTarget.Project)
        {
            PositionChanged?.Invoke(this, t);
        }
    }

    public void RefreshCaption()
    {
        if (Target == PreviewTarget.Project)
        {
            Caption = ProjectCaption?.Invoke() ?? string.Empty;
        }
    }

    private void UpdateTimeText(MediaTime t) => TimeText = TimeFormat.FormatPair(MediaTime.Min(t, Duration), Duration);

    private void SyncState()
    {
        IsPlaying = Playback.State == PlaybackState.Playing;
        SyncPosition(Playback.Position);
    }

    private void NotifyTransport()
    {
        bool has = Duration > MediaTime.Zero;
        if (has == _hadDuration)
        {
            return;
        }

        _hadDuration = has;
        foreach (IRelayCommand c in new IRelayCommand[] { PlayPauseCommand, StopCommand, BackCommand, ForwardCommand, PreviousFrameCommand, NextFrameCommand })
        {
            c.NotifyCanExecuteChanged();
        }
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double aspect = DisplayAspect;
        int w = width, h = (int)Math.Round(width / aspect);
        if (h > height)
        {
            h = height;
            w = (int)Math.Round(height * aspect);
        }

        Playback.SetPreviewSize(Math.Max(16, w & ~1), Math.Max(16, h & ~1));
    }

    public void ShowProject(bool seekStart = false)
    {
        bool switching = Target != PreviewTarget.Project;
        if (switching)
        {
            Playback.Pause();
        }

        Target = PreviewTarget.Project;
        _item = null;
        _itemPlan = null;
        SourceName = string.Empty;
        Playback.Plan = _session.Plan;
        HasVideo = ProjectHasVideo;
        if (seekStart)
        {
            _projectPosition = MediaTime.Zero;
            Playback.Seek(MediaTime.Zero);
        }
        else if (switching)
        {
            Playback.Seek(_projectPosition);
        }

        RefreshCaption();
        SyncPosition(Playback.Position);
        if (switching)
        {
            SourceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ShowItem(MediaItem media, SourceClip? clip, bool play)
    {
        Load(RenderPlanner.ForMedia(media, clip, _session.Project.Settings), clip?.Name ?? media.Name, play, media.Kind != Media.MediaKind.Audio);
        _item = (media, clip);
    }

    public void ShowPlan(RenderPlan plan, string caption, bool play) => Load(plan, caption, play, hasVideo: true);

    private void Load(RenderPlan plan, string name, bool play, bool hasVideo)
    {
        if (Target == PreviewTarget.Project)
        {
            _projectPosition = Playback.Position;
        }

        Playback.Pause();
        Target = PreviewTarget.Item;
        _item = null;
        Caption = name;
        SourceName = name;
        _itemPlan = plan;
        Playback.Plan = plan;
        HasVideo = hasVideo;
        Playback.Seek(MediaTime.Zero);
        if (play)
        {
            Playback.Play();
        }

        SyncPosition(Playback.Position);
        SourceChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanPlay() => Duration > MediaTime.Zero;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void PlayPause() => Playback.TogglePlayPause();

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Stop() => Playback.Stop();

    private bool CanPlayProject() => !_session.Project.IsEmpty && _session.Plan.Duration > MediaTime.Zero;

    [RelayCommand(CanExecute = nameof(CanPlayProject))]
    private void PlayProject()
    {
        if (Target != PreviewTarget.Project)
        {
            Project p = _session.Project;
            int i = TimelineLayout.Compute(p).IndexAt(_projectPosition);
            if (i >= 0)
            {
                _session.Select([p.VideoTrack[i].Id], Timeline.TimelineTrack.Video);
            }

            ShowProject();
            Playback.Play();
            return;
        }

        Playback.TogglePlayPause();
    }

    [RelayCommand(CanExecute = nameof(CanPlayProject))]
    private void Rewind()
    {
        ShowProject();
        Playback.Pause();
        Playback.Seek(MediaTime.Zero);
        if (_session.Project.VideoTrack.FirstOrDefault() is { } first)
        {
            _session.Select([first.Id], Timeline.TimelineTrack.Video);
        }
    }

    private MediaTime HalfFrame
    {
        get
        {
            double fps = _session.Project.Settings.FrameRate.ToDouble();
            return MediaTime.FromSeconds(0.5 / (fps > 0 ? fps : 30));
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void PreviousFrame()
    {
        if (Playback.Position <= HalfFrame)
        {
            Back();
            return;
        }

        Playback.StepFrame(-1);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void NextFrame()
    {
        if (Playback.Position >= Duration - HalfFrame)
        {
            Forward();
            return;
        }

        Playback.StepFrame(1);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Back()
    {
        if (Target == PreviewTarget.Project && ProjectStep is { } step)
        {
            step(-1);
            return;
        }

        Playback.Seek(MediaTime.Zero);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Forward()
    {
        if (Target == PreviewTarget.Project && ProjectStep is { } step)
        {
            step(1);
            return;
        }

        Playback.Seek(Duration);
    }

    private void SelectSecondHalf(MediaTime t)
    {
        Project p = _session.Project;
        MediaTime tol = MediaTime.FromSeconds(0.001);
        if (p.AudioMusicTrack.FirstOrDefault(a => Math.Abs((a.Start - t).Seconds) < tol.Seconds && _session.SelectionTrack == Timeline.TimelineTrack.AudioMusic) is { } a)
        {
            _session.Select([a.Id], Timeline.TimelineTrack.AudioMusic);
        }
        else if (p.TitleOverlayTrack.FirstOrDefault(o => Math.Abs((o.Start - t).Seconds) < tol.Seconds && _session.SelectionTrack == Timeline.TimelineTrack.TitleOverlay) is { } o)
        {
            _session.Select([o.Id], Timeline.TimelineTrack.TitleOverlay);
        }
        else if (TimelineLayout.Compute(p).IndexAt(t) is var i and >= 0)
        {
            _session.Select([p.VideoTrack[i].Id], Timeline.TimelineTrack.Video);
        }
    }

    [RelayCommand]
    private void Split()
    {
        MediaTime t = Playback.Position;
        if (Target == PreviewTarget.Project)
        {
            if (_session.Editor.SplitAt(t, _session.SelectedClips.FirstOrDefault()))
            {
                SelectSecondHalf(t);
            }

            return;
        }

        if (_item is { } item && item.Media.Kind != Media.MediaKind.Picture)
        {
            SourceClip clip = item.Clip ?? item.Media.Clips[0];
            MediaTime at = clip.Start + t;
            if (at <= clip.Start || at >= clip.End)
            {
                return;
            }

            int index = item.Media.Clips.ToList().FindIndex(c => c.Id == clip.Id);
            var first = clip with { End = at };
            var second = clip with { Id = Guid.NewGuid(), Start = at, Name = clip.Name + " (2)" };
            var clips = item.Media.Clips.ToList();
            clips[index] = first;
            clips.Insert(index + 1, second);
            _session.Editor.SetSourceClips(item.Media.Id, clips);
        }
    }

    private bool CanFullScreen() => HasVideo && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanFullScreen))]
    private void FullScreen()
    {
        FullScreenRequested?.Invoke(this, EventArgs.Empty);
        if (Playback.State != PlaybackState.Playing)
        {
            Playback.Play();
        }
    }

    public event EventHandler? FullScreenRequested;

    public async Task TakePictureAsync(string path, string author)
    {
        (int w, int h) = _session.Project.Settings.PreviewSize;
        byte[] jpeg = await Playback.EncodeFrameAsync(Playback.Position, w, h, jpeg: true);
        jpeg = JpegMetadata.Insert(jpeg, Path.GetFileNameWithoutExtension(path), author, Strings.AppName, DateTime.Now);
        FileStore.WriteAllBytes(path, jpeg);
    }

    public void Seek(MediaTime t) => Playback.Seek(t);
}
