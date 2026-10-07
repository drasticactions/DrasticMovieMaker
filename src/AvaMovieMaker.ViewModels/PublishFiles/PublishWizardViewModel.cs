using System.Globalization;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Effects;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Export;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Publish;

public sealed partial class PublishWizardViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly EngineHost _engine;
    private readonly IFileDialogs _files;
    public const string InvalidNameChars = "<>*?/\\|;+=[]()\":\0";

    public const int MaxNameLength = 64;
    private const int MaxStemLength = 58;

    private readonly IDialogService _dialogs;
    private readonly IMessageBoxes _messages;
    private readonly IDispatcher _dispatcher;
    private readonly Settings.AppSettings _settings;
    private readonly string _defaultName;
    private CancellationTokenSource? _cancel;

    private bool _canceling;

    private string? _proposed;

    public PublishWizardViewModel(ProjectSession session, EngineHost engine, IFileDialogs files, IDialogService dialogs, IMessageBoxes messages, IDispatcher dispatcher, Settings.AppSettings settings)
    {
        _session = session;
        _engine = engine;
        _files = files;
        _dialogs = dialogs;
        _messages = messages;
        _dispatcher = dispatcher;
        _settings = settings;
        _defaultName = DefaultName(session.Project);
        FileName = _defaultName;
        PlayWhenFinished = settings.PlayPublishedMovie;
        Folder = string.IsNullOrEmpty(settings.LastPublishFolder) ? AppPaths.VideosDir : settings.LastPublishFolder;
        string initial = Folder;
        AddFolder(AppPaths.VideosDir, Strings.FolderVideos);
        AddFolder(Path.Combine(AppPaths.HomeDir, "Desktop"), Strings.FolderDesktop);
        AddFolder(AppPaths.HomeDir, Strings.FolderHome);
        AddFolder(initial);
        Profiles = PublishProfiles.All.Where(p => p.IsAvailableFor(session.Project.Settings.Aspect)).Select(p => new ProfileOption(p, MovieEncoder.MissingEncoder(p.Encoder(session.Project.Settings, false, new Dictionary<string, string>())))).ToList();
        MoreProfiles = Profiles.Where(p => p.Profile != PublishProfiles.Recommended).ToList();
        SelectedProfile = MoreProfiles.FirstOrDefault(p => p.IsAvailable) ?? Profiles[0];
        Page = PublishPage.Where;
        UseHardwareEncode = settings.HardwareEncode;
    }

    public IReadOnlyList<ProfileOption> Profiles { get; }

    public IReadOnlyList<ProfileOption> MoreProfiles { get; }

    public IReadOnlyList<(string Name, string Description)> Destinations { get; } =
    [
        (Strings.TaskThisComputer, Strings.PublishWizardPublishForPlaybackOnYourComputer),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(IsBest), nameof(IsCompress), nameof(IsMore), nameof(EffectiveProfile))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial PublishMode Mode { get; set; }

    public bool IsBest
    {
        get => Mode == PublishMode.Best;
        set { if (value) Mode = PublishMode.Best; }
    }

    public bool IsCompress
    {
        get => Mode == PublishMode.Compress;
        set { if (value) Mode = PublishMode.Compress; }
    }

    public bool IsMore
    {
        get => Mode == PublishMode.More;
        set { if (value) Mode = PublishMode.More; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(EffectiveProfile))]
    public partial decimal CompressMegabytes { get; set; } = 10;

    public PublishProfile EffectiveProfile => Mode switch
    {
        PublishMode.Compress => PublishProfile.CompressTo((long)(CompressMegabytes * (CompressUnit == "KB" ? 1024 : 1024 * 1024)), _session.Project.Duration),
        PublishMode.More => SelectedProfile.Profile,
        _ => PublishProfiles.Recommended,
    };

    public string MovieSettingsText => string.Join(Environment.NewLine, Details.Take(5).Select(d => string.Format(CultureInfo.CurrentCulture, Strings.LabelAndValue, d.Label, d.Value)));

    public string FileSizeText
    {
        get
        {
            IEnumerable<(string Label, string Value)> rows = Details.Skip(5);
            string sizes = string.Join(Environment.NewLine + Environment.NewLine, rows.Skip(Mode == PublishMode.Best ? 1 : 0).Select(d => string.Format(CultureInfo.CurrentCulture, Strings.LabelAndValueBelow, d.Label, d.Value).ReplaceLineEndings()));

            return Mode == PublishMode.Best
                ? Strings.PublishSizeVaries + Environment.NewLine + Environment.NewLine + sizes
                : sizes;
        }
    }

    public IReadOnlyList<string> CompressUnits { get; } = ["MB", "KB"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(EffectiveProfile))]
    public partial string CompressUnit { get; set; } = "MB";

    partial void OnCompressUnitChanged(string value) => NotifyBoxes();

    public System.Collections.ObjectModel.ObservableCollection<PublishFolder> Folders { get; } = [];

    [ObservableProperty]
    public partial PublishFolder? SelectedFolder { get; set; }

    partial void OnSelectedFolderChanged(PublishFolder? value)
    {
        if (value is not null)
        {
            Folder = value.Path;
        }
    }

    private void AddFolder(string path, string? name = null)
    {
        PublishFolder? existing = Folders.FirstOrDefault(f => string.Equals(Path.TrimEndingDirectorySeparator(f.Path), Path.TrimEndingDirectorySeparator(path), StringComparison.Ordinal));
        if (existing is null)
        {
            existing = new PublishFolder(name ?? (Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } n ? n : path), path);
            Folders.Add(existing);
        }

        SelectedFolder = existing;
    }

    public bool CanGoBack => Page is PublishPage.Name or PublishPage.Settings;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(BackCommand))]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(NextText), nameof(CanGoBack), nameof(IsWherePage), nameof(IsNamePage), nameof(IsSettingsPage), nameof(IsProgressPage), nameof(IsFinishPage))]
    public partial PublishPage Page { get; set; }

    public bool IsWherePage => Page == PublishPage.Where;

    public bool IsNamePage => Page == PublishPage.Name;

    public bool IsSettingsPage => Page == PublishPage.Settings;

    public bool IsProgressPage => Page == PublishPage.Progress;

    public bool IsFinishPage => Page == PublishPage.Finish;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(NameError))]
    public partial string FileName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial string Folder { get; set; }

    public string? NameError => FileName.IndexOfAny(InvalidNameChars.ToCharArray()) >= 0 ? Strings.PublishNameInvalid : null;

    partial void OnPageChanged(PublishPage value)
    {
        if (value == PublishPage.Name && (_proposed is null || FileName == _proposed))
        {
            ProposeName(_defaultName);
        }
    }

    partial void OnFolderChanged(string value)
    {
        if (Page == PublishPage.Name && _proposed is not null && FileName == _proposed)
        {
            ProposeName(_defaultName);
        }
    }

    public static string DefaultName(Project project)
    {
        string name = CleanName(project.Properties.Title);
        if (name.Length == 0 && project.FilePath is not null)
        {
            name = CleanName(project.DisplayName);
        }

        return name.Length == 0 ? Strings.DefaultMovieName : name;
    }

    public static string CleanName(string name)
    {
        string clean = new string(name.Where(c => !InvalidNameChars.Contains(c, StringComparison.Ordinal)).ToArray()).Trim();
        return clean.Length > MaxNameLength ? clean[..MaxNameLength].TrimEnd() : clean;
    }

    private static bool NameTaken(string folder, string name)
    {
        try
        {
            return FileStore.Current.EnumerateFiles(folder).Any(f => Path.GetFileNameWithoutExtension(f) == name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private void ProposeName(string name)
    {
        if (!NameTaken(Folder, name))
        {
            FileName = _proposed = name;
            return;
        }

        string stem = name.Length > 5 && name[^5] == '_' ? name[..^5] : name;
        if (stem.Length > MaxStemLength)
        {
            stem = stem[..MaxStemLength];
        }

        int low = 1, high = 9999;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (NameTaken(Folder, Numbered(stem, mid)))
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (low > 9999)
        {
            FileName = _proposed = name;
            _ = _messages.ShowAsync(new MessageRequest(Strings.PublishNameExhausted, MessageButtons.Ok, MessageIcon.Warning));
            return;
        }

        FileName = _proposed = Numbered(stem, low);
    }

    private static string Numbered(string stem, int n) => stem + "_" + n.ToString("D4", CultureInfo.InvariantCulture);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(EffectiveProfile))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial ProfileOption SelectedProfile { get; set; }

    partial void OnModeChanged(PublishMode value) => NotifyBoxes();

    partial void OnCompressMegabytesChanged(decimal value) => NotifyBoxes();

    partial void OnSelectedProfileChanged(ProfileOption value) => NotifyBoxes();

    private void NotifyBoxes()
    {
        OnPropertyChanged(nameof(MovieSettingsText));
        OnPropertyChanged(nameof(FileSizeText));
    }

    [ObservableProperty]
    public partial bool UseHardwareEncode { get; set; }

    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial bool PlayWhenFinished { get; set; }

    public string Heading => Page switch
    {
        PublishPage.Where => Strings.PublishHeadingWhere,
        PublishPage.Name => Strings.PublishHeadingName,
        PublishPage.Settings => Strings.PublishHeadingSettings,
        PublishPage.Progress => Strings.PublishHeadingProgress,
        _ => Error is null ? Strings.PublishHeadingDone : Strings.PublishHeadingFailed,
    };

    public string NextText => Page switch
    {
        PublishPage.Settings => Strings.ButtonPublish,
        PublishPage.Finish => Strings.ButtonFinish,
        _ => Strings.ButtonNext,
    };

    public string OutputPath => Path.Combine(Folder, FileName + EffectiveProfile.Container switch
    {
        ContainerFormat.WebM => ".webm",
        _ => ".mp4",
    });

    public IReadOnlyList<(string Label, string Value)> Details
    {
        get
        {
            PublishProfile p = EffectiveProfile;
            ProjectSettings ps = _session.Project.Settings;
            (int w, int h, Rational par) = p.Size(ps, LargestSource());
            MediaTime d = _session.Project.Duration;
            long bytes = p.EstimateBytes(d);
            string free = FileStore.Current.AvailableFreeSpace(Folder) is { } available ? FormatBytes(available) : Strings.Unknown;

            return
            [
                (Strings.PublishFileType, p.FileTypeName),
                (Strings.PublishBitRate, Mode == PublishMode.Best ? Strings.VariableBitRate : PublishProfile.FormatBitrate(p.EstimatedVideoBitrate + p.AudioBitrate)),
                (Strings.PublishDisplaySize, string.Format(CultureInfo.CurrentCulture, Strings.DisplaySize, w, h)),
                (Strings.PublishAspectRatio, AspectRatios.Label(ps.Aspect)),
                (Strings.PublishFramesPerSecond, Math.Round(ps.FrameRate.ToDouble()).ToString(CultureInfo.CurrentCulture)),
                (Strings.PublishSpaceRequired, FormatBytes(bytes)),
                (Strings.PublishSpaceAvailable, free),
            ];
        }
    }

    private (int, int)? LargestSource() =>
        _session.Project.Media.Where(m => m.Video is not null && m.Kind == MediaKind.Video)
            .Select(m => m.Video!.DisplaySize).OrderByDescending(s => s.Width * s.Height).Cast<(int, int)?>().FirstOrDefault();

    private static string FormatBytes(long b) =>
        string.Format(CultureInfo.CurrentCulture, b >= 1 << 30 ? Strings.SizeGigabytes : Strings.SizeMegabytes, b / (double)(b >= 1 << 30 ? 1 << 30 : 1 << 20));

    public event EventHandler? Closed;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _files.PickFolderAsync(Strings.PickPublishFolder, Folder) is { } f)
        {
            AddFolder(f);
        }
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private async Task NextAsync()
    {
        switch (Page)
        {
            case PublishPage.Where:
                Page = PublishPage.Name;
                break;
            case PublishPage.Name:
                Page = PublishPage.Settings;
                OnPropertyChanged(nameof(Details));
                NotifyBoxes();
                break;
            case PublishPage.Settings:
                await PublishAsync();
                break;
            case PublishPage.Finish:
                _settings.PlayPublishedMovie = PlayWhenFinished;
                if (Error is null && PlayWhenFinished)
                {
                    _dialogs.PlayMovie(OutputPath);
                }

                Closed?.Invoke(this, EventArgs.Empty);

                break;
        }
    }

    private bool CanNext() => Page switch
    {
        PublishPage.Name => !string.IsNullOrWhiteSpace(FileName) && NameError is null && !string.IsNullOrWhiteSpace(Folder),
        PublishPage.Progress => false,
        PublishPage.Settings => Mode != PublishMode.More || SelectedProfile.IsAvailable,
        _ => true,
    };

    [RelayCommand(CanExecute = nameof(CanBack))]
    private void Back() => Page = Page == PublishPage.Settings ? PublishPage.Name : PublishPage.Where;

    private bool CanBack() => CanGoBack;

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (Page != PublishPage.Progress)
        {
            Closed?.Invoke(this, EventArgs.Empty);
            return;
        }

        MessageResult r = await _messages.ShowAsync(new MessageRequest(Strings.PublishCancelPrompt, MessageButtons.YesNo, MessageIcon.Question));
        if (r == MessageResult.Yes && Page == PublishPage.Progress && _cancel is { } cancel)
        {
            _canceling = true;
            ProgressText = Strings.PublishCanceling;
            await cancel.CancelAsync();
        }
    }

    private async Task<bool> CanPublishAsync()
    {
        Project project = _session.Project;
        if (project.IsEmpty)
        {
            await _messages.ShowAsync(new MessageRequest(Strings.PublishEmpty, MessageButtons.Ok, MessageIcon.Information));
            return false;
        }

        var used = project.VideoTrack.Select(c => c.MediaId).Concat(project.AudioMusicTrack.Select(a => a.MediaId)).ToHashSet();
        if (project.Media.Any(m => m.Missing && used.Contains(m.Id)))
        {
            await _messages.ShowAsync(new MessageRequest(Strings.PublishMissingSources, MessageButtons.Ok, MessageIcon.Error) { Title = Strings.PublishCannotComplete });
            return false;
        }

        if (FileStore.Current.Exists(OutputPath))
        {
            MessageResult r = await _messages.ShowAsync(new MessageRequest(
                string.Format(CultureInfo.CurrentCulture, Strings.PublishReplacePrompt, Path.GetFileName(OutputPath)), MessageButtons.YesNo, MessageIcon.Warning));
            return r == MessageResult.Yes;
        }

        return true;
    }

    private async Task PublishAsync()
    {
        if (!await CanPublishAsync())
        {
            return;
        }

        Page = PublishPage.Progress;
        Error = null;
        _canceling = false;
        _settings.LastPublishFolder = Folder;
        _settings.HardwareEncode = UseHardwareEncode;
        FileStore.Current.CreateDirectory(Folder);
        ProjectProperties props = _session.Project.Properties;
        var meta = _settings.OmitPublishedMetadata ? new Dictionary<string, string>() : new Dictionary<string, string>
        {
            ["title"] = string.IsNullOrEmpty(props.Title) ? FileName : props.Title,
            ["artist"] = props.Author,
            ["copyright"] = props.Copyright,
            ["comment"] = props.Description,
        };
        EncoderSettings enc = EffectiveProfile.Encoder(_session.Project.Settings, UseHardwareEncode, meta, LargestSource());
        _cancel = new CancellationTokenSource();
        var progress = new Progress<ExportProgress>(p =>
        {
            Progress = p.Fraction * 100;
            if (!_canceling)
            {
                ProgressText = string.Format(CultureInfo.CurrentCulture, Strings.PublishProgress, p.Fraction, FormatRemaining(p.Remaining));
            }
        });
        string path = OutputPath;
        _engine.Playback.Pause();
        try
        {
            await MovieExporter.ExportAsync(_engine.Device, EffectLibrary.Instance, _engine.Frames, _session.Plan, enc, path, progress, _cancel.Token);
        }
        catch (OperationCanceledException)
        {
            Page = PublishPage.Settings;
            return;
        }
        catch (Exception e)
        {
            Log.Error("publish", "Publish failed", e);
            Error = e.Message;
        }

        Page = PublishPage.Finish;
        OnPropertyChanged(nameof(Heading));
    }

    private static string FormatRemaining(TimeSpan t) => t.TotalMinutes >= 1
        ? string.Format(CultureInfo.CurrentCulture, Strings.DurationMinutesSeconds, (int)t.TotalMinutes, t.Seconds)
        : string.Format(CultureInfo.CurrentCulture, Strings.DurationSeconds, t.Seconds);
}
