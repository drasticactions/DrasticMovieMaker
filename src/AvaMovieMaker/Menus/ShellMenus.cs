using System.Windows.Input;
using Avalonia.Data;
using AvaMovieMaker.ViewModels;
using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Menus;

public static class ShellMenus
{
    public static IReadOnlyList<MenuItemEntry> Build(ShellViewModel s) =>
    [
        File(s), Edit(s), View(s), Tools(s), Clip(s), Play(s), Help(s),
    ];

    private static MenuItemEntry File(ShellViewModel s) => Menu(Strings.MenuFile, "FileMenu",
        Item(Strings.NewProject, Strings.PromptNewProject, s.NewProjectCommand, "Ctrl+N"),
        Item(Strings.OpenProject, Strings.PromptOpenProject, s.OpenProjectCommand, "Ctrl+O"),
        Item(Strings.SaveProject, Strings.PromptSaveProject, s.SaveCommand, "Ctrl+S"),
        Item(Strings.SaveProjectAs, Strings.PromptSaveProjectAs, s.SaveAsCommand, "F12"),
        Item(Strings.ExportProjectPackage, null, s.ExportPackageCommand),
        Item(Strings.PublishMovie, Strings.PromptPublishMovie, s.PublishCommand, "Ctrl+P", icon: "publish"),
        Separator(),
        Item(Strings.ImportFromCamera, Strings.PromptImportFromCamera, s.Unavailable, "Ctrl+R"),
        Item(Strings.ImportMediaItems, Strings.PromptImportMediaItems, s.ImportMediaCommand, "Ctrl+I", icon: "import"),
        Item(Strings.NewCollectionFolder, Strings.PromptNewCollectionFolder, s.Unavailable, icon: "collections"),
        Separator(),
        Item(Strings.ProjectProperties, Strings.PromptProjectProperties, s.ProjectPropertiesCommand),
        Separator(),
        new MenuDynamic(() => Recent(s), s.RecentProjects),
        Separator(),
        Item(Strings.Exit, Strings.PromptExit, s.ExitCommand) with { Role = MenuRole.Exit });

    private static MenuItemEntry Edit(ShellViewModel s) => Menu(Strings.MenuEdit, null,
        Bound(Of(s, nameof(s.UndoText), x => x.UndoText), Strings.PromptUndo, s.UndoCommand, "Ctrl+Z", icon: "undo"),
        Bound(Of(s, nameof(s.RedoText), x => x.RedoText), Strings.PromptRedo, s.RedoCommand, "Ctrl+Y", icon: "redo"),
        Separator(),
        Item(Strings.Cut, Strings.PromptCut, s.CutCommand, "Ctrl+X"),
        Item(Strings.Copy, Strings.PromptCopy, s.CopyCommand, "Ctrl+C"),
        Item(Strings.Paste, Strings.PromptPaste, s.PasteCommand, "Ctrl+V"),
        Item(Strings.Remove, Strings.PromptRemove, s.RemoveCommand, "Delete"),
        Separator(),
        Item(Strings.EditTitle, Strings.PromptEditTitle, s.EditTitleCommand),
        Separator(),
        Item(Strings.SelectAll, Strings.PromptSelectAll, s.SelectAllCommand, "Ctrl+A"),
        Item(Strings.Rename, Strings.PromptRename, s.RenameCommand, "F2"),
        Separator(),
        Bound(Of(s, nameof(s.ClearText), x => x.ClearText), Strings.PromptClearTimeline, s.ClearTimelineCommand, "Ctrl+Delete"),
        Separator(),
        Item(Strings.BrowseForMissingFile, Strings.PromptBrowseForMissingFile, s.BrowseMissingCommand));

