using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaMovieMaker.ViewModels.Shell;

public sealed partial class TaskSection(string title, IReadOnlyList<TaskEntry> entries) : ObservableObject
{
    public string Title { get; } = title;

    public IReadOnlyList<TaskEntry> Entries { get; } = entries;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;
}
