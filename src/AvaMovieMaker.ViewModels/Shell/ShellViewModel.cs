using System.Collections.ObjectModel;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.IO;
using AvaMovieMaker.Interop.Mswmm;
using AvaMovieMaker.Media;
using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Settings;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Import;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using AvaMovieMaker.Timeline.Serialization;
using AvaMovieMaker.ViewModels.AutoMovie;
using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Narration;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Publish;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using AvaMovieMaker.ViewModels.Storyboard;
using AvaMovieMaker.ViewModels.Timeline;
using AvaMovieMaker.ViewModels.Titles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Shell;

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IFileDialogs _files;
    private readonly IMessageBoxes _messages;
    private readonly IDialogService _dialogs;
    private readonly IDispatcher _dispatcher;
    private readonly ISettingsStore _store;
    private readonly ProjectClipboard _clipboard;
    private Timer? _recoveryTimer;

    public ShellViewModel(EngineHost engine, IFileDialogs files, IMessageBoxes messages, IDialogService dialogs, IDispatcher dispatcher, ISettingsStore store, ProjectClipboard clipboard)
    {
        Engine = engine;
        _files = files;
        _messages = messages;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _store = store;
        _clipboard = clipboard;
        Session = new ProjectSession();
        Waveforms = new WaveformService();
        Monitor = new MonitorViewModel(Session, engine, dispatcher);
        Monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MonitorViewModel.HasVideo))
            {
                TakePictureCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(MonitorViewModel.IsProjectPlaying))
            {
                OnPropertyChanged(nameof(PlayBoardText));
                OnPropertyChanged(nameof(PlayBoardTip));
                OnPropertyChanged(nameof(PlayBoardGlyph));
            }
        };
        Contents = new ContentsViewModel(Session, engine.Thumbnails);
        Contents.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContentsViewModel.View))
            {
                OnPropertyChanged(nameof(StatusText));
                ShowImportedMediaCommand.NotifyCanExecuteChanged();
                ShowEffectsCommand.NotifyCanExecuteChanged();
                ShowTransitionsCommand.NotifyCanExecuteChanged();
            }
        };
        Storyboard = new StoryboardViewModel(Session, engine.Thumbnails);
        Timeline = new TimelineViewModel(Session, Waveforms, engine.Thumbnails);
        TitleEditor = new TitleEditorViewModel(Session, Monitor, dialogs);
        AutoMovie = new AutoMovieViewModel(Session, Contents, messages, Settings);
        AudioLevels = new AudioLevelsViewModel(Session);
        Tasks = new TaskPaneViewModel(this);

        IsTimeline = Settings.ShowTimeline;
        ShowTasks = Settings.ShowTasksPane;
        ShowCollections = Settings.ShowCollectionsPane && !ShowTasks;
        ShowStatusBar = Settings.ShowStatusBar;
        Contents.IsDetails = Settings.ContentsDetails;

        Session.Changed += (_, _) => OnProjectChanged();
        Session.Undo.Changed += (_, _) => OnUndoChanged();
        Session.Refused += (_, message) => _ = Inform(message, null, MessageIcon.Warning);
        Session.SelectionChanged += (_, _) =>
        {
            RefreshCommands();
            OnBoardSelectionChanged();
        };
        Contents.SelectedItems.CollectionChanged += (_, _) => RefreshCommands();
        Contents.ItemSelected += (_, item) => LoadContentsItem(item, play: false);
        Contents.PreviewRequested += (_, item) => LoadContentsItem(item, play: true);
        Contents.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContentsViewModel.SelectedCatalogItem) && Contents.SelectedCatalogItem is { } cat)
            {
                PreviewCatalogItem(cat, play: false);
            }
        };
        Contents.CatalogPreviewRequested += (_, item) => PreviewCatalogItem(item, play: true);
        Contents.Activated += (_, _) =>
        {
            if (Contents.IsMediaView && Contents.SelectedItems.Count > 0)
            {
                LoadContentsItem(Contents.SelectedItems[^1], play: false);
            }
            else if (!Contents.IsMediaView && Contents.SelectedCatalogItem is { } cat)
            {
                PreviewCatalogItem(cat, play: false);
            }
        };
        Monitor.PositionChanged += (_, t) => Timeline.Playhead = t;
        Monitor.SourceChanged += (_, _) => _catalogLoaded = null;
        Monitor.ProjectCaption = ProjectCaption;
        Monitor.ProjectStep = step => IsTimeline
            ? Timeline.Key(step < 0 ? SelectionKey.Previous : SelectionKey.Next)
            : Storyboard.Key(step < 0 ? SelectionKey.Previous : SelectionKey.Next);
        Monitor.FullScreenRequested += (_, _) => _dialogs.ShowFullScreen(Monitor);
        Timeline.SeekRequested += (_, t) =>
        {
            Monitor.ShowProject();
            Monitor.Seek(t);
        };
        Storyboard.AudioAddedToTimeline += async (_, _) =>
        {
            IsTimeline = true;
            await Inform(Strings.AudioNeedsTimeline, "switch-to-timeline");
        };
        Storyboard.SeekRequested += (_, t) =>
        {
            Monitor.ShowProject();
            Monitor.Seek(t);
        };
        TitleEditor.Closed += (_, _) => Pane = UpperPane.Contents;
        TitleEditor.TimelineNeeded += async (_, _) =>
        {
            if (!IsTimeline)
            {
                IsTimeline = true;
                await Inform(Strings.OverlayNeedsTimeline, "overlay-needs-timeline");
            }
        };
        AutoMovie.Closed += (_, _) => Pane = UpperPane.Contents;
        AutoMovie.Created += (_, _) => Monitor.ShowProject(seekStart: true);
        AutoMovie.BrowseMusic = BrowseAutoMovieMusicAsync;
        TitleFonts.MissingFamily += family => _dispatcher.Post(() => _ = Inform(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.FontMissing, family), "font-missing"));
        Settings.TrimRecent();
        RecentProjects = [.. Settings.RecentProjects];

        Project.Settings = OptionsViewModel.ForProject(Settings);
        Session.Rebuild();
        Session.Changed += (_, _) => _changedSinceRecovery = true;
        UpdateTitle();
        StartRecoveryTimer();
    }

    public EngineHost Engine { get; }

    public ProjectSession Session { get; }

    public AppSettings Settings => _store.Settings;

    public WaveformService Waveforms { get; }

    public MonitorViewModel Monitor { get; }

    public ContentsViewModel Contents { get; }

    public StoryboardViewModel Storyboard { get; }

    public TimelineViewModel Timeline { get; }

    public TitleEditorViewModel TitleEditor { get; }

    public AutoMovieViewModel AutoMovie { get; }

    public AudioLevelsViewModel AudioLevels { get; }

    public TaskPaneViewModel Tasks { get; }

    [ObservableProperty]
    public partial NarrationViewModel? Narration { get; private set; }

    public ObservableCollection<string> RecentProjects { get; }

    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTimeline { get; set; }

    [ObservableProperty]
    public partial bool ShowTasks { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowCollections { get; set; }

    [ObservableProperty]
    public partial bool ShowStatusBar { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? MenuPrompt { get; set; }

    public string StatusText => MenuPrompt ?? (IsBusy && !string.IsNullOrEmpty(BusyText) ? BusyText : Strings.Ready);

    [RelayCommand]
    private void ToggleCollections() => ShowCollections = !ShowCollections;

    [RelayCommand]
    private void ToggleStatusBar() => ShowStatusBar = !ShowStatusBar;

    [ObservableProperty]
    public partial bool LargeMonitor { get; set; } = true;

    [ObservableProperty]
    public partial UpperPane Pane { get; set; }

    [ObservableProperty]
    public partial string UndoText { get; private set; } = Strings.Undo;

    [ObservableProperty]
    public partial string RedoText { get; private set; } = Strings.Redo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string BusyText { get; private set; } = string.Empty;

    public Project Project => Session.Project;

    public event EventHandler? CloseRequested;

    partial void OnIsTimelineChanged(bool value)
    {
        Settings.ShowTimeline = value;
        Timeline.Invalidate();
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(AddToText));
        OnPropertyChanged(nameof(PlayBoardText));
        OnPropertyChanged(nameof(RewindBoardText));
        OnPropertyChanged(nameof(PlayBoardTip));
        OnPropertyChanged(nameof(RewindBoardTip));
        Monitor.RefreshCaption();
        NarrateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnPaneChanged(UpperPane value)
    {
        ShowImportedMediaCommand.NotifyCanExecuteChanged();
        ShowEffectsCommand.NotifyCanExecuteChanged();
        ShowTransitionsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsContentsPane));
        OnPropertyChanged(nameof(IsTitlesPane));
        OnPropertyChanged(nameof(IsAutoMoviePane));
        OnPropertyChanged(nameof(IsNarrationPane));
    }

    public string ClearText => IsTimeline ? Strings.ClearTimeline : Strings.ClearStoryboard;

    public string AddToText => IsTimeline ? Strings.AddToTimeline : Strings.AddToStoryboard;

    public string PlayBoardText => Monitor.IsProjectPlaying
        ? IsTimeline ? Strings.PauseTimeline : Strings.PauseStoryboard
        : IsTimeline ? Strings.PlayTimeline : Strings.PlayStoryboard;

    public string RewindBoardText => IsTimeline ? Strings.RewindTimeline : Strings.RewindStoryboard;

    public string PlayBoardTip => Monitor.IsProjectPlaying
        ? IsTimeline ? Strings.PauseTimelineTip : Strings.PauseStoryboardTip
        : IsTimeline ? Strings.PlayTimelineTip : Strings.PlayStoryboardTip;

    public string RewindBoardTip => IsTimeline ? Strings.RewindTimelineTip : Strings.RewindStoryboardTip;

    public string PlayBoardGlyph => Monitor.IsProjectPlaying ? "pause" : "play";

    private string ProjectCaption()
    {
        string name = SelectedItemName();
        return name.Length == 0 ? string.Empty : string.Format(System.Globalization.CultureInfo.CurrentCulture, IsTimeline ? Strings.TimelineCaption : Strings.StoryboardCaption, name);
    }

    private string SelectedItemName()
    {
        IReadOnlyList<Guid> sel = Session.SelectedClips;
        if (sel.Count == 0)
        {
            return string.Empty;
        }

        Guid id = Session.SelectionActive is { } a && sel.Contains(a) ? a : sel[0];
        Project p = Project;
        string MediaName(Guid mediaId, Guid clipId) => p.FindMedia(mediaId) is { } m ? m.Clip(clipId)?.Name ?? m.Name : string.Empty;
        switch (Session.SelectionTrack)
        {
            case ViewModels.Timeline.TimelineTrack.AudioMusic:
                return p.AudioMusicTrack.FirstOrDefault(x => x.Id == id) is { } audio ? MediaName(audio.MediaId, audio.SourceClipId) : string.Empty;
            case ViewModels.Timeline.TimelineTrack.TitleOverlay:
                return p.TitleOverlayTrack.FirstOrDefault(x => x.Id == id) is { } title ? title.Content.Summary : string.Empty;
        }

        if (p.VideoTrack.FirstOrDefault(x => x.Id == id) is not { } c)
        {
            return string.Empty;
        }

        if (Session.SelectionTrack == ViewModels.Timeline.TimelineTrack.Transition)
        {
            return c.TransitionIn is { } t ? AvaMovieMaker.Effects.Catalog.TransitionCatalog.Find(t.TransitionId)?.Name ?? string.Empty : string.Empty;
        }

        return c.Kind == VideoClipKind.Title ? c.Title?.Summary ?? Strings.UntitledTitle : MediaName(c.MediaId, c.SourceClipId);
    }

    private void OnBoardSelectionChanged()
    {
        if (Session.ActiveSelection == SelectionKind.Timeline && Monitor.Target != PreviewTarget.Project)
        {
            Monitor.ShowProject();
        }
        else
        {
            Monitor.RefreshCaption();
        }
    }

    private void LoadContentsItem(ContentItemViewModel item, bool play)
    {
        if (Monitor.Target == PreviewTarget.Item && Monitor.Item is { } loaded && loaded.Media.Id == item.Media.Id && loaded.Clip?.Id == item.Clip?.Id)
        {
            if (play && !Monitor.IsPlaying)
            {
                Monitor.Playback.Play();
            }

            return;
        }

        Monitor.ShowItem(item.Media, item.Clip, play);
    }

    public bool IsContentsPane => Pane == UpperPane.Contents;

    public bool IsTitlesPane => Pane == UpperPane.Titles;

    public bool IsAutoMoviePane => Pane == UpperPane.AutoMovie;

    public bool IsNarrationPane => Pane == UpperPane.Narration;

    public bool CanAutoMovie => Project.Media.Any(m => m.Kind != MediaKind.Audio);

    public bool CanPublish => !Project.IsEmpty;

    partial void OnShowTasksChanged(bool value)
    {
        Settings.ShowTasksPane = value;
        if (value)
        {
            ShowCollections = false;
        }
    }

    partial void OnShowCollectionsChanged(bool value)
    {
        Settings.ShowCollectionsPane = value;
        if (value)
        {
            ShowTasks = false;
        }
    }

    partial void OnShowStatusBarChanged(bool value) => Settings.ShowStatusBar = value;

    private TimeSpan _lastLength;

    private async void CheckProjectLength()
    {
        TimeSpan length = TimeSpan.FromSeconds(Project.Duration.Seconds);
        TimeSpan before = _lastLength;
        _lastLength = length;
        if (length > TimeSpan.FromHours(24) && before <= TimeSpan.FromHours(24) && Session.Undo.CanUndo)
        {
            await Inform(Strings.ProjectOver24Hours, null, MessageIcon.Warning);
            Session.Undo.Undo();
        }
        else if (length > TimeSpan.FromHours(6) && before <= TimeSpan.FromHours(6))
        {
            await Inform(Strings.ProjectOver6Hours, "over-6-hours", MessageIcon.Warning);
        }
    }

    private void OnProjectChanged()
    {
        CheckProjectLength();
        OnPropertyChanged(nameof(CanAutoMovie));
        OnPropertyChanged(nameof(CanPublish));
        UpdateTitle();
        AutoMovie.Refresh();
        Narration?.Refresh();
        RefreshCommands();
    }

    private void OnUndoChanged()
    {
        UndoText = Session.Undo.UndoName is { } u ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.UndoName, u) : Strings.Undo;
        RedoText = Session.Undo.RedoName is { } r ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RedoName, r) : Strings.Redo;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(UndoHistory));
        OnPropertyChanged(nameof(RedoHistory));
        OnPropertyChanged(nameof(HasUndo));
        OnPropertyChanged(nameof(HasRedo));
        UpdateTitle();
    }

    public bool HasUndo => Session.Undo.CanUndo;

    public bool HasRedo => Session.Undo.CanRedo;

    public IReadOnlyList<HistoryEntry> UndoHistory =>
        Session.Undo.UndoNames.Take(HistoryEntry.MaxRows).Select((n, i) => new HistoryEntry(n, i + 1, UndoManyCommand)).ToList();

    public IReadOnlyList<HistoryEntry> RedoHistory =>
        Session.Undo.RedoNames.Take(HistoryEntry.MaxRows).Select((n, i) => new HistoryEntry(n, i + 1, RedoManyCommand, IsRedo: true)).ToList();

    [RelayCommand]
    private void UndoMany(int count)
    {
        for (int i = 0; i < count && Session.Undo.CanUndo; i++)
        {
            Session.Undo.Undo();
        }
    }

    [RelayCommand]
    private void RedoMany(int count)
    {
        for (int i = 0; i < count && Session.Undo.CanRedo; i++)
        {
            Session.Undo.Redo();
        }
    }

    private void UpdateTitle() => WindowTitle = Strings.AppName;

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(Aspect));
        OnPropertyChanged(nameof(SelectedFit));
        OnPropertyChanged(nameof(CanFit));
        OnPropertyChanged(nameof(IsAudioMuted));
        OnPropertyChanged(nameof(IsAudioFadeIn));
        OnPropertyChanged(nameof(IsAudioFadeOut));
        foreach (IRelayCommand c in new IRelayCommand[]
        {
            AddToTimelineCommand, RemoveCommand, SplitCommand, CombineCommand, TrimBeginningCommand, TrimEndCommand,
            ClearTrimPointsCommand, NudgeLeftCommand, NudgeRightCommand, CutCommand, CopyCommand, EditTitleCommand,
            ClipPropertiesCommand, VideoEffectsCommand, RemoveEffectsCommand, VideoFadeInCommand, VideoFadeOutCommand, SetFitCommand, AudioMuteCommand,
            AudioFadeInCommand, AudioFadeOutCommand, AudioVolumeCommand, RenameCommand, ClearTimelineCommand,
            PasteCommand, BrowseMissingCommand, CreateClipsCommand, PublishCommand, PublishToComputerCommand, TakePictureCommand,
        })
        {
            c.NotifyCanExecuteChanged();
        }
    }

    private async Task<MessageResult> Inform(string text, string? key = null, MessageIcon icon = MessageIcon.Information)
    {
        if (key is not null && Settings.DismissedWarnings.Contains(key))
        {
            return MessageResult.Ok;
        }

        return await _messages.ShowAsync(new MessageRequest(text, MessageButtons.Ok, icon) { DontShowAgainKey = key });
    }

    public async Task StartAsync(IReadOnlyList<string> paths)
    {
        string? project = paths.FirstOrDefault(IsProjectFile);
        await StartAsync(project);
        var media = paths.Where(p => p != project && FileStore.Current.Exists(p)).ToList();
        if (media.Count > 0)
        {
            await ImportFilesAsync(media);
        }
    }

    public async Task StartAsync(string? path)
    {
        if (await OfferRecoveryAsync())
        {
            Monitor.ShowProject(seekStart: true);
            return;
        }

        if (path is null && Settings.OpenLastProjectOnStartup && Settings.RecentProjects.FirstOrDefault() is { } last && FileStore.Current.Exists(last))
        {
            path = last;
        }

        if (path is not null)
        {
            await OpenAnyAsync(path);
        }

        Monitor.ShowProject(seekStart: true);
    }

    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!Session.Undo.IsDirty)
        {
            return true;
        }

        MessageResult r = await _messages.ShowAsync(new MessageRequest(
            string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.SaveChangesPrompt, Project.DisplayName),
            MessageButtons.YesNoCancel, MessageIcon.Warning));
        return r switch
        {
            MessageResult.Yes => await SaveAsync(),
            MessageResult.No => true,
            _ => false,
        };
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        Engine.Playback.Stop();
        DeleteRecovery();
        Session.Replace(new Project
        {
            Settings = OptionsViewModel.ForProject(Settings),
            Properties = new ProjectProperties { Author = Settings.DefaultAuthor },
        });
        Pane = UpperPane.Contents;
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        string? path = await _files.OpenProjectAsync(Folder(Settings.RecentProjects.FirstOrDefault()));
        if (path is not null)
        {
            await OpenAnyAsync(path);
        }
    }

    private Task OpenAnyAsync(string path) =>
        path.EndsWith(".mswmm", StringComparison.OrdinalIgnoreCase) ? ImportMswmmFileAsync(path)
        : path.EndsWith(ProjectPackage.Extension, StringComparison.OrdinalIgnoreCase) ? OpenPackageAsync(path)
        : OpenFileAsync(path);

    private async Task OpenPackageAsync(string path)
    {
        if (await _files.PackageMediaFolderAsync(Path.GetFileNameWithoutExtension(path)) is not { } folder)
        {
            return;
        }

        try
        {
            Engine.Playback.Stop();
            IsBusy = true;
            BusyText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Opening, Path.GetFileName(path));
            Project p = await Task.Run(() =>
            {
                using Stream s = FileStore.Current.OpenRead(path);
                return ProjectPackage.Import(s, folder);
            });
            p.Settings = OptionsViewModel.ForProject(Settings, p.Settings);
            DeleteRecovery();
            Session.Replace(p);
            Session.Undo.MarkDirty();
            Pane = UpperPane.Contents;
            int missing = p.Media.Count(m => m.Missing);
            if (missing > 0)
            {
                await Inform(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.MissingFilesPrompt, missing), null, MessageIcon.Warning);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Log.Error("project", $"Open {path} failed", e);
            await _messages.ShowAsync(new MessageRequest(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.PackageCannotOpen, e.Message), MessageButtons.Ok, MessageIcon.Error));
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ExportPackageAsync()
    {
        if (await _files.SavePackageAsync(Project.DisplayName, Folder(Project.FilePath)) is not { } path)
        {
            return;
        }

        IReadOnlyList<string> missing;
        IsBusy = true;
        BusyText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Exporting, Path.GetFileName(path));
        try
        {
            missing = await Task.Run(() =>
            {
                using Stream s = FileStore.Current.Create(path);
                return ProjectPackage.Export(Project, s);
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error("project", $"Export {path} failed", e);
            await _messages.ShowAsync(new MessageRequest(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.PackageCannotWrite, e.Message), MessageButtons.Ok, MessageIcon.Error));
            return;
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }

        if (missing.Count > 0)
        {
            await _messages.ShowAsync(new MessageRequest(Strings.PackageMissingMedia + "\n\n" + string.Join("\n", missing), MessageButtons.Ok, MessageIcon.Warning) { IsList = true });
        }

    }

    public System.Windows.Input.ICommand Unavailable => TaskEntry.Unavailable(string.Empty).Command;

    [RelayCommand]
    private async Task OpenRecentAsync(string path)
    {
        if (await ConfirmDiscardAsync())
        {
            await OpenAnyAsync(path);
        }
    }

    private static string? Folder(string? path) => path is null ? null : Path.GetDirectoryName(path);

    public async Task<bool> OpenFileAsync(string path)
    {
        try
        {
            Engine.Playback.Stop();
            Project p = await Task.Run(() => ProjectSerializer.Load(path));
            p.FilePath = Path.GetFullPath(path);

            p.Settings = OptionsViewModel.ForProject(Settings, p.Settings);
            DeleteRecovery();
            Session.Replace(p);
            AddRecent(p.FilePath);
            Pane = UpperPane.Contents;
            int missing = p.Media.Count(m => m.Missing);
            if (missing > 0)
            {
                await Inform(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.MissingFilesPrompt, missing), null, MessageIcon.Warning);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Log.Error("project", $"Open {path} failed", e);
            await _messages.ShowAsync(new MessageRequest(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ProjectCannotOpen, e.Message), MessageButtons.Ok, MessageIcon.Error));
            Settings.RecentProjects.Remove(path);
            RecentProjects.Remove(path);
            return false;
        }
    }

    [RelayCommand]
    private async Task ImportMswmmAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        string? path = await _files.OpenMswmmAsync(Settings.LastImportFolder);
        if (path is not null)
        {
            await ImportMswmmFileAsync(path);
        }
    }

    private async Task ImportMswmmFileAsync(string path)
    {
        try
        {
            MswmmImportResult result = await Task.Run(() => MswmmImporter.Import(path));
            if (result.MissingMedia.Count > 0 && await _files.PickFolderAsync(Strings.PickMediaSearchFolder, Folder(path)) is { } folder)
            {
                result = await Task.Run(() => MswmmImporter.Import(path, folder));
            }

            Engine.Playback.Stop();
            result.Project.Settings = OptionsViewModel.ForProject(Settings, result.Project.Settings);
            DeleteRecovery();
            Session.Replace(result.Project);
            Session.Undo.MarkDirty();
            var report = result.Warnings.Concat(result.MissingMedia.Select(m => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ImportNotFoundLine, m))).ToList();
            if (report.Count > 0)
            {
                await _messages.ShowAsync(new MessageRequest(Strings.ImportReportIntro + "\n\n" + string.Join('\n', report.Take(20)), MessageButtons.Ok, MessageIcon.Information) { Title = Strings.ImportReportTitle });
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException)
        {
            Log.Error("mswmm", $"Import {path} failed", e);
            await _messages.ShowAsync(new MessageRequest(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.MswmmCannotImport, e.Message), MessageButtons.Ok, MessageIcon.Error));
        }
    }

    [RelayCommand]
    private async Task<bool> SaveAsync()
    {
        if (Project.FilePath is null)
        {
            return await SaveAsAsync();
        }

        return await SaveToAsync(Project.FilePath);
    }

    [RelayCommand]
    private async Task<bool> SaveAsAsync()
    {
        string? path = await _files.SaveProjectAsync(Project.DisplayName, Folder(Project.FilePath ?? Settings.RecentProjects.FirstOrDefault()));
        if (path is null)
        {
            return false;
        }

        if (!path.EndsWith(ProjectSerializer.Extension, StringComparison.OrdinalIgnoreCase))
        {
            path += ProjectSerializer.Extension;
        }

        return await SaveToAsync(path);
    }

    private async Task<bool> SaveToAsync(string path)
    {
        try
        {
            await Task.Run(() => ProjectSerializer.Save(Project, path));
            DeleteRecovery();
            Project.FilePath = Path.GetFullPath(path);
            Session.Undo.MarkSaved();
            AddRecent(Project.FilePath);
            UpdateTitle();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await _messages.ShowAsync(new MessageRequest(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ProjectCannotSave, e.Message), MessageButtons.Ok, MessageIcon.Error));
            return false;
        }
    }

    private void AddRecent(string path)
    {
        Settings.AddRecent(path);
        RecentProjects.Clear();
        foreach (string r in Settings.RecentProjects)
        {
            RecentProjects.Add(r);
        }

        _store.Save();
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (await ConfirmDiscardAsync())
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StartRecoveryTimer()
    {
        _recoveryTimer?.Dispose();
        if (!Settings.AutoRecoveryEnabled)
        {
            return;
        }

        TimeSpan every = TimeSpan.FromMinutes(Math.Max(1, Settings.AutoRecoveryMinutes));
        _recoveryTimer = new Timer(_ => _dispatcher.Post(WriteRecovery), null, every, every);
    }

    public static string RecoveryFolder => Path.Combine(IO.AppPaths.StateDir, "recovery");

    public static string RecoveryPath => Path.Combine(RecoveryFolder, "AutoRecover.dmmproj");

    public static string RecoveryOriginPath => Path.Combine(RecoveryFolder, "AutoRecover.origin");

    private static string LegacyRecoveryPath => Path.Combine(RecoveryFolder, "Untitled.dmmproj");

    private bool _changedSinceRecovery;

    public void WriteRecovery()
    {
        if (!Session.Undo.IsDirty || !_changedSinceRecovery || Project.IsEmpty && Project.FilePath is null)
        {
            return;
        }

        try
        {
            ProjectSerializer.SaveCopy(Project, RecoveryPath);
            FileStore.WriteAllText(RecoveryOriginPath, Project.FilePath ?? string.Empty);
            _changedSinceRecovery = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn("recovery", e.Message);
        }
    }

    private void DeleteRecovery()
    {
        _changedSinceRecovery = true;
        foreach (string f in new[] { RecoveryPath, RecoveryOriginPath, LegacyRecoveryPath })
        {
            try
            {
                FileStore.Current.Delete(f);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warn("recovery", e.Message);
            }
        }
    }

    private async Task<bool> OfferRecoveryAsync()
    {
        string? copy = FileStore.Current.Exists(RecoveryPath) ? RecoveryPath : FileStore.Current.Exists(LegacyRecoveryPath) ? LegacyRecoveryPath : null;
        if (copy is null)
        {
            return false;
        }

        MessageResult r = await _messages.ShowAsync(new MessageRequest(Strings.RecoverPrompt, MessageButtons.YesNo, MessageIcon.Question));
        bool recover = r == MessageResult.Yes;
        if (recover)
        {
            try
            {
                Project p = ProjectSerializer.Load(copy);
                string origin = copy == RecoveryPath && FileStore.Current.Exists(RecoveryOriginPath) ? FileStore.ReadAllText(RecoveryOriginPath).Trim() : string.Empty;
                p.FilePath = Path.IsPathFullyQualified(origin) ? origin : null;
                p.Settings = OptionsViewModel.ForProject(Settings, p.Settings);
                Session.Replace(p);
                Session.Undo.MarkDirty();
            }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                Log.Warn("recovery", $"Could not recover: {e.Message}");
                recover = false;
            }
        }

        DeleteRecovery();
        return recover;
    }

    [RelayCommand]
    private Task ImportMediaAsync() => ImportWithDialogAsync(MediaFilter.All);

    [RelayCommand]
    private Task ImportVideosAsync() => ImportWithDialogAsync(MediaFilter.Video);

    [RelayCommand]
    private Task ImportPicturesAsync() => ImportWithDialogAsync(MediaFilter.Pictures);

    [RelayCommand]
    private Task ImportAudioAsync() => ImportWithDialogAsync(MediaFilter.Audio);

    private async Task ImportWithDialogAsync(MediaFilter filter)
    {
        IReadOnlyList<string> paths = await _files.ImportMediaAsync(filter, ImportStartFolder(filter));
        if (paths.Count > 0)
        {
            await ImportFilesAsync(paths);
            RememberImportFolder(filter, paths[0]);
        }
    }

    public string ImportStartFolder(MediaFilter filter)
    {
        string remembered = filter switch
        {
            MediaFilter.Video => Settings.ImportFolderVideo,
            MediaFilter.Pictures => Settings.ImportFolderPictures,
            MediaFilter.Audio => Settings.ImportFolderAudio,
            _ => Settings.ImportFolderAllMedia,
        };
        if (!string.IsNullOrEmpty(remembered) && FileStore.Current.DirectoryExists(remembered))
        {
            return remembered;
        }

        return filter switch
        {
            MediaFilter.Pictures => IO.AppPaths.PicturesDir,
            MediaFilter.Audio => IO.AppPaths.MusicDir,
            _ => IO.AppPaths.VideosDir,
        };
    }

    private void RememberImportFolder(MediaFilter filter, string firstPath)
    {
        string folder = Path.GetDirectoryName(firstPath) ?? string.Empty;
        switch (filter)
        {
            case MediaFilter.Video:
                Settings.ImportFolderVideo = folder;
                break;
            case MediaFilter.Pictures:
                Settings.ImportFolderPictures = folder;
                break;
            case MediaFilter.Audio:
                Settings.ImportFolderAudio = folder;
                break;
            default:
                Settings.ImportFolderAllMedia = folder;
                break;
        }
    }

    public async Task<IReadOnlyList<MediaItem>> ImportFilesAsync(IReadOnlyList<string> paths, bool show = true)
    {
        if (paths.Count == 0)
        {
            return [];
        }

        IsBusy = true;
        try
        {
            var dialog = new Dialogs.ProgressViewModel(Strings.ImportMediaItemsTitle);
            var progress = new Progress<(int Done, int Total, string Name)>(p =>
            {
                BusyText = p.Total > 1 ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ImportingMany, p.Name, p.Done + 1, p.Total) : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ImportingOne, p.Name);
                dialog.Report(p.Done, p.Total, p.Name);
            });

            Task<MediaImporter.Result> work = MediaImporter.ImportAsync(paths, createClips: false, Session.NextImportBatch, progress, dialog.Token);
            Task shown = Task.CompletedTask;
            if (await Task.WhenAny(work, Task.Delay(250)) != work)
            {
                shown = _dialogs.ShowProgressAsync(dialog);
            }

            MediaImporter.Result result = await work;
            dialog.Finish();
            await shown;
            Session.Editor.ImportMedia(result.Items);
            Contents.View = ContentsView.ImportedMedia;
            Pane = UpperPane.Contents;
            if (result.Failures.Count > 0)
            {
                await _messages.ShowAsync(new MessageRequest(ImportErrorText(result.Failures), MessageButtons.Ok, MessageIcon.Error) { IsList = true });
            }

            if (show && result.Items.Count > 0 && Contents.Items.FirstOrDefault(i => i.Media.Id == result.Items[^1].Id) is { } last)
            {
                Contents.Reveal(last);
                Monitor.ShowItem(last.Media, last.Clip, play: false);
            }

            return result.Items;
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    public static string ImportErrorText(IEnumerable<MediaImporter.Failure> failures) =>
        string.Join("\n\n", failures.Select(f => f.Error switch
        {
            ImportError.NotFound => Format(Strings.ImportNotFound, f.Path),
            ImportError.Folder => Strings.ImportFolders,
            ImportError.Empty => Format(Strings.ImportEmpty, f.Path),
            ImportError.NotSupported => Format(Strings.ImportNotSupported, f.Path),
            ImportError.MissingCodec => Format(Strings.ImportMissingCodec, f.Path),
            _ when string.IsNullOrWhiteSpace(f.Reason) => Format(Strings.ImportFailedNoReason, f.Path),
            _ => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ImportFailed, f.Path, f.Reason),
        }).Distinct(StringComparer.Ordinal));

    private static string Format(string format, string path) => string.Format(System.Globalization.CultureInfo.CurrentCulture, format, path);

    private static bool IsProjectDrop(IReadOnlyList<string> paths) => paths.Count == 1 && IsProjectFile(paths[0]);

    private static bool IsProjectFile(string path) =>
        path.EndsWith(".dmmproj", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(ProjectPackage.Extension, StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".mswmm", StringComparison.OrdinalIgnoreCase);

    public bool CanDropFiles(IReadOnlyList<string> paths) =>
        !IsBusy && paths.Count > 0 && (IsProjectDrop(paths) || paths.Any(MediaFormats.IsSupported));

    public async Task DropFilesAsync(IReadOnlyList<string> paths)
    {
        if (!CanDropFiles(paths))
        {
            return;
        }

        if (IsProjectDrop(paths))
        {
            if (await ConfirmDiscardAsync())
            {
                await OpenAnyAsync(paths[0]);
            }

            return;
        }

        await ImportFilesAsync(paths);
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Undo.Undo();

    private bool CanUndo() => Session.Undo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Undo.Redo();

    private bool CanRedo() => Session.Undo.CanRedo;

    private bool TimelineActive => Session.ActiveSelection == SelectionKind.Timeline && Session.SelectedClips.Count > 0;

    private bool ContentsActive => Session.ActiveSelection == SelectionKind.Contents && Contents.HasSelection;

    private bool HasAnySelection() => TimelineActive || ContentsActive;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Cut()
    {
        Copy();
        Remove();
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        if (Pane == UpperPane.Contents && Contents.View != ContentsView.ImportedMedia && Contents.SelectedCatalogItem is { } cat && !TimelineActive)
        {
            _clipboard.SetCatalog(cat.IsTransition ? cat.Id : null, cat.IsTransition ? null : cat.Id);
        }
        else if (ContentsActive)
        {
            _clipboard.SetItems(Contents.Selection);
        }
        else if (Session.SelectionTrack == AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Transition)
        {
            string? id = Project.VideoTrack.FirstOrDefault(c => Session.SelectedClips.Contains(c.Id))?.TransitionIn?.TransitionId;
            _clipboard.SetCatalog(id, null);
        }
        else
        {
            var ids = Session.SelectedClips.ToHashSet();
            var video = Project.VideoTrack.Where(c => ids.Contains(c.Id)).ToList();
            var audio = Project.AudioMusicTrack.Where(a => ids.Contains(a.Id)).ToList();
            var titles = Project.TitleOverlayTrack.Where(t => ids.Contains(t.Id)).ToList();
            var mediaIds = video.Select(v => v.MediaId).Concat(audio.Select(a => a.MediaId)).ToHashSet();
            _clipboard.Set(video, audio, titles, Project.Media.Where(m => mediaIds.Contains(m.Id)).ToList());
        }

        PasteCommand.NotifyCanExecuteChanged();
    }

    private bool CanCopy() => HasAnySelection() || Pane == UpperPane.Contents && Contents.View != ContentsView.ImportedMedia && Contents.SelectedCatalogItem is not null;

    private bool HasTimelineSelection() => Session.SelectedClips.Count > 0;

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void Paste()
    {
        MediaTime playhead = Monitor.ProjectPosition;
        TimelineLayout layout = TimelineLayout.Compute(Project);
        if (_clipboard.TransitionId is { } tr || _clipboard.EffectId is not null)
        {
            int at = Session.SelectionTrack == AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video && Session.SelectedVideo is { } v ? Project.IndexOfVideo(v.Id) : layout.IndexAt(playhead);
            if (at >= 0)
            {
                Guid target = Project.VideoTrack[at].Id;
                _ = _clipboard.TransitionId is { } t ? Session.Editor.SetTransition(target, t) : Session.Editor.AddEffect([target], _clipboard.EffectId!);
            }

            return;
        }

        int index = Session.SelectionTrack == AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video && Session.SelectedVideo is { } sel
            ? Project.IndexOfVideo(sel.Id) + 1
            : Project.VideoTrack.Count == 0 ? 0 : layout.InsertIndexAt(playhead);
        IReadOnlyList<VideoClip> video = _clipboard.Items.Count > 0
            ? [.. _clipboard.Items.Where(i => i.Media.Kind != MediaKind.Audio).Select(i => Session.Editor.NewVideoClip(i.Media, i.Clip))]
            : [.. _clipboard.Video.Select(v => v with { Id = Guid.NewGuid() })];
        IReadOnlyList<AudioClip> audio = _clipboard.Items.Count > 0
            ? [.. _clipboard.Items.Where(i => i.Media.Kind == MediaKind.Audio).Select(i => Session.Editor.NewAudioClip(i.Media, i.Clip, playhead))]
            : [.. _clipboard.Audio.Select(a => a with { Id = Guid.NewGuid() })];
        IReadOnlyList<TitleClip> titles = [.. _clipboard.Titles.Select(t => t with { Id = Guid.NewGuid() })];
        var pasted = new List<Guid>();
        AvaMovieMaker.ViewModels.Timeline.TimelineTrack track = video.Count > 0 ? AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video : audio.Count > 0 ? AvaMovieMaker.ViewModels.Timeline.TimelineTrack.AudioMusic : AvaMovieMaker.ViewModels.Timeline.TimelineTrack.TitleOverlay;
        Session.Editor.Batch(UndoNames.Paste, p =>
        {
            foreach (MediaItem m in _clipboard.Media.Where(m => p.FindMedia(m.Id) is null))
            {
                p.Media.Add(m);
            }

            int i = Math.Min(index, p.VideoTrack.Count);
            p.VideoTrack.InsertRange(i, video.Select((v, k) => i + k == 0 ? v with { TransitionIn = null } : v));
            pasted.AddRange(video.Select(v => v.Id));
            MediaTime at = playhead;
            foreach (AudioClip a in audio)
            {
                MediaTime start = TimelineEditor.FreeStart(p.AudioMusicTrack, at, a.Length);
                p.AudioMusicTrack.Add(a with { Start = start });
                pasted.Add(a.Id);
                at = start + a.Length;
            }

            p.AudioMusicTrack.Sort((x, y) => x.Start.CompareTo(y.Start));
            foreach (TitleClip t in titles)
            {
                p.TitleOverlayTrack.Add(t with { Start = playhead });
                pasted.Add(t.Id);
            }
        });
        if (pasted.Count > 0)
        {
            Session.Select(pasted.Where(id => track == AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video ? video.Any(v => v.Id == id) : true).ToList(), track);
        }
    }

    private bool CanPaste() => !_clipboard.IsEmpty;

    [RelayCommand(CanExecute = nameof(HasAnySelection))]
    private void Remove()
    {
        if (ContentsActive)
        {
            Contents.RemoveSelected();
            return;
        }

        AvaMovieMaker.ViewModels.Timeline.TimelineTrack track = Session.SelectionTrack;
        var selected = Session.SelectedClips.ToHashSet();
        List<Guid> order = Timeline.TrackItems(track).Select(i => i.Id).ToList();
        int last = order.FindLastIndex(selected.Contains);
        Guid? next = order.Skip(last + 1).Where(id => !selected.Contains(id)).Cast<Guid?>().FirstOrDefault()
            ?? order.Take(Math.Max(0, last)).Where(id => !selected.Contains(id)).Cast<Guid?>().LastOrDefault();
        if (track == AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Transition)
        {
            foreach (Guid id in selected)
            {
                Session.Editor.RemoveTransition(id);
            }

            Session.Select([], track);
            return;
        }

        if (Session.Editor.RemoveClips(selected) && next is { } n)
        {
            Session.Select([n], track);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (Session.ActiveSelection == SelectionKind.Contents || Pane == UpperPane.Contents && Session.ActiveSelection == SelectionKind.None)
        {
            Contents.SelectAll();
        }
        else if (IsTimeline)
        {
            Timeline.SelectAll();
        }
        else
        {
            Storyboard.SelectAll();
        }
    }

    [RelayCommand(CanExecute = nameof(ContentsActiveCheck))]
    private void Rename() => Contents.BeginRename();

    private bool ContentsActiveCheck() => ContentsActive;

    [RelayCommand(CanExecute = nameof(CanClearTimeline))]
    private void ClearTimeline() => Session.Editor.ClearTimeline(IsTimeline ? UndoNames.ClearTimeline : UndoNames.ClearStoryboard);

    private bool CanClearTimeline() => !Project.IsEmpty;

    [RelayCommand(CanExecute = nameof(CanEditTitle))]
    private void EditTitle()
    {
        Guid id = Session.SelectedClips[0];
        TitleEditor.Edit(id);
        Pane = UpperPane.Titles;
    }

    private bool CanEditTitle() =>
        Session.SelectedClips.Count == 1 && (Project.VideoTrack.Any(c => c.Id == Session.SelectedClips[0] && c.Kind == VideoClipKind.Title)
                                             || Project.TitleOverlayTrack.Any(t => t.Id == Session.SelectedClips[0]));

    [RelayCommand(CanExecute = nameof(HasMissing))]
    private async Task BrowseMissingAsync()
    {
        foreach (MediaItem m in Project.Media.Where(m => m.Missing).ToList())
        {
            string? path = await _files.OpenFileAsync(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.FindFile, Path.GetFileName(m.Path)), null);
            if (path is null)
            {
                break;
            }

            Session.Editor.Relink(m.Id, path);
            Engine.Frames.Reset();
        }
    }

    private bool HasMissing() => Project.Media.Any(m => m.Missing);

    [RelayCommand]
    private void ToggleStoryboardTimeline() => IsTimeline = !IsTimeline;

    [RelayCommand]
    private void ShowStoryboardView() => IsTimeline = false;

    [RelayCommand]
    private void ShowTimelineView() => IsTimeline = true;

    [RelayCommand]
    private void ZoomIn()
    {
        IsTimeline = true;
        Timeline.ZoomInCommand.Execute(null);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        IsTimeline = true;
        Timeline.ZoomOutCommand.Execute(null);
    }

    public event EventHandler? ZoomToFitRequested;

    [RelayCommand]
    private void ZoomToFit()
    {
        IsTimeline = true;
        ZoomToFitRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleTasks() => ShowTasks = !ShowTasks;

    [RelayCommand]
    private void SetMonitorSize(string size) => LargeMonitor = size == "large";

    [RelayCommand]
    private void SetContentsDetails(string mode)
    {
        Contents.IsDetails = mode == "details";
        Settings.ContentsDetails = Contents.IsDetails;
    }

    [RelayCommand]
    private void SortBy(ContentsSort sort) => Contents.Sort = sort;

    [RelayCommand]
    private void SetTheme(string theme)
    {
        Settings.Theme = theme;
        _dialogs.ApplyTheme(theme);
        _store.Save();
    }

    private bool CanShowImportedMedia() => Pane != UpperPane.Contents || Contents.View != ContentsView.ImportedMedia;

    [RelayCommand(CanExecute = nameof(CanShowImportedMedia))]
    private void ShowImportedMedia()
    {
        Pane = UpperPane.Contents;
        Contents.View = ContentsView.ImportedMedia;
    }

    private bool CanShowEffects() => Pane != UpperPane.Contents || Contents.View != ContentsView.Effects;

    [RelayCommand(CanExecute = nameof(CanShowEffects))]
    private void ShowEffects()
    {
        Pane = UpperPane.Contents;
        Contents.View = ContentsView.Effects;
    }

    private bool CanShowTransitions() => Pane != UpperPane.Contents || Contents.View != ContentsView.Transitions;

    [RelayCommand(CanExecute = nameof(CanShowTransitions))]
    private void ShowTransitions()
    {
        Pane = UpperPane.Contents;
        Contents.View = ContentsView.Transitions;
    }

    private void PreviewCatalogItem(CatalogItemViewModel item, bool play)
    {
        string key = (item.IsTransition ? "t:" : "e:") + item.Id;
        if (Monitor.Target == PreviewTarget.Item && _catalogLoaded == key)
        {
            if (play && !Monitor.IsPlaying)
            {
                Monitor.Playback.Play();
            }

            return;
        }

        Monitor.ShowPlan(CatalogSamplePlan(item.Id, item.IsTransition, Project.Settings), item.Name, play);
        _catalogLoaded = key;
    }

    private string? _catalogLoaded;

    public static RenderPlan CatalogSamplePlan(string id, bool transition, ProjectSettings settings)
    {
        (string first, string second) = EffectCatalog.IsFraming(id) ? SamplePictures.Other(settings.Aspect) : SamplePictures.For(settings.Aspect);
        var scratch = new Project { Settings = settings };
        MediaItem Picture(string path) => new() { Kind = MediaKind.Picture, Path = path, Name = Path.GetFileNameWithoutExtension(path) };
        MediaItem a = Picture(first), b = Picture(second);
        scratch.Media.AddRange([a, b]);
        var editor = new TimelineEditor(scratch, new Undo.UndoStack());
        VideoClip ca = editor.NewVideoClip(a) with { StillDuration = MediaTime.FromSeconds(3) };
        if (transition)
        {
            VideoClip cb = editor.NewVideoClip(b) with { StillDuration = MediaTime.FromSeconds(3), TransitionIn = new TransitionRef(id, MediaTime.FromSeconds(1.5)) };
            scratch.VideoTrack.AddRange([ca, cb]);
        }
        else
        {
            scratch.VideoTrack.Add(ca with { Effects = [new EffectRef(id)] });
        }

        return RenderPlanner.Build(scratch);
    }

    [RelayCommand]
    private void AutoMovieShow()
    {
        AutoMovie.Open();
        Pane = UpperPane.AutoMovie;
    }

    public IRelayCommand AutoMovieCommand => AutoMovieShowCommand;

    private async Task<MediaItem?> BrowseAutoMovieMusicAsync()
    {
        IReadOnlyList<string> paths = await _files.ImportMediaAsync(MediaFilter.Audio, ImportStartFolder(MediaFilter.Audio));
        if (paths.Count == 0)
        {
            return null;
        }

        IReadOnlyList<MediaItem> items = await ImportFilesAsync(paths.Take(1).ToList(), show: false);
        RememberImportFolder(MediaFilter.Audio, paths[0]);
        Pane = UpperPane.AutoMovie;
        return items.FirstOrDefault(m => m.Kind == MediaKind.Audio);
    }

    [RelayCommand]
    private void TitlesShow()
    {
        TitleEditor.Start();
        Pane = UpperPane.Titles;
    }

    public IRelayCommand TitlesCommand => TitlesShowCommand;

    [RelayCommand(CanExecute = nameof(CanCreateClips))]
    private async Task CreateClipsAsync()
    {
        var targets = Contents.Selection.Where(s => s.Media.Kind == MediaKind.Video && !s.Media.Missing).Select(s => s.Media).Distinct().ToList();
        IsBusy = true;
        try
        {
            foreach (MediaItem m in targets)
            {
                BusyText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.CreatingClips, m.Name);
                var ranges = await ClipDetector.DetectAsync(m.Path);
                Session.Editor.SetSourceClips(m.Id, MediaImporter.ClipsFor(m.Name, ranges));
            }
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    private bool CanCreateClips() => Contents.SelectionHasVideo;

    private bool CanTakePicture() => Monitor.HasVideo;

    [RelayCommand(CanExecute = nameof(CanTakePicture))]
    private async Task TakePictureAsync()
    {
        Monitor.Playback.Pause();
        string folder = IO.AppPaths.PicturesDir;
        try
        {
            FileStore.Current.CreateDirectory(folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        string baseName = Monitor.SourceName.Length > 0 ? Monitor.SourceName : Project.FilePath is not null ? Project.DisplayName : PictureNames.Fallback;
        string? path = await _files.SavePictureAsync(PictureNames.Suggest(folder, baseName) ?? PictureNames.Clean(baseName) + ".jpg", folder);
        if (path is null)
        {
            return;
        }

        if (!PictureNames.IsJpeg(path))
        {
            path += ".jpg";
        }

        try
        {
            await Monitor.TakePictureAsync(path, Project.Properties.Author);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Inform(Strings.PictureNotSaved, null, MessageIcon.Error);
            return;
        }

        await ImportFilesAsync([path], show: false);
    }

    private bool CanNarrate() => AvaMovieMaker.Audio.AudioSystem.CaptureDevices.Count > 0;

    [RelayCommand(CanExecute = nameof(CanNarrate))]
    private async Task NarrateAsync()
    {
        if (AvaMovieMaker.Audio.AudioSystem.CaptureDevices.Count == 0)
        {
            await Inform(Strings.NarrationNoHardware, null, MessageIcon.Error);
            return;
        }

        if (!IsTimeline)
        {
            IsTimeline = true;
            await Inform(Strings.NarrationNeedsTimeline, "narration-needs-timeline");
        }

        OpenNarration(null, null);
    }

    public void OpenNarration(IReadOnlyList<string>? devices, Func<string, string, MediaTime?, AvaMovieMaker.Audio.Capture.INarrationRecording?>? record)
    {
        Narration = new NarrationViewModel(Session, Monitor, _dispatcher, _messages, _files, Settings, devices, record);
        Narration.Closed += (_, _) => Pane = UpperPane.Contents;
        Pane = UpperPane.Narration;
    }

    [RelayCommand]
    private void ShowAudioLevels() => _dialogs.ShowAudioLevels(AudioLevels);

    [RelayCommand]
    private async Task OptionsAsync()
    {
        var vm = new OptionsViewModel(Settings, Project.Settings);
        if (await _dialogs.ShowOptionsAsync(vm))
        {
            ProjectSettings before = Project.Settings;
            ProjectSettings next = vm.Apply(before);
            if (next != before)
            {
                Project.Settings = next;
                Project.NotifyChanged();
                if (next.Format != before.Format || next.Aspect != before.Aspect)
                {
                    Session.Undo.MarkDirty();
                }
            }

            Engine.Decoders.AllowHardware = Settings.HardwareDecode;
            _store.Save();
            StartRecoveryTimer();
        }
    }

    [RelayCommand]
    private async Task ProjectPropertiesAsync()
    {
        var vm = new ProjectPropertiesViewModel(Project.Properties, Project.Duration, Settings.DefaultAuthor, Project.FilePath is null ? null : Project.DisplayName);
        vm.ApplyRequested += (_, _) => Session.Editor.SetProperties(vm.ToProperties());
        if (await _dialogs.ShowProjectPropertiesAsync(vm))
        {
            Session.Editor.SetProperties(vm.ToProperties());
        }
    }

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private Task PublishAsync() => RunPublishAsync(PublishPage.Where);

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private Task PublishToComputerAsync() => RunPublishAsync(PublishPage.Name);

    private async Task RunPublishAsync(PublishPage first)
    {
        Engine.Playback.Pause();
        var wizard = new PublishWizardViewModel(Session, Engine, _files, _dialogs, _messages, _dispatcher, Settings) { Page = first };
        await _dialogs.ShowPublishWizardAsync(wizard);
        _store.Save();
    }

    [RelayCommand]
    private Task AboutAsync() => _dialogs.ShowAboutAsync();

    [RelayCommand(CanExecute = nameof(ContentsActiveCheck))]
    private void AddToTimeline() => Contents.AddToTimeline();

    private VideoClip? SelectedVideoClip => Session.SelectedVideo;

    private bool HasVideoSelection() => SelectedVideoClip is not null;

    private bool HasClipWithAudio() =>
        Session.SelectedClips.Any(id => Project.AudioMusicTrack.Any(a => a.Id == id) || Project.VideoTrack.Any(c => c.Id == id && c.Kind == VideoClipKind.Video));

    private IEnumerable<(Guid Id, AudioSettings Audio)> SelectedAudio()
    {
        foreach (Guid id in Session.SelectedClips)
        {
            if (Project.VideoTrack.FirstOrDefault(c => c.Id == id && c.Kind == VideoClipKind.Video) is { } v)
            {
                yield return (id, v.Audio);
            }
            else if (Project.AudioMusicTrack.FirstOrDefault(a => a.Id == id) is { } a)
            {
                yield return (id, a.Audio);
            }
        }
    }

    public bool IsAudioMuted => SelectedAudio().ToList() is { Count: > 0 } a && a.All(i => i.Audio.Mute);

    public bool IsAudioFadeIn => SelectedAudio().Select(i => i.Audio.FadeIn).FirstOrDefault();

    public bool IsAudioFadeOut => SelectedAudio().Select(i => i.Audio.FadeOut).FirstOrDefault();

    [RelayCommand(CanExecute = nameof(HasClipWithAudio))]
    private void AudioMute()
    {
        var items = SelectedAudio().ToList();
        bool mute = !items.All(i => i.Audio.Mute);
        foreach ((Guid id, AudioSettings s) in items)
        {
            Session.Editor.SetAudio(id, s with { Mute = mute });
        }
    }

    [RelayCommand(CanExecute = nameof(HasClipWithAudio))]
    private void AudioFadeIn()
    {
        (Guid id, AudioSettings s) = SelectedAudio().First();
        Session.Editor.SetAudio(id, s with { FadeIn = !s.FadeIn });
    }

    [RelayCommand(CanExecute = nameof(HasClipWithAudio))]
    private void AudioFadeOut()
    {
        (Guid id, AudioSettings s) = SelectedAudio().First();
        Session.Editor.SetAudio(id, s with { FadeOut = !s.FadeOut });
    }

    [RelayCommand(CanExecute = nameof(HasClipWithAudio))]
    private async Task AudioVolumeAsync()
    {
        var first = SelectedAudio().First();
        var vm = new ClipVolumeViewModel(first.Audio);
        if (await _dialogs.ShowClipVolumeAsync(vm))
        {
            foreach ((Guid id, AudioSettings s) in SelectedAudio().ToList())
            {
                Session.Editor.SetAudio(id, vm.ToSettings() with { FadeIn = s.FadeIn, FadeOut = s.FadeOut });
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasVideoSelection))]
    private async Task VideoEffectsAsync()
    {
        VideoClip clip = SelectedVideoClip!;
        var vm = new ClipEffectsViewModel(clip.Effects.Select(e => e.EffectId));
        if (await _dialogs.ShowEffectsAsync(vm))
        {
            Session.Editor.SetEffects(clip.Id, vm.Result);
        }
    }

    [RelayCommand(CanExecute = nameof(HasEffects))]
    private void RemoveEffects() =>
        Session.Editor.RemoveEffects([.. Session.SelectedClips.Where(id => Project.IndexOfVideo(id) >= 0)]);

    private bool HasEffects() =>
        Session.SelectedClips.Any(id => Project.IndexOfVideo(id) is var i and >= 0 && Project.VideoTrack[i].Effects.Count > 0);

    private IEnumerable<VideoClip> SelectedFramable() =>
        Session.SelectedClips.Select(id => Project.IndexOfVideo(id)).Where(i => i >= 0).Select(i => Project.VideoTrack[i]).Where(TimelineEditor.CanFrame);

    public bool CanFit => SelectedFramable().Any();

    public FrameFitMode? SelectedFit =>
        SelectedFramable().Select(c => EffectCatalog.FitOf(c.Effects.Select(e => e.EffectId))).Distinct().ToList() is [var only] ? only : null;

    [RelayCommand(CanExecute = nameof(CanFit))]
    private void SetFit(FrameFitMode mode) => Session.Editor.SetFit([.. SelectedFramable().Select(c => c.Id)], mode);

    public AspectRatio Aspect => Project.Settings.Aspect;

    [RelayCommand]
    private void SetAspectRatio(AspectRatio aspect) => Session.Editor.SetAspect(aspect);

    [RelayCommand(CanExecute = nameof(HasVideoSelection))]
    private void VideoFadeIn() => Session.Editor.ToggleEffect(SelectedVideoClip!.Id, "fade-in-from-black");

    [RelayCommand(CanExecute = nameof(HasVideoSelection))]
    private void VideoFadeOut() => Session.Editor.ToggleEffect(SelectedVideoClip!.Id, "fade-out-to-black");

    [RelayCommand(CanExecute = nameof(CanTrim))]
    private void TrimBeginning()
    {
        MediaTime t = Monitor.Playback.Position;
        Guid? clip = ClipAt(t);
        if (Session.Editor.TrimStartAt(t) && clip is { } id && StartOf(id) is { } start)
        {
            Monitor.Seek(start);
        }
    }

    [RelayCommand(CanExecute = nameof(CanTrim))]
    private void TrimEnd()
    {
        MediaTime t = Monitor.Playback.Position;
        bool video = TimelineLayout.Compute(Project).IndexAt(t) >= 0;
        if (Session.Editor.TrimEndAt(t) && video)
        {
            Monitor.Seek(MediaTime.Max(MediaTime.Zero, t - MediaTime.FrameDuration(Project.Settings.FrameRate)));
        }
    }

    private Guid? ClipAt(MediaTime t)
    {
        TimelineLayout layout = TimelineLayout.Compute(Project);
        int i = layout.IndexAt(t);
        return i >= 0 ? Project.VideoTrack[i].Id : Project.AudioMusicTrack.FirstOrDefault(a => t > a.Start && t < a.End)?.Id;
    }

    private MediaTime? StartOf(Guid id)
    {
        int i = Project.IndexOfVideo(id);
        return i >= 0 ? TimelineLayout.Compute(Project).Starts[i] : Project.AudioMusicTrack.FirstOrDefault(a => a.Id == id)?.Start;
    }

    private bool CanTrim() => Monitor.Target == PreviewTarget.Project && !Project.IsEmpty;

    [RelayCommand(CanExecute = nameof(HasOneSelected))]
    private void ClearTrimPoints() => Session.Editor.ClearTrimPoints(Session.SelectedClips[0]);

    private bool HasOneSelected() => Session.SelectedClips.Count == 1;

    [RelayCommand(CanExecute = nameof(CanSplit))]
    private void Split() => Monitor.SplitCommand.Execute(null);

    private bool CanSplit() => !Project.IsEmpty || Monitor.Target == PreviewTarget.Item;

    [RelayCommand(CanExecute = nameof(CanCombine))]
    private void Combine()
    {
        if (ContentsActive)
        {
            Contents.CombineSelected();
        }
        else
        {
            Session.Editor.Combine(Session.SelectedClips.ToList());
        }
    }

    private bool CanCombine() => ContentsActive ? Contents.SelectedItems.Count > 1 : Session.SelectedClips.Count > 1;

    [RelayCommand(CanExecute = nameof(CanNudge))]
    private void NudgeLeft() => Timeline.Nudge(-1);

    [RelayCommand(CanExecute = nameof(CanNudge))]
    private void NudgeRight() => Timeline.Nudge(1);

    private bool CanNudge() => IsTimeline && Session.SelectedClips.Count > 0
        && Session.SelectionTrack != AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Transition && !Monitor.IsPlaying;

    [RelayCommand(CanExecute = nameof(HasAnySelection))]
    private async Task ClipPropertiesAsync()
    {
        ClipPropertiesViewModel? vm = null;
        if (ContentsActive && Contents.Selection.FirstOrDefault() is { Media: { } m } s)
        {
            vm = ClipPropertiesViewModel.For(m, s.Clip);
        }
        else if (Session.SelectedClips.FirstOrDefault() is var id && id != Guid.Empty)
        {
            if (Project.VideoTrack.FirstOrDefault(c => c.Id == id) is { } v && Project.FindMedia(v.MediaId) is { } vm1)
            {
                vm = ClipPropertiesViewModel.For(vm1, vm1.Clip(v.SourceClipId), v.Length);
            }
            else if (Project.AudioMusicTrack.FirstOrDefault(a => a.Id == id) is { } a && Project.FindMedia(a.MediaId) is { } am)
            {
                vm = ClipPropertiesViewModel.For(am, am.Clip(a.SourceClipId), a.Length);
            }
        }

        if (vm is not null)
        {
            await _dialogs.ShowClipPropertiesAsync(vm);
        }
    }

    [RelayCommand]
    private void PlayPauseClip()
    {
        if (ContentsActive && Monitor.Target == PreviewTarget.Project && Contents.SelectedItems.FirstOrDefault() is { } item)
        {
            Monitor.ShowItem(item.Media, item.Clip, play: true);
            return;
        }

        Monitor.PlayPauseCommand.Execute(null);
    }

    partial void OnIsBusyChanged(bool value) => Monitor.IsBusy = value;

    public void Dispose()
    {
        _recoveryTimer?.Dispose();

        DeleteRecovery();
        _store.Save();
    }
}