    private static MenuItemEntry View(ShellViewModel s) => Menu(Strings.MenuView, null,
        Radio(Item(Strings.Storyboard, Strings.PromptStoryboard, s.ShowStoryboardViewCommand, "Ctrl+T"), "board", MenuBindings.Not(s, nameof(s.IsTimeline), x => x.IsTimeline)),
        Radio(Item(Strings.Timeline, Strings.PromptTimeline, s.ShowTimelineViewCommand, "Ctrl+T"), "board", Of(s, nameof(s.IsTimeline), x => x.IsTimeline)),
        Separator(),
        Item(Strings.ZoomIn, Strings.PromptZoomIn, s.ZoomInCommand, "PageDown", icon: "zoom-in"),
        Item(Strings.ZoomOut, Strings.PromptZoomOut, s.ZoomOutCommand, "PageUp", icon: "zoom-out"),
        Item(Strings.ZoomToFit, Strings.PromptZoomToFit, s.ZoomToFitCommand, "F9"),
        Separator(),
        Menu(Strings.PreviewMonitorSize, null,
            Radio(Item(Strings.MonitorSmall, Strings.PromptMonitorSmall, s.SetMonitorSizeCommand, parameter: "small"), "monitor", MenuBindings.Not(s, nameof(s.LargeMonitor), x => x.LargeMonitor)),
            Radio(Item(Strings.MonitorLarge, Strings.PromptMonitorLarge, s.SetMonitorSizeCommand, parameter: "large"), "monitor", Of(s, nameof(s.LargeMonitor), x => x.LargeMonitor))),
        Item(Strings.FullScreen, Strings.PromptFullScreen, s.Monitor.FullScreenCommand, "Alt+Enter"),
        Separator(),
        Check(Item(Strings.StatusBar, Strings.PromptStatusBar, s.ToggleStatusBarCommand), Of(s, nameof(s.ShowStatusBar), x => x.ShowStatusBar)),
        Check(Item(Strings.Tasks, Strings.PromptTasks, s.ToggleTasksCommand, icon: "tasks"), Of(s, nameof(s.ShowTasks), x => x.ShowTasks)),
        Check(Item(Strings.Collections, Strings.PromptCollections, s.ToggleCollectionsCommand, icon: "collections"), Of(s, nameof(s.ShowCollections), x => x.ShowCollections)),
        Separator(),
        Radio(Item(Strings.Thumbnails, Strings.PromptThumbnails, s.SetContentsDetailsCommand, parameter: "thumbnails"), "contents", MenuBindings.Not(s.Contents, nameof(s.Contents.IsDetails), x => x.IsDetails)),
        Radio(Item(Strings.Details, Strings.PromptDetails, s.SetContentsDetailsCommand, parameter: "details"), "contents", Of(s.Contents, nameof(s.Contents.IsDetails), x => x.IsDetails)),
        Separator(),
        Menu(Strings.ArrangeIconsBy, null,
            Item(Strings.SortNameAndDate, Strings.PromptSortNameAndDate, s.SortByCommand, parameter: ContentsSort.NameAndDate),
            Item(Strings.SortClipName, Strings.PromptSortClipName, s.SortByCommand, parameter: ContentsSort.ClipName),
            Item(Strings.SortDuration, Strings.PromptSortDuration, s.SortByCommand, parameter: ContentsSort.Duration),
            Item(Strings.SortStartTime, Strings.PromptSortStartTime, s.SortByCommand, parameter: ContentsSort.StartTime),
            Item(Strings.SortEndTime, Strings.PromptSortEndTime, s.SortByCommand, parameter: ContentsSort.EndTime),
            Item(Strings.SortDimensions, Strings.PromptSortDimensions, s.SortByCommand, parameter: ContentsSort.Dimensions),
            Item(Strings.SortDateTaken, Strings.PromptSortDateTaken, s.SortByCommand, parameter: ContentsSort.DateTaken),
            Item(Strings.SortFileName, Strings.PromptSortFileName, s.SortByCommand, parameter: ContentsSort.FileName)),
        Separator(),
        Menu(Strings.Theme, null,
            Item(Strings.ThemeSystem, null, s.SetThemeCommand, parameter: "System"),
            Item(Strings.ThemeLight, null, s.SetThemeCommand, parameter: "Light"),
            Item(Strings.ThemeDark, null, s.SetThemeCommand, parameter: "Dark")));

