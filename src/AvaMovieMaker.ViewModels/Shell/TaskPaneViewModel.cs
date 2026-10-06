using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaMovieMaker.ViewModels.Shell;

public sealed partial class TaskPaneViewModel : ObservableObject
{
    public TaskPaneViewModel(ShellViewModel shell)
    {
        Sections =
        [
            new TaskSection(Strings.TaskImport,
            [
                TaskEntry.Unavailable(Strings.TaskFromCamera),
                new(Strings.TaskVideos, shell.ImportVideosCommand, Strings.TaskImportVideosName),
                new(Strings.TaskPictures, shell.ImportPicturesCommand, Strings.TaskImportPicturesName),
                new(Strings.TaskAudioOrMusic, shell.ImportAudioCommand, Strings.TaskImportAudioName),
            ]),
            new TaskSection(Strings.TaskEdit,
            [
                new(Strings.TaskImportedMedia, shell.ShowImportedMediaCommand, Strings.TaskShowImportedMediaName),
                new(Strings.TaskEffects, shell.ShowEffectsCommand, Strings.TaskShowEffectsName),
                new(Strings.TaskTransitions, shell.ShowTransitionsCommand, Strings.TaskShowTransitionsName),
                new(Strings.TaskTitlesAndCredits, shell.TitlesCommand, Strings.TaskMakeTitlesName),
            ]),
            new TaskSection(Strings.TaskPublishTo,
            [
                new(Strings.TaskThisComputer, shell.PublishToComputerCommand, Strings.TaskPublishToComputerName),
                TaskEntry.Unavailable(Strings.TaskDvd),
                TaskEntry.Unavailable(Strings.TaskRecordableCd),
                TaskEntry.Unavailable(Strings.TaskEmail),
                TaskEntry.Unavailable(Strings.TaskCamera),
            ]),
        ];
    }

    public IReadOnlyList<TaskSection> Sections { get; }
}
