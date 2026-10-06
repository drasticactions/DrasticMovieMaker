using System.Globalization;
using AvaMovieMaker.Audio;
using AvaMovieMaker.Audio.Capture;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media;
using AvaMovieMaker.Settings;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Import;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Narration;

public sealed partial class NarrationViewModel : ObservableObject
{
    public const long MinimumFreeBytes = 40L * 1024 * 1024;

    private readonly ProjectSession _session;
    private readonly MonitorViewModel _monitor;
    private readonly IDispatcher _dispatcher;
    private readonly IMessageBoxes _messages;
    private readonly IFileDialogs _files;
    private readonly AppSettings _settings;
    private readonly Func<string, string, MediaTime?, INarrationRecording?> _record;
    private INarrationRecording? _recording;
    private MediaTime _startedAt;
    private Timer? _timer;
    private bool _stopping;

    public NarrationViewModel(ProjectSession session, MonitorViewModel monitor, IDispatcher dispatcher, IMessageBoxes messages,
        IFileDialogs files, AppSettings settings, IReadOnlyList<string>? devices = null,
        Func<string, string, MediaTime?, INarrationRecording?>? record = null)
    {
        _session = session;
        _monitor = monitor;
        _dispatcher = dispatcher;
        _messages = messages;
        _files = files;
        _settings = settings;
        _record = record ?? ((device, path, limit) => NarrationRecorder.Start(device, path, limit));
        Devices = devices ?? [.. AudioSystem.CaptureDevices.Select(d => d.Name)];
        Device = Devices.Contains(settings.CaptureDevice) ? settings.CaptureDevice : Devices.FirstOrDefault() ?? string.Empty;
        LimitToFreeSpace = settings.NarrationLimitToFreeSpace;
        ShowOptions = settings.NarrationShowOptions;
        InputLevel = Math.Clamp(settings.NarrationInputLevel, 0, 65535);
        _monitor.Playback.PositionChanged += OnPosition;
        _monitor.Playback.StateChanged += OnPlaybackState;
        Refresh();
    }

    public IReadOnlyList<string> Devices { get; }

    [ObservableProperty]
    public partial string Device { get; set; }