    private static MenuItemEntry Tools(ShellViewModel s) => Menu(Strings.MenuTools, null,
        Item(Strings.AutoMovie, Strings.PromptAutoMovie, s.AutoMovieShowCommand, icon: "automovie"),
        Item(Strings.TitlesAndCredits, Strings.PromptTitlesAndCredits, s.TitlesShowCommand),
        Separator(),
        Item(Strings.Effects, Strings.PromptEffects, s.ShowEffectsCommand, icon: "star-on"),
        Item(Strings.Transitions, Strings.PromptTransitions, s.ShowTransitionsCommand, icon: "transitions"),
        Separator(),
        Item(Strings.CreateClips, Strings.PromptCreateClips, s.CreateClipsCommand),
        Item(Strings.TakePicture, Strings.PromptTakePicture, s.TakePictureCommand),
        Item(Strings.NarrateTimeline, Strings.PromptNarrateTimeline, s.NarrateCommand, icon: "narrate"),
        Item(Strings.AudioLevels, Strings.PromptAudioLevels, s.ShowAudioLevelsCommand, icon: "audio-levels"),
        Separator(),
        Item(Strings.Options, Strings.PromptOptions, s.OptionsCommand) with { Role = MenuRole.Options });

    private static MenuItemEntry Clip(ShellViewModel s) => Menu(Strings.MenuClip, null,
        Bound(Of(s, nameof(s.AddToText), x => x.AddToText), Strings.PromptAddToTimeline, s.AddToTimelineCommand, "Ctrl+D"),
        Menu(Strings.Audio, null,
            Check(Item(Strings.Mute, Strings.PromptMute, s.AudioMuteCommand), Of(s, nameof(s.IsAudioMuted), x => x.IsAudioMuted)),
            Check(Item(Strings.FadeIn, Strings.PromptAudioFadeIn, s.AudioFadeInCommand), Of(s, nameof(s.IsAudioFadeIn), x => x.IsAudioFadeIn)),
            Check(Item(Strings.FadeOut, Strings.PromptAudioFadeOut, s.AudioFadeOutCommand), Of(s, nameof(s.IsAudioFadeOut), x => x.IsAudioFadeOut)),
            Item(Strings.Volume, Strings.PromptVolume, s.AudioVolumeCommand, "Ctrl+U")),
        Menu(Strings.Video, null,
            Item(Strings.VideoEffects, Strings.PromptVideoEffects, s.VideoEffectsCommand),
            Item(Strings.FadeIn, Strings.PromptVideoFadeIn, s.VideoFadeInCommand),
            Item(Strings.FadeOut, Strings.PromptVideoFadeOut, s.VideoFadeOutCommand)),
        Separator(),
        Item(Strings.TrimBeginning, Strings.PromptTrimBeginning, s.TrimBeginningCommand, "I"),
        Item(Strings.TrimEnd, Strings.PromptTrimEnd, s.TrimEndCommand, "O"),
        Item(Strings.ClearTrimPoints, Strings.PromptClearTrimPoints, s.ClearTrimPointsCommand, "U"),
        Separator(),
        Item(Strings.Split, Strings.PromptSplit, s.SplitCommand, "M", icon: "split"),
        Item(Strings.Combine, Strings.PromptCombine, s.CombineCommand, "N"),
        Item(Strings.NudgeLeft, Strings.PromptNudgeLeft, s.NudgeLeftCommand, "Ctrl+Shift+B"),
        Item(Strings.NudgeRight, Strings.PromptNudgeRight, s.NudgeRightCommand, "Ctrl+Shift+N"),
        Separator(),
        Item(Strings.Properties, Strings.PromptProperties, s.ClipPropertiesCommand));

