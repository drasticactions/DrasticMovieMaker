using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using AvaMovieMaker.Undo;

namespace AvaMovieMaker.ViewModels.Session;

public sealed class ProjectSession
{
    public ProjectSession()
    {
        Undo = new UndoStack();
        Project = new Project();
        Editor = new TimelineEditor(Project, Undo);
        Editor.Refused += m => Refused?.Invoke(this, m);
        Hook();
        Plan = RenderPlanner.Build(Project);
    }

    public Project Project { get; private set; }

    public UndoStack Undo { get; }

    public TimelineEditor Editor { get; private set; }

    public RenderPlan Plan { get; private set; }

    public IReadOnlyList<Guid> SelectedClips { get; private set; } = [];

    public Timeline.TimelineTrack SelectionTrack { get; private set; } = Timeline.TimelineTrack.Video;

    public Guid? SelectionAnchor { get; private set; }

    public Guid? SelectionActive { get; private set; }

    public TrackSelection.State SelectionState => new(SelectedClips, SelectionAnchor, SelectionActive);

    public SelectionKind ActiveSelection { get; set; }

    public event EventHandler? Changed;

    public event EventHandler? Replaced;

    public event EventHandler? SelectionChanged;

    public event EventHandler<string>? Refused;

    public int NextImportBatch => Project.Media.Count == 0 ? 1 : Project.Media.Max(m => m.ImportBatch) + 1;

    public void Replace(Project project)
    {
        Project.Changed -= OnProjectChanged;
        Project = project;
        Editor = new TimelineEditor(project, Undo);
        Editor.Refused += m => Refused?.Invoke(this, m);
        Undo.Clear();
        Hook();

        SelectedClips = project.VideoTrack.Count > 0 ? [project.VideoTrack[0].Id] : [];
        SelectionAnchor = SelectionActive = SelectedClips.Count > 0 ? SelectedClips[0] : null;
        SelectionTrack = Timeline.TimelineTrack.Video;
        Rebuild();
        Replaced?.Invoke(this, EventArgs.Empty);
    }

    private void Hook() => Project.Changed += OnProjectChanged;

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        var all = Project.VideoTrack.Select(c => c.Id).Concat(Project.AudioMusicTrack.Select(a => a.Id)).Concat(Project.TitleOverlayTrack.Select(t => t.Id)).ToHashSet();
        if (SelectedClips.Any(id => !all.Contains(id)))
        {
            SelectedClips = SelectedClips.Where(all.Contains).ToList();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        Rebuild();
    }

    public void Rebuild()
    {
        Plan = RenderPlanner.Build(Project);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Select(IReadOnlyList<Guid> clips, Timeline.TimelineTrack track = Timeline.TimelineTrack.Video)
    {
        Select(new TrackSelection.State(clips, clips.Count > 0 ? clips[^1] : null, clips.Count > 0 ? clips[^1] : null), track);
    }

    public void Select(TrackSelection.State state, Timeline.TimelineTrack track)
    {
        SelectedClips = state.Selected;
        SelectionAnchor = state.Anchor;
        SelectionActive = state.Active;
        SelectionTrack = track;
        ActiveSelection = SelectionKind.Timeline;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public VideoClip? SelectedVideo => SelectedClips.Select(id => Project.VideoTrack.FirstOrDefault(c => c.Id == id)).FirstOrDefault(c => c is not null);
}
