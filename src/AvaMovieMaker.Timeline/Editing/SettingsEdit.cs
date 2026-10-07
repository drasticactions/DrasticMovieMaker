using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Undo;

namespace AvaMovieMaker.Timeline.Editing;

public sealed class SettingsEdit(Project project, string name, ProjectSettings after) : IUndoableCommand
{
    private readonly ProjectSettings _before = project.Settings;

    public string Name { get; } = name;

    public void Do()
    {
        project.Settings = after;
        project.NotifyChanged();
    }

    public void Undo()
    {
        project.Settings = _before;
        project.NotifyChanged();
    }
}