    private static MenuItemEntry Play(ShellViewModel s) => Menu(Strings.MenuPlay, null,
        Bound(Of(s.Monitor, nameof(s.Monitor.PlayClipText), x => x.PlayClipText), Strings.PromptPlayPauseClip, s.PlayPauseClipCommand, "K", icon: "menu-play"),
        Item(Strings.Stop, Strings.PromptStop, s.Monitor.StopCommand, "Ctrl+K", icon: "menu-stop"),
        Bound(Of(s, nameof(s.PlayBoardText), x => x.PlayBoardText), Strings.PromptPlayTimeline, s.Monitor.PlayProjectCommand, "Ctrl+W", icon: "menu-play"),
        Bound(Of(s, nameof(s.RewindBoardText), x => x.RewindBoardText), Strings.PromptRewindTimeline, s.Monitor.RewindCommand, "Ctrl+Q", icon: "menu-rewind"),
        Separator(),
        Item(Strings.Back, Strings.PromptBack, s.Monitor.BackCommand, "Ctrl+Alt+Left"),
        Item(Strings.Forward, Strings.PromptForward, s.Monitor.ForwardCommand, "Ctrl+Alt+Right"),
        Item(Strings.PreviousFrame, Strings.PromptPreviousFrame, s.Monitor.PreviousFrameCommand, "J", icon: "menu-prev"),
        Item(Strings.NextFrame, Strings.PromptNextFrame, s.Monitor.NextFrameCommand, "L", icon: "menu-next"));

    private static MenuItemEntry Help(ShellViewModel s) => Menu(Strings.MenuHelp, null,
        new MenuItemEntry(Strings.HelpTopics) { Prompt = Strings.PromptHelpTopics, Shortcut = Shortcut.Parse("F1"), IsEnabled = false },
        Separator(),
        Item(Strings.About, Strings.PromptAbout, s.AboutCommand) with { Role = MenuRole.About });

    private static IReadOnlyList<MenuEntry> Recent(ShellViewModel s) =>
        s.RecentProjects.Count == 0
            ? [new MenuItemEntry(Strings.RecentEmpty) { Name = "RecentEmpty", IsEnabled = false }]
            : [.. s.RecentProjects.Select((path, i) => new MenuItemEntry(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RecentProject,
                i + 1, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? string.Empty, System.IO.Path.GetFileNameWithoutExtension(path))))
            {
                Command = s.OpenRecentCommand,
                CommandParameter = path,
                ToolTip = path,
            })];

    private static MenuItemEntry Menu(string header, string? name, params MenuEntry[] items) =>
        new(header) { Name = name, Items = items };

    private static MenuSeparator Separator(string? name = null) => new(name);

    private static MenuItemEntry Item(string header, string? prompt, ICommand command, string? shortcut = null, string? icon = null, object? parameter = null) =>
        new(header)
        {
            Prompt = prompt,
            Command = command,
            CommandParameter = parameter,
            Shortcut = shortcut is null ? null : Shortcut.Parse(shortcut),
            Icon = icon,
        };

    private static MenuItemEntry Bound(BindingBase header, string? prompt, ICommand command, string? shortcut = null, string? icon = null) =>
        Item(string.Empty, prompt, command, shortcut, icon) with { HeaderBinding = header };

    private static MenuItemEntry Check(MenuItemEntry item, BindingBase isChecked) =>
        item with { Toggle = MenuToggle.CheckBox, IsChecked = isChecked };

    private static MenuItemEntry Radio(MenuItemEntry item, string group, BindingBase isChecked) =>
        item with { Toggle = MenuToggle.Radio, Group = group, IsChecked = isChecked };

    private static BindingBase Of<TSource, TValue>(TSource source, string property, Func<TSource, TValue> get)
        where TSource : class =>
        MenuBindings.Of(source, property, get);
}
