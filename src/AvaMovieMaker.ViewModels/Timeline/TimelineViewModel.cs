using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Timeline;

public sealed partial class TimelineViewModel : ObservableObject
{
    public const double SnapPixels = 7;

    public bool SnapDisabled { get; set; }

    private readonly ProjectSession _session;
    private (Guid Id, TimelineTrack Track, TrimEdge Edge, MediaTime In, MediaTime Out, MediaTime Start, MediaTime Length)? _trim;
    private (Guid Id, TimelineTrack Track, MediaTime Grab, MediaTime Start)? _move;

    public TimelineViewModel(ProjectSession session, WaveformService waveforms, ThumbnailService thumbnails)
    {
        _session = session;
        Waveforms = waveforms;
        Thumbnails = thumbnails;
        session.Changed += (_, _) => Invalidate();
        session.SelectionChanged += (_, _) => Invalidate();
        waveforms.Ready += _ => Invalidate();
    }

    public ProjectSession Session => _session;

    public Project Project => _session.Project;

    public WaveformService Waveforms { get; }

    public ThumbnailService Thumbnails { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PixelsPerSecond))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    public partial int ZoomStep { get; set; } = TimelineZoom.DefaultStep;

    public double PixelsPerSecond => TimelineZoom.PixelsPerSecond[TimelineZoom.Clamp(ZoomStep)];

    [ObservableProperty]
    public partial double ScrollSeconds { get; set; }

    [ObservableProperty]
    public partial bool IsVideoExpanded { get; set; }

    [ObservableProperty]
    public partial MediaTime Playhead { get; set; }

    public event EventHandler? Invalidated;

    public event EventHandler<MediaTime>? SeekRequested;

    public void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);

    public TimelineLayout Layout => TimelineLayout.Compute(Project);

    public MediaTime SnapTolerance => MediaTime.FromSeconds(SnapPixels / PixelsPerSecond);

    private MediaTime SnapDrag(MediaTime t, Guid ignore)
    {
        if (!SnapDisabled)
        {
            t = _session.Editor.Snap(t, SnapTolerance, Playhead, ignore);
        }

        return t.SnapToFrame(Project.Settings.FrameRate);
    }

    public double ToPixels(MediaTime t) => (t.Seconds - ScrollSeconds) * PixelsPerSecond;

    public MediaTime ToTime(double x) => MediaTime.FromSeconds(Math.Max(0, x / PixelsPerSecond + ScrollSeconds));

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn() => ZoomStep = TimelineZoom.Clamp(ZoomStep + 1);

    private bool CanZoomIn() => ZoomStep < TimelineZoom.PixelsPerSecond.Length - 1;

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut() => ZoomStep = TimelineZoom.Clamp(ZoomStep - 1);

    private bool CanZoomOut() => ZoomStep > 0;

    public void ZoomToFit(double width)
    {
        ZoomStep = TimelineZoom.Fit(Project.Duration.Seconds, width, ZoomStep);
        ScrollSeconds = 0;
    }

    [RelayCommand]
    private void ToggleVideoExpanded() => IsVideoExpanded = !IsVideoExpanded;

    partial void OnZoomStepChanged(int value) => Invalidate();

    partial void OnScrollSecondsChanged(double value) => Invalidate();

    partial void OnIsVideoExpandedChanged(bool value)
    {
        if (!value && ActiveTrack is TimelineTrack.Transition or TimelineTrack.Audio)
        {
            ActiveTrack = TimelineTrack.Video;
        }

        Invalidate();
    }

    partial void OnPlayheadChanged(MediaTime value) => Invalidate();

    public void RequestSeek(MediaTime t) => SeekRequested?.Invoke(this, t);

    public bool IsSelected(Guid id) => IsSelected(id, TimelineTrack.Video);

    public bool IsSelected(Guid id, TimelineTrack track) =>
        _session.SelectionTrack == track && _session.SelectedClips.Contains(id);

    public List<(Guid Id, MediaTime Start)> TrackItems(TimelineTrack track)
    {
        Project p = Project;
        TimelineLayout l = Layout;
        return track switch
        {
            TimelineTrack.AudioMusic => [.. p.AudioMusicTrack.OrderBy(a => a.Start).Select(a => (a.Id, a.Start))],
            TimelineTrack.TitleOverlay => [.. p.TitleOverlayTrack.OrderBy(t => t.Start).Select(t => (t.Id, t.Start))],
            TimelineTrack.Transition => [.. Enumerable.Range(1, Math.Max(0, l.Count - 1)).Where(i => l.Transitions[i] > MediaTime.Zero).Select(i => (p.VideoTrack[i].Id, l.Starts[i]))],
            TimelineTrack.Audio => [.. Enumerable.Range(0, l.Count).Where(i => p.VideoTrack[i].Kind == VideoClipKind.Video && p.FindMedia(p.VideoTrack[i].MediaId)?.HasAudio == true).Select(i => (p.VideoTrack[i].Id, l.Starts[i]))],
            _ => [.. Enumerable.Range(0, l.Count).Select(i => (p.VideoTrack[i].Id, l.Starts[i]))],
        };
    }

    private TrackSelection.State StateOn(TimelineTrack track) =>
        _session.SelectionTrack == track ? _session.SelectionState : new TrackSelection.State([], null, null);

    public void Click(TimelineTrack track, Guid id, bool ctrl, bool shift)
    {
        ActiveTrack = track;
        List<(Guid Id, MediaTime Start)> items = TrackItems(track);
        _session.Select(TrackSelection.Click([.. items.Select(i => i.Id)], StateOn(track), id, ctrl, shift), track);
        if (!ctrl && !shift && items.FirstOrDefault(i => i.Id == id) is { Id: var found } item && found == id)
        {
            RequestSeek(item.Start);
        }
    }

    public void Collapse(TimelineTrack track, Guid id) => _session.Select(TrackSelection.Collapse(StateOn(track), id), track);

    public void SelectRange(TimelineTrack track, MediaTime from, MediaTime to, bool add)
    {
        (MediaTime lo, MediaTime hi) = from <= to ? (from, to) : (to, from);
        Project p = Project;
        TimelineLayout l = Layout;
        IEnumerable<(Guid Id, MediaTime Start, MediaTime End)> spans = track switch
        {
            TimelineTrack.AudioMusic => p.AudioMusicTrack.Select(a => (a.Id, a.Start, a.End)),
            TimelineTrack.TitleOverlay => p.TitleOverlayTrack.Select(t => (t.Id, t.Start, t.End)),
            TimelineTrack.Transition => Enumerable.Range(1, Math.Max(0, l.Count - 1)).Where(i => l.Transitions[i] > MediaTime.Zero).Select(i => (p.VideoTrack[i].Id, l.Starts[i], l.Starts[i] + l.Transitions[i])),
            _ => Enumerable.Range(0, l.Count).Select(i => (p.VideoTrack[i].Id, l.Starts[i], l.End(i))),
        };
        var hit = spans.Where(x => x.Start < hi && x.End > lo).OrderBy(x => x.Start).Select(x => x.Id).ToList();
        IReadOnlyList<Guid> selected = add ? [.. StateOn(track).Selected.Union(hit)] : hit;
        ActiveTrack = track;
        _session.Select(new TrackSelection.State(selected, hit.FirstOrDefault(), hit.Count > 0 ? hit[^1] : null), track);
    }

    public void SelectAll()
    {
        var ids = TrackItems(ActiveTrack).Select(i => i.Id).ToList();
        _session.Select(new TrackSelection.State(ids, ids.FirstOrDefault(), ids.Count > 0 ? ids[^1] : null), ActiveTrack);
    }

    public bool Key(SelectionKey key, bool shift = false, bool ctrl = false)
    {
        List<(Guid Id, MediaTime Start)> items = TrackItems(ActiveTrack);
        if (items.Count == 0 && key != SelectionKey.Clear)
        {
            return false;
        }

        Guid? under = items.LastOrDefault(i => i.Start <= Playhead) is { Id: var u } && u != Guid.Empty ? u : null;
        TrackSelection.State next = TrackSelection.Key([.. items.Select(i => i.Id)], StateOn(ActiveTrack), key, shift, ctrl, under, out Guid? moved);
        _session.Select(next, ActiveTrack);
        if (moved is { } m)
        {
            RequestSeek(items.First(i => i.Id == m).Start);
        }

        return true;
    }

    public bool SelectClip(int step, bool toEnd = false, bool extend = false) =>
        Key(toEnd ? (step < 0 ? SelectionKey.First : SelectionKey.Last) : step < 0 ? SelectionKey.Previous : SelectionKey.Next, shift: extend);

    public void ClearSelection() => _session.Select(new TrackSelection.State([], null, null), ActiveTrack);

    [ObservableProperty]
    public partial TimelineTrack ActiveTrack { get; set; } = TimelineTrack.Video;

    partial void OnActiveTrackChanged(TimelineTrack value) => Invalidate();

    public IReadOnlyList<TimelineTrack> VisibleTracks => IsVideoExpanded
        ? [TimelineTrack.Video, TimelineTrack.Transition, TimelineTrack.Audio, TimelineTrack.AudioMusic, TimelineTrack.TitleOverlay]
        : [TimelineTrack.Video, TimelineTrack.AudioMusic, TimelineTrack.TitleOverlay];

    public void ActivateTrack(TimelineTrack track)
    {
        if (track is TimelineTrack.Transition or TimelineTrack.Audio)
        {
            IsVideoExpanded = true;
        }

        ActiveTrack = track;
        _session.ActiveSelection = SelectionKind.Timeline;
    }

    public void MoveTrack(int step)
    {
        IReadOnlyList<TimelineTrack> tracks = VisibleTracks;
        int i = Math.Max(0, tracks.ToList().IndexOf(ActiveTrack));
        ActiveTrack = tracks[Math.Clamp(i + step, 0, tracks.Count - 1)];
    }

    public void BeginTrim(Guid id, TimelineTrack track, TrimEdge edge)
    {
        TimelineLayout layout = Layout;
        switch (track)
        {
            case TimelineTrack.Video:
            {
                int i = Project.IndexOfVideo(id);
                VideoClip c = Project.VideoTrack[i];
                _trim = (id, track, edge, c.In, c.Out, layout.Starts[i], layout.Lengths[i]);
                break;
            }

            case TimelineTrack.AudioMusic:
            {
                AudioClip a = Project.AudioMusicTrack.First(x => x.Id == id);
                _trim = (id, track, edge, a.In, a.Out, a.Start, a.Length);
                break;
            }

            case TimelineTrack.TitleOverlay:
            {
                TitleClip t = Project.TitleOverlayTrack.First(x => x.Id == id);
                _trim = (id, track, edge, MediaTime.Zero, t.Duration, t.Start, t.Duration);
                break;
            }

            case TimelineTrack.Transition:
            {
                int i = Project.IndexOfVideo(id);
                _trim = (id, track, edge, MediaTime.Zero, layout.Transitions[i], layout.Starts[i], layout.Transitions[i]);
                break;
            }

            default:
                return;
        }

        bool still = track == TimelineTrack.Transition || track == TimelineTrack.TitleOverlay
            || (track == TimelineTrack.Video && Project.VideoTrack[Project.IndexOfVideo(id)].IsStill);
        _session.Undo.BeginGesture(still ? UndoNames.SetDuration : edge == TrimEdge.Start ? UndoNames.TrimStart : UndoNames.TrimEnd);
    }

    public void UpdateTrim(MediaTime t)
    {
        if (_trim is not { } tr)
        {
            return;
        }

        t = SnapDrag(t, tr.Id);
        MediaTime delta = tr.Edge == TrimEdge.Start ? t - tr.Start : t - (tr.Start + tr.Length);
        TimelineEditor e = _session.Editor;
        switch (tr.Track)
        {
            case TimelineTrack.Video:
            {
                VideoClip c = Project.VideoTrack[Project.IndexOfVideo(tr.Id)];
                if (c.IsStill)
                {
                    MediaTime len = tr.Edge == TrimEdge.End ? tr.Length + delta : tr.Length - delta;
                    e.SetStillDuration(tr.Id, len, "trim");
                }
                else if (tr.Edge == TrimEdge.Start)
                {
                    e.TrimVideo(tr.Id, tr.In + delta * c.Speed, tr.Out, "trim");
                }
                else
                {
                    e.TrimVideo(tr.Id, tr.In, tr.Out + delta * c.Speed, "trim");
                }

                break;
            }

            case TimelineTrack.AudioMusic:
                if (tr.Edge == TrimEdge.Start)
                {
                    e.TrimAudioClip(tr.Id, tr.In + delta, tr.Out, "trim");
                }
                else
                {
                    e.TrimAudioClip(tr.Id, tr.In, tr.Out + delta, "trim");
                }

                break;
            case TimelineTrack.TitleOverlay:
                if (tr.Edge == TrimEdge.End)
                {
                    e.SetTitleClipDuration(tr.Id, tr.Length + delta, "trim");
                }

                break;
            case TimelineTrack.Transition:
                e.SetTransitionDuration(tr.Id, tr.Length - delta, "trim");
                break;
        }
    }

    public void EndTrim()
    {
        if (_trim is not null)
        {
            _trim = null;
            _session.Undo.EndGesture();
        }
    }

    public void CancelGesture()
    {
        if (_trim is not null || _move is not null)
        {
            _trim = null;
            _move = null;
            _session.Undo.CancelGesture();
        }
    }

    public void BeginMove(Guid id, TimelineTrack track, MediaTime grab)
    {
        MediaTime start = track switch
        {
            TimelineTrack.AudioMusic => Project.AudioMusicTrack.First(a => a.Id == id).Start,
            TimelineTrack.TitleOverlay => Project.TitleOverlayTrack.First(t => t.Id == id).Start,
            _ => Layout.Starts[Project.IndexOfVideo(id)],
        };
        _move = (id, track, grab, start);
        _session.Undo.BeginGesture(UndoNames.MoveClip);
    }

    public void UpdateMove(MediaTime t)
    {
        if (_move is not { } mv)
        {
            return;
        }

        MediaTime start = mv.Start + (t - mv.Grab);
        start = SnapDrag(start, mv.Id);
        switch (mv.Track)
        {
            case TimelineTrack.AudioMusic:
                _session.Editor.MoveAudioClip(mv.Id, start, "move");
                break;
            case TimelineTrack.TitleOverlay:
                _session.Editor.MoveTitleClip(mv.Id, start, "move");
                break;
        }
    }

    public void EndMove(MediaTime t, TimelineTrack? onto = null)
    {
        if (_move is not { } mv)
        {
            return;
        }

        if (mv.Track == TimelineTrack.TitleOverlay && onto == TimelineTrack.Video && _session.Editor.MoveTitleToVideo(mv.Id, Layout.InsertIndexAt(t)))
        {
            Click(TimelineTrack.Video, mv.Id, ctrl: false, shift: false);
        }
        else if (mv.Track == TimelineTrack.Video)
        {
            var ids = _session.SelectedClips.Contains(mv.Id) ? _session.SelectedClips.ToList() : [mv.Id];
            _session.Editor.MoveVideoClips(ids, Layout.InsertIndexAt(t));
        }

        _move = null;
        _session.Undo.EndGesture();
    }

    public Guid? SelectedVideoTitle =>
        _session.SelectionTrack == TimelineTrack.Video && _session.SelectedClips.Count == 1 &&
        Project.VideoTrack.FirstOrDefault(c => c.Id == _session.SelectedClips[0]) is { Kind: VideoClipKind.Title } c ? c.Id : null;

    public bool DropTitleOnOverlay(Guid id, MediaTime t, bool copy = false)
    {
        if (!_session.Editor.MoveTitleToOverlay(id, SnapDrag(t, id), copy, out Guid placed))
        {
            return false;
        }

        Click(TimelineTrack.TitleOverlay, placed, ctrl: false, shift: false);
        return true;
    }

    public bool DropMedia(TimelineTrack track, MediaTime t, IReadOnlyList<(MediaItem Media, SourceClip? Clip)> items)
    {
        TimelineEditor e = _session.Editor;
        if (track == TimelineTrack.AudioMusic)
        {
            var added = new List<Guid>();
            MediaTime at = SnapDrag(t, Guid.Empty);
            foreach ((MediaItem m, SourceClip? c) in items.Where(i => i.Media.Kind == Media.MediaKind.Audio && !i.Media.Missing))
            {
                var before = Project.AudioMusicTrack.Select(a => a.Id).ToHashSet();
                if (e.AddAudioClip(m, c, at))
                {
                    AudioClip clip = Project.AudioMusicTrack.First(a => !before.Contains(a.Id));
                    added.Add(clip.Id);
                    at = clip.End;
                }
            }

            if (added.Count > 0)
            {
                _session.Select(added, TimelineTrack.AudioMusic);
                ActiveTrack = TimelineTrack.AudioMusic;
                RequestSeek(Project.AudioMusicTrack.First(a => a.Id == added[0]).Start);
            }

            return added.Count > 0;
        }

        var clips = items.Where(i => i.Media.Kind != Media.MediaKind.Audio && !i.Media.Missing).Select(i => e.NewVideoClip(i.Media, i.Clip)).ToList();
        return clips.Count > 0 && e.InsertVideoClips(Layout.InsertIndexAt(t), clips);
    }

    public static bool Accepts(TimelineTrack track, IReadOnlyList<(MediaItem Media, SourceClip? Clip)> items) => track switch
    {
        TimelineTrack.AudioMusic => items.Any(i => i.Media.Kind == Media.MediaKind.Audio),
        TimelineTrack.Video => items.Any(i => i.Media.Kind != Media.MediaKind.Audio),
        _ => false,
    };

    public bool DropTransition(MediaTime t, string transitionId)
    {
        int i = Layout.IndexAt(t);
        return i > 0 && _session.Editor.SetTransition(Project.VideoTrack[i].Id, transitionId);
    }

    public bool DropEffect(MediaTime t, string effectId)
    {
        int i = Layout.IndexAt(t);
        if (i < 0)
        {
            return false;
        }

        Guid id = Project.VideoTrack[i].Id;
        return _session.Editor.AddEffect(IsSelected(id) ? _session.SelectedClips : [id], effectId);
    }

    public bool Nudge(int direction)
    {
        List<(Guid Id, MediaTime Start)> items = TrackItems(_session.SelectionTrack);
        Guid id = items.FirstOrDefault(i => _session.SelectedClips.Contains(i.Id)).Id;
        if (_session.SelectionTrack == TimelineTrack.Transition || id == Guid.Empty)
        {
            return false;
        }

        Rational rate = Project.Settings.FrameRate;
        int frames = TimelineZoom.FramesPerPixel(ZoomStep, rate.ToDouble());
        if (!_session.Editor.Nudge(id, MediaTime.FrameDuration(rate) * (frames * Math.Sign(direction))))
        {
            return false;
        }

        (Guid movedId, MediaTime start) = TrackItems(_session.SelectionTrack).FirstOrDefault(i => i.Id == id);
        if (movedId == id)
        {
            RequestSeek(start + MediaTime.FrameDuration(rate));
        }

        return true;
    }

    public Task<SkiaSharp.SKBitmap?> FilmstripFrameAsync(VideoClip clip, MediaTime sourceTime, int width, int height)
    {
        MediaItem? m = Project.FindMedia(clip.MediaId);
        if (m is null || m.Missing)
        {
            return Task.FromResult<SkiaSharp.SKBitmap?>(null);
        }

        MediaTime step = MediaTime.FromSeconds(Math.Max(0.04, 40 / PixelsPerSecond));
        MediaTime q = new(sourceTime.Ticks / Math.Max(1, step.Ticks) * step.Ticks);
        return Thumbnails.GetAsync(m.Path, clip.IsStill ? MediaTime.Zero : q, width, height);
    }

    public Waveform? WaveformFor(Guid mediaId)
    {
        MediaItem? m = Project.FindMedia(mediaId);
        return m is null || m.Missing ? null : Waveforms.TryGet(m.Path);
    }
}
