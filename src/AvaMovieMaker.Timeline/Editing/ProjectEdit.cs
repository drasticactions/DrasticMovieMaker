using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Undo;

namespace AvaMovieMaker.Timeline.Editing;

public sealed class ProjectEdit : IUndoableCommand
{
    private readonly Project _project;
    private readonly Action<Project> _change;
    private ProjectState? _before;
    private ProjectState? _after;

    public ProjectEdit(Project project, string name, Action<Project> change, string? mergeKey = null)
    {
        _project = project;
        Name = name;
        _change = change;
        MergeKey = mergeKey;
    }

    public string Name { get; }

    public string? MergeKey { get; }

    public void Do()
    {
        if (_after is not null)
        {
            _after.Restore(_project);
        }
        else
        {
            _before = ProjectState.Capture(_project);
            _change(_project);
            _after = ProjectState.Capture(_project);
        }

        _project.NotifyChanged();
    }

    public void Undo()
    {
        _before!.Restore(_project);
        _project.NotifyChanged();
    }

    public bool TryMerge(IUndoableCommand next)
    {
        if (MergeKey is null || next is not ProjectEdit e || e.MergeKey != MergeKey || e._after is null)
        {
            return false;
        }

        _after = e._after;
        return true;
    }
}