    [ObservableProperty]
    public partial bool MuteSpeakers { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool LimitToFreeSpace { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OptionsLinkText), nameof(PaneHeight))]
    public partial bool ShowOptions { get; set; }

    public double PaneHeight => ShowOptions ? 475 : 260;

    public string OptionsLinkText => ShowOptions ? Strings.HideOptions : Strings.ShowOptions;

    [ObservableProperty]
    public partial int InputLevel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsRecording { get; private set; }

    public bool IsIdle => !IsRecording;

    [ObservableProperty]
    public partial double Level { get; private set; }

    [ObservableProperty]
    public partial string Captured { get; private set; } = Hms(MediaTime.Zero);

    [ObservableProperty]
    public partial string AvailableTime { get; private set; } = "--:--:--";

    [ObservableProperty]
    public partial string? StartTip { get; private set; }

    public string Instructions => Strings.NarrationInstructions;

    public event EventHandler? Closed;

    public void Refresh()
    {
        MediaTime at = _monitor.ProjectPosition;
        StartTip = OnAudioClip(at) ? Strings.NarrationStartTip : null;
        AvailableTime = !LimitToFreeSpace || _session.Project.IsEmpty ? "--:--:--"
            : OnAudioClip(at) ? Hms(MediaTime.Zero)
            : NextClip(at) is { } next ? Hms(next.Start - at)
            : "--:--:--";
        StartCommand.NotifyCanExecuteChanged();
    }

    partial void OnLimitToFreeSpaceChanged(bool value)
    {
        _settings.NarrationLimitToFreeSpace = value;
        Refresh();
    }

    partial void OnShowOptionsChanged(bool value) => _settings.NarrationShowOptions = value;

    partial void OnDeviceChanged(string value) => _settings.CaptureDevice = value;

    partial void OnInputLevelChanged(int value)
    {
        _settings.NarrationInputLevel = value;
        if (_recording is { } r)
        {
            r.Gain = Gain(value);
        }
    }

    partial void OnMuteSpeakersChanged(bool value) => _monitor.Playback.Volume = value ? 0 : 1;

    [RelayCommand]
    private void ToggleOptions() => ShowOptions = !ShowOptions;

    private void OnPosition(MediaTime t) => _dispatcher.Post(() =>
    {
        if (!IsRecording)
        {
            Refresh();
        }
    });

    private void OnPlaybackState(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (IsRecording && !_stopping && !_session.Project.IsEmpty && _monitor.Playback.State != AvaMovieMaker.Timeline.Playback.PlaybackState.Playing)
        {
            StopCommand.Execute(null);
        }
    });

    private bool CanStart() => !IsRecording && Devices.Count > 0 && !OnAudioClip(_monitor.ProjectPosition);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        MediaTime at = _monitor.ProjectPosition;
        if (OnAudioClip(at))
        {
            return;
        }

        string temp = string.IsNullOrEmpty(_settings.TemporaryFolder) ? Path.GetTempPath() : _settings.TemporaryFolder;
        FileStore.Current.CreateDirectory(temp);
        if (FreeBytes(temp) is { } free && free <= MinimumFreeBytes)
        {
            string drive = Path.GetPathRoot(Path.GetFullPath(temp)) ?? temp;
            await _messages.ShowAsync(new MessageRequest(string.Format(CultureInfo.CurrentCulture, Strings.NarrationNoTempSpace, drive), MessageButtons.Ok, MessageIcon.Warning));
            return;
        }

        MediaTime? limit = LimitToFreeSpace && NextClip(at) is { } next ? next.Start - at : null;
        string path = FreeName(temp, "narration", ".flac", n => n.ToString(CultureInfo.InvariantCulture));
        _recording = _record(Device, path, limit);
        if (_recording is null)
        {
            await _messages.ShowAsync(new MessageRequest(Strings.NarrationNoHardware, MessageButtons.Ok, MessageIcon.Error));
            return;
        }

        _recording.Gain = Gain(InputLevel);
        _recording.LimitReached += (_, _) => _dispatcher.Post(() => StopCommand.Execute(null));
        _startedAt = at;
        IsRecording = true;

        _monitor.ShowProject();
        if (!_session.Project.IsEmpty && at < _session.Project.Duration)
        {
            _monitor.Playback.Seek(at);
            _monitor.Playback.Play();
        }

        _timer = new Timer(_ => _dispatcher.Post(Tick), null, 0, 500);
    }

    private void Tick()
    {
        if (_recording is { } r)
        {
            Level = r.Level;
            Captured = Hms(r.Duration);
        }
    }

    [RelayCommand(CanExecute = nameof(IsRecording))]
    private async Task StopAsync()
    {
        if (_recording is not { } rec || _stopping)
        {
            return;
        }

        _stopping = true;
        try
        {
            _timer?.Dispose();
            _timer = null;
            _monitor.Playback.Pause();
            MediaTime length = rec.Stop();
            rec.Dispose();
            _recording = null;
            IsRecording = false;
            Level = 0;
            Captured = Hms(length);
            await SaveAndPlaceAsync(rec.Path, length);
        }
        finally
        {
            _stopping = false;
            Refresh();
        }
    }

    private async Task SaveAndPlaceAsync(string temp, MediaTime length)
    {
        if (length <= MediaTime.Zero)
        {
            TryDelete(temp);
            return;
        }

        string folder = FileStore.Current.DirectoryExists(_settings.NarrationFolder) ? _settings.NarrationFolder : Path.Combine(IO.AppPaths.MusicDir, "Narration");
        FileStore.Current.CreateDirectory(folder);
        string baseName = _session.Project.DisplayName + Strings.NarrationSuffix;
        string suggested = FileStore.Current.Exists(Path.Combine(folder, baseName + ".flac"))
            ? Path.GetFileName(FreeName(folder, baseName + "_", ".flac", n => n.ToString("0000", CultureInfo.InvariantCulture), first: 1))
            : baseName + ".flac";

        string? target;
        while (true)
        {
            target = await _files.SaveNarrationAsync(suggested, folder);
            if (target is null)
            {
                TryDelete(temp);
                return;
            }

            if (!target.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
            {
                target += ".flac";
            }

            if (!FileStore.Current.Exists(target))
            {
                break;
            }

            await _messages.ShowAsync(new MessageRequest(Strings.NarrationFileExists, MessageButtons.Ok, MessageIcon.Warning));
            suggested = Path.GetFileName(target);
        }

        try
        {
            FileStore.Current.Move(temp, target);
            _settings.NarrationFolder = Path.GetDirectoryName(target) ?? string.Empty;
            MediaItem item = await Task.Run(() => MediaImporter.FromFile(target, false, _session.NextImportBatch));
            _session.Editor.AddNarration(item, _startedAt, LimitToFreeSpace);
        }
        catch (Exception e)
        {
            Log.Error("narration", "Could not add the narration", e);
            await _messages.ShowAsync(new MessageRequest(Strings.NarrationNotValid, MessageButtons.Ok, MessageIcon.Error));
        }
    }

    [RelayCommand]
    private async Task DoneAsync()
    {
        if (IsRecording)
        {
            await StopAsync();
        }

        _monitor.Playback.PositionChanged -= OnPosition;
        _monitor.Playback.StateChanged -= OnPlaybackState;
        if (MuteSpeakers)
        {
            _monitor.Playback.Volume = 1;
        }

        Closed?.Invoke(this, EventArgs.Empty);
    }

    private bool OnAudioClip(MediaTime at)
    {
        MediaTime pad = LimitToFreeSpace ? MediaTime.FromTimeBase(1, _session.Project.Settings.FrameRate.Invert()) : MediaTime.Zero;
        return _session.Project.AudioMusicTrack.Any(a => at >= a.Start - pad && at < a.End + pad);
    }

    private AudioClip? NextClip(MediaTime at) => _session.Project.AudioMusicTrack.Where(a => a.Start > at).MinBy(a => a.Start);

    private static float Gain(int level) => level / 32768f;

    internal static string Hms(MediaTime t)
    {
        long s = (long)Math.Max(0, t.Seconds);
        return $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
    }

    private static string FreeName(string folder, string stem, string ext, Func<int, string> number, int first = 0)
    {
        for (int n = first; n < first + 10000; n++)
        {
            string path = Path.Combine(folder, stem + number(n) + ext);
            if (!FileStore.Current.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(folder, stem + Guid.NewGuid().ToString("N") + ext);
    }

    private static long? FreeBytes(string folder) => FileStore.Current.AvailableFreeSpace(folder);

    private static void TryDelete(string path)
    {
        try
        {
            FileStore.Current.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
