using AvaMovieMaker.Assets;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Settings;
using AvaMovieMaker.Time;
using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Publish;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.ViewModels.Tests;

internal sealed class MemorySettingsStore(AppSettings settings) : ISettingsStore
{
    public AppSettings Settings { get; } = settings;

    public int Saves { get; private set; }

    public void Save() => Saves++;
}

internal sealed class InlineDispatcher : IDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action action) => action();
}

internal sealed class FakeFileDialogs : IFileDialogs
{
    public IReadOnlyList<string> ImportResult { get; set; } = [];

    public List<(MediaFilter Filter, string? StartFolder)> ImportRequests { get; } = [];

    public Task<IReadOnlyList<string>> ImportMediaAsync(MediaFilter filter, string? startFolder)
    {
        ImportRequests.Add((filter, startFolder));
        return Task.FromResult(ImportResult);
    }

    public Task<string?> OpenProjectAsync(string? startFolder) => Task.FromResult<string?>(null);

    public Task<string?> OpenMswmmAsync(string? startFolder) => Task.FromResult<string?>(null);

    public Task<string?> SaveProjectAsync(string suggestedName, string? startFolder) => Task.FromResult<string?>(null);

    public Func<string, string?, string?> PackageSave { get; set; } = (_, _) => null;

    public Task<string?> SavePackageAsync(string suggestedName, string? startFolder) => Task.FromResult(PackageSave(suggestedName, startFolder));

    public string? PackageFolder { get; set; }

    public Task<string?> PackageMediaFolderAsync(string projectName) => Task.FromResult(PackageFolder);

    public Func<string, string?, string?> PictureSave { get; set; } = (_, _) => null;

    public Task<string?> SavePictureAsync(string suggestedName, string? startFolder) => Task.FromResult(PictureSave(suggestedName, startFolder));

    public Func<string, string?, string?> NarrationSave { get; set; } = (_, _) => null;

    public Task<string?> SaveNarrationAsync(string suggestedName, string? startFolder) => Task.FromResult(NarrationSave(suggestedName, startFolder));

    public Task<string?> PickFolderAsync(string title, string? startFolder) => Task.FromResult<string?>(null);

    public Task<string?> OpenFileAsync(string title, string? startFolder) => Task.FromResult<string?>(null);
}

internal sealed class FakeMessageBoxes : IMessageBoxes
{
    public List<MessageRequest> Shown { get; } = [];

    public MessageResult Answer { get; set; } = MessageResult.Ok;

    public Task<MessageResult> ShowAsync(MessageRequest request)
    {
        Shown.Add(request);
        return Task.FromResult(Answer);
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public bool Result { get; set; }

    public Action<object>? OnShow { get; set; }

    public List<object> Shown { get; } = [];

    private Task<bool> Show(object vm)
    {
        Shown.Add(vm);
        OnShow?.Invoke(vm);
        return Task.FromResult(Result);
    }

    public Task<bool> ShowOptionsAsync(OptionsViewModel options) => Show(options);

    public Task<bool> ShowProjectPropertiesAsync(ProjectPropertiesViewModel properties) => Show(properties);

    public Task ShowClipPropertiesAsync(ClipPropertiesViewModel properties) => Show(properties);

    public Task<bool> ShowClipVolumeAsync(ClipVolumeViewModel volume) => Show(volume);

    public Task<bool> ShowEffectsAsync(ClipEffectsViewModel effects) => Show(effects);

    public Task ShowPublishWizardAsync(PublishWizardViewModel wizard) => Show(wizard);

    public Task ShowAboutAsync() => Show("about");

    public List<ViewModels.Dialogs.ProgressViewModel> Progress { get; } = [];

    public Task ShowProgressAsync(ViewModels.Dialogs.ProgressViewModel progress)

    {

        Progress.Add(progress);

        return Task.CompletedTask;

    }

    public void ShowAudioLevels(AudioLevelsViewModel levels) => Shown.Add(levels);

    public void ShowFullScreen(MonitorViewModel monitor) => Shown.Add(monitor);

    public Func<uint, Action<uint>, uint?> PickColor { get; set; } = (_, _) => null;

    public Task<uint?> PickColorAsync(uint initial, Action<uint> changed) => Task.FromResult(PickColor(initial, changed));

    public void PlayMovie(string path) => Shown.Add(path);

    public void ApplyTheme(string theme) => Shown.Add("theme:" + theme);
}

internal sealed class Harness : IDisposable
{
    private static readonly Lock FontGate = new();
    private static bool _fontsRegistered;

    public Harness(AppSettings? settings = null)
    {
        RegisterFonts();
        Store = new MemorySettingsStore(settings ?? new AppSettings { AutoRecoveryEnabled = false });
        Engine = new EngineHost(preferGpu: false, hardwareDecode: false, withAudio: false);
        Shell = new ShellViewModel(Engine, Files, Messages, Dialogs, Dispatcher, Store, new ProjectClipboard());
    }

    public MemorySettingsStore Store { get; }

    public FakeFileDialogs Files { get; } = new();

    public FakeMessageBoxes Messages { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public InlineDispatcher Dispatcher { get; } = new();

    public EngineHost Engine { get; }

    public ShellViewModel Shell { get; }

    public ProjectSession Session => Shell.Session;

    public Guid AddTitle(string text)
    {
        Session.Editor.AddTitle(new TitleContent { Lines = [text] }, null, MediaTime.Zero);
        return Session.Project.VideoTrack[0].Id;
    }

    public static void RegisterFonts()
    {
        lock (FontGate)
        {
            if (_fontsRegistered)
            {
                return;
            }

            foreach (Stream s in AssetFonts.OpenAll())
            {
                using (s)
                {
                    TitleFonts.Register(s);
                }
            }

            _fontsRegistered = true;
        }
    }

    public static void RequireFFmpeg()
    {
        if (FFmpegRuntime.Check() is { } error)
        {
            Assert.Fail(error);
        }
    }

    public void Dispose()
    {
        Shell.Dispose();
        Engine.Dispose();
    }
}
