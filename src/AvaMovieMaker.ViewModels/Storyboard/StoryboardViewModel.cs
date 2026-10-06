using System.Collections.ObjectModel;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaMovieMaker.ViewModels.Storyboard;

public sealed partial class StoryboardViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly ThumbnailService _thumbnails;

    public StoryboardViewModel(ProjectSession session, ThumbnailService thumbnails)
    {
        _session = session;
        _thumbnails = thumbnails;
        session.Changed += (_, _) => Refresh();
        session.SelectionChanged += (_, _) => SyncSelection();
        Refresh();
    }

    public ProjectSession Session => _session;

    public ObservableCollection<StoryboardCellViewModel> Cells { get; } = [];

    public int ThumbnailWidth { get; private set; } = 153;

    public int ThumbnailHeight { get; private set; } = 115;

    public void SetThumbnailSize(int width, int height)
    {
        if (width == ThumbnailWidth && height == ThumbnailHeight)
        {
            return;
        }

        ThumbnailWidth = width;
        ThumbnailHeight = height;
        foreach (StoryboardCellViewModel cell in Cells)
        {
            LoadThumbnail(cell);
        }
    }

    private void LoadThumbnail(StoryboardCellViewModel cell)
    {
        VideoClip c = cell.Clip;
        MediaItem? m = c.Kind == VideoClipKind.Title ? null : _session.Project.FindMedia(c.MediaId);
        _ = cell.LoadThumbnailAsync(_thumbnails, m, ThumbnailWidth, ThumbnailHeight);
    }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    public void Refresh()
    {
        var old = Cells.ToDictionary(c => c.Clip.Id);
        var next = new List<StoryboardCellViewModel>();
        Project p = _session.Project;
        for (int i = 0; i < p.VideoTrack.Count; i++)
        {
            VideoClip c = p.VideoTrack[i];
            if (old.TryGetValue(c.Id, out StoryboardCellViewModel? vm) && vm.Clip == c && vm.Index == i)
            {
                next.Add(vm);
                continue;
            }

            MediaItem? m = c.Kind == VideoClipKind.Title ? null : p.FindMedia(c.MediaId);
            string name = c.Kind == VideoClipKind.Title ? c.Title?.Summary ?? Strings.UntitledTitle
                : m?.Clip(c.SourceClipId)?.Name ?? m?.Name ?? "?";
            var cell = new StoryboardCellViewModel(c, i, name);
            LoadThumbnail(cell);
            next.Add(cell);
        }

        Cells.Clear();
        foreach (StoryboardCellViewModel c in next)
        {
            Cells.Add(c);
        }

        IsEmpty = Cells.Count == 0;
        SyncSelection();
    }

    private void SyncSelection()
    {
        var sel = _session.SelectedClips.ToHashSet();
        foreach (StoryboardCellViewModel c in Cells)
        {
            c.IsSelected = sel.Contains(c.Clip.Id);
        }
    }

    public event EventHandler<Time.MediaTime>? SeekRequested;

    private IReadOnlyList<Guid> Order => [.. Cells.Select(c => c.Clip.Id)];

    private TrackSelection.State State => _session.SelectionTrack == Timeline.TimelineTrack.Video
        ? _session.SelectionState
        : new TrackSelection.State([], null, null);

    public void Select(StoryboardCellViewModel cell, bool toggle, bool range)
    {
        _session.Select(TrackSelection.Click(Order, State, cell.Clip.Id, toggle, range), Timeline.TimelineTrack.Video);
        if (!toggle && !range)
        {
            Seek(cell.Clip.Id);
        }
    }

    public void Collapse(StoryboardCellViewModel cell) =>
        _session.Select(TrackSelection.Collapse(State, cell.Clip.Id), Timeline.TimelineTrack.Video);

    public void SelectCells(IReadOnlyList<int> indexes, bool add)
    {
        var ids = indexes.Where(i => i >= 0 && i < Cells.Count).Order().Select(i => Cells[i].Clip.Id).ToList();
        IReadOnlyList<Guid> selected = add ? [.. State.Selected.Union(ids)] : ids;
        _session.Select(new TrackSelection.State(selected, ids.FirstOrDefault(), ids.Count > 0 ? ids[^1] : null), Timeline.TimelineTrack.Video);
    }

    public void SelectAll() => SelectCells([.. Enumerable.Range(0, Cells.Count)], add: false);

    public bool Key(SelectionKey key, bool shift = false, bool ctrl = false)
    {
        if (Cells.Count == 0 && key != SelectionKey.Clear)
        {
            return false;
        }

        _session.Select(TrackSelection.Key(Order, State, key, shift, ctrl, null, out Guid? moved), Timeline.TimelineTrack.Video);
        if (moved is { } m)
        {
            Seek(m);
        }

        return true;
    }

    private void Seek(Guid id)
    {
        int i = _session.Project.IndexOfVideo(id);
        if (i >= 0)
        {
            SeekRequested?.Invoke(this, TimelineLayout.Compute(_session.Project).Starts[i]);
        }
    }

    public void ClearSelection() => _session.Select(new TrackSelection.State([], null, null), Timeline.TimelineTrack.Video);

    public event EventHandler? AudioAddedToTimeline;

    public bool DropMedia(int index, IReadOnlyList<(MediaItem Media, SourceClip? Clip)> items)
    {
        var video = items.Where(i => i.Media.Kind != Media.MediaKind.Audio && !i.Media.Missing).Select(i => _session.Editor.NewVideoClip(i.Media, i.Clip)).ToList();
        bool ok = video.Count > 0 && _session.Editor.InsertVideoClips(index, video);
        var audio = items.Where(i => i.Media.Kind == Media.MediaKind.Audio && !i.Media.Missing).ToList();
        if (audio.Count > 0)
        {
            TimelineLayout layout = TimelineLayout.Compute(_session.Project);
            Time.MediaTime at = index < layout.Count ? layout.Starts[index] : layout.VideoEnd;
            foreach ((MediaItem m, SourceClip? c) in audio)
            {
                if (_session.Editor.AddAudioClip(m, c, at))
                {
                    ok = true;
                    at = _session.Project.AudioMusicTrack.Max(a => a.End);
                }
            }

            AudioAddedToTimeline?.Invoke(this, EventArgs.Empty);
        }

        return ok;
    }

    public bool CopySelected(int index) => _session.Editor.CopyVideoClips(_session.SelectedClips.ToList(), index);

    public bool TransitionFits(int cellIndex) => cellIndex > 0 && cellIndex < Cells.Count && _session.Editor.TransitionFits(Cells[cellIndex].Clip.Id);

    public bool MoveSelected(int index) => _session.Editor.MoveVideoClips(_session.SelectedClips.ToList(), index);

    public bool DropTransition(int cellIndex, string transitionId)
    {
        if (cellIndex <= 0 || cellIndex >= Cells.Count)
        {
            return false;
        }

        return _session.Editor.SetTransition(Cells[cellIndex].Clip.Id, transitionId);
    }

    public bool DropEffect(int cellIndex, string effectId)
    {
        if (cellIndex < 0 || cellIndex >= Cells.Count || EffectCatalog.Find(effectId) is null)
        {
            return false;
        }

        StoryboardCellViewModel target = Cells[cellIndex];
        IReadOnlyList<Guid> ids = target.IsSelected ? _session.SelectedClips : [target.Clip.Id];
        return _session.Editor.AddEffect(ids, effectId);
    }
}
