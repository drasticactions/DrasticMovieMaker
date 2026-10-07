using AvaMovieMaker.Audio;
using AvaMovieMaker.Settings;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed partial class OptionsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public OptionsViewModel(AppSettings settings, ProjectSettings project)
    {
        _settings = settings;
        TemporaryFolder = string.IsNullOrEmpty(settings.TemporaryFolder) ? Path.GetTempPath() : settings.TemporaryFolder;
        OpenLastProject = settings.OpenLastProjectOnStartup;
        OmitMetadata = settings.OmitPublishedMetadata;
        AutoRecovery = settings.AutoRecoveryEnabled;
        AutoRecoveryMinutes = settings.AutoRecoveryMinutes;
        CreateClips = settings.CreateClipsOnImport;
        PictureSeconds = Math.Clamp(settings.PictureDurationSeconds, ProjectSettings.MinPicture.Seconds, ProjectSettings.MaxPicture.Seconds);
        TransitionSeconds = Math.Clamp(settings.TransitionDurationSeconds, ProjectSettings.MinTransition.Seconds, ProjectSettings.MaxTransition.Seconds);
        IsPal = project.Format == VideoFormat.Pal;
        Aspect = project.Aspect;
        HardwareDecode = settings.HardwareDecode;
        HardwareEncode = settings.HardwareEncode;
        PlaybackDevices = [Strings.DefaultDevice, .. AudioSystem.PlaybackDevices.Select(d => d.Name)];
        PlaybackDevice = string.IsNullOrEmpty(settings.PlaybackDevice) ? PlaybackDevices[0] : settings.PlaybackDevice;
    }

    [ObservableProperty]
    public partial bool OmitMetadata { get; set; }

    [ObservableProperty]
    public partial string TemporaryFolder { get; set; }

    [ObservableProperty]
    public partial bool OpenLastProject { get; set; }

    [ObservableProperty]
    public partial bool AutoRecovery { get; set; }

    [ObservableProperty]
    public partial int AutoRecoveryMinutes { get; set; }

    [ObservableProperty]
    public partial bool CreateClips { get; set; }

    [ObservableProperty]
    public partial double PictureSeconds { get; set; }

    [ObservableProperty]
    public partial double TransitionSeconds { get; set; }

    [ObservableProperty]
    public partial bool IsPal { get; set; }

    [ObservableProperty]
    public partial AspectRatio Aspect { get; set; }

    public IReadOnlyList<AspectOption> Aspects => AspectOption.All;

    public AspectOption SelectedAspect
    {
        get => AspectOption.For(Aspect);
        set
        {
            if (value is not null)
            {
                Aspect = value.Value;
            }
        }
    }

    partial void OnAspectChanged(AspectRatio value) => OnPropertyChanged(nameof(SelectedAspect));

    [ObservableProperty]
    public partial bool HardwareDecode { get; set; }

    [ObservableProperty]
    public partial bool HardwareEncode { get; set; }

    [ObservableProperty]
    public partial string PlaybackDevice { get; set; }

    public IReadOnlyList<string> PlaybackDevices { get; }

    public bool WarningsReset { get; private set; }

    [RelayCommand]
    private void ResetWarnings() => WarningsReset = true;

    [RelayCommand]
    private void RestoreDefaults()
    {
        TemporaryFolder = Path.GetTempPath();
        OpenLastProject = false;
        AutoRecovery = true;
        AutoRecoveryMinutes = 10;
        OmitMetadata = false;
        HardwareDecode = true;
        HardwareEncode = false;
        PlaybackDevice = PlaybackDevices[0];
        PictureSeconds = 5;
        TransitionSeconds = 1.25;
        IsPal = false;
        Aspect = AspectRatio.Standard4x3;
    }

    public ProjectSettings Apply(ProjectSettings current)
    {
        _settings.OmitPublishedMetadata = OmitMetadata;
        _settings.TemporaryFolder = Path.TrimEndingDirectorySeparator(TemporaryFolder) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) ? string.Empty : TemporaryFolder;
        _settings.OpenLastProjectOnStartup = OpenLastProject;
        _settings.AutoRecoveryEnabled = AutoRecovery;
        _settings.AutoRecoveryMinutes = Math.Clamp(AutoRecoveryMinutes, 1, 60);
        _settings.CreateClipsOnImport = CreateClips;
        _settings.HardwareDecode = HardwareDecode;
        _settings.HardwareEncode = HardwareEncode;
        _settings.PlaybackDevice = PlaybackDevice == PlaybackDevices[0] ? string.Empty : PlaybackDevice;
        if (WarningsReset)
        {
            _settings.DismissedWarnings.Clear();
        }

        ProjectSettings next = (current with
        {
            PictureDuration = MediaTime.FromSeconds(PictureSeconds),
            TransitionDuration = MediaTime.FromSeconds(TransitionSeconds),
            Format = IsPal ? VideoFormat.Pal : VideoFormat.Ntsc,
            Aspect = Aspect,
        }).Clamped();
        _settings.PictureDurationSeconds = next.PictureDuration.Seconds;
        _settings.TransitionDurationSeconds = next.TransitionDuration.Seconds;
        _settings.PalVideo = IsPal;
        _settings.DefaultAspect = AspectRatios.Id(Aspect);
        return next;
    }

    public static ProjectSettings ForProject(AppSettings settings, ProjectSettings? file = null) => (new ProjectSettings
    {
        PictureDuration = MediaTime.FromSeconds(settings.PictureDurationSeconds),
        TransitionDuration = MediaTime.FromSeconds(settings.TransitionDurationSeconds),
        Format = file?.Format ?? (settings.PalVideo ? VideoFormat.Pal : VideoFormat.Ntsc),
        Aspect = file?.Aspect ?? DefaultAspect(settings),
    }).Clamped();

    public static AspectRatio DefaultAspect(AppSettings settings) =>
        AspectRatios.TryParse(settings.DefaultAspect, out AspectRatio aspect) ? aspect : AspectRatio.Standard4x3;
}
