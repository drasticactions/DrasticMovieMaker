using System.Diagnostics;
using AvaMovieMaker.ViewModels;
using Avalonia;
using AvaWpf;
using AvaMovieMaker.Dialogs;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Settings;
using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Publish;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Services;

internal sealed class DialogService(Visual owner, ISettingsStore store, IFullScreenHost fullScreen, IShellOpener shell) : IDialogService
{
    private IWindowHandle? _levels;

    public Func<string> Renderer { get; set; } = () => Strings.UnknownRenderer;

    public Task<bool> ShowOptionsAsync(OptionsViewModel options) =>
        WindowHost.ShowDialogAsync<bool>(new OptionsWindow { DataContext = options }, owner);

    public Task<bool> ShowProjectPropertiesAsync(ProjectPropertiesViewModel properties) =>
        WindowHost.ShowDialogAsync<bool>(new ProjectPropertiesWindow { DataContext = properties }, owner);

    public Task ShowClipPropertiesAsync(ClipPropertiesViewModel properties) =>
        WindowHost.ShowDialogAsync(new ClipPropertiesWindow(properties), owner);

    public Task<bool> ShowClipVolumeAsync(ClipVolumeViewModel volume) =>
        WindowHost.ShowDialogAsync<bool>(new ClipVolumeWindow { DataContext = volume }, owner);

    public Task<bool> ShowEffectsAsync(ClipEffectsViewModel effects) =>
        WindowHost.ShowDialogAsync<bool>(new EffectsWindow { DataContext = effects }, owner);

    public Task ShowPublishWizardAsync(PublishWizardViewModel wizard) =>
        WindowHost.ShowDialogAsync(new PublishWizardWindow { DataContext = wizard }, owner);

    public Task ShowProgressAsync(ProgressViewModel progress)
    {
        var content = new ProgressWindow { DataContext = progress };
        progress.Finished += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => WindowHost.CloseDialog(content));
        return WindowHost.ShowDialogAsync(content, owner);
    }

    public Task ShowAboutAsync() => WindowHost.ShowDialogAsync(new AboutWindow(Renderer()), owner);

    public void ShowAudioLevels(AudioLevelsViewModel levels)
    {
        if (_levels is { IsOpen: true })
        {
            _levels.Activate();
            return;
        }

        _levels = WindowHost.Show(new AudioLevelsWindow { DataContext = levels }, owner);
        _levels.Closed += (_, _) => _levels = null;
    }

    public void ShowFullScreen(MonitorViewModel monitor) => fullScreen.Show(new FullScreenView(monitor), owner);

    public async Task<uint?> PickColorAsync(uint initial, Action<uint> changed)
    {
        uint? picked = await WindowHost.ShowDialogAsync<uint?>(new ColorDialog(initial, store.Settings.CustomColors, changed), owner);
        store.Save();
        return picked;
    }

    public void PlayMovie(string path) => shell.Open(path);

    public void ApplyTheme(string theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = App.VariantFor(theme);
        }
    }
}

public interface IShellOpener
{
    void Open(string path);
}

public sealed class DesktopShellOpener : IShellOpener
{
    public void Open(string path)
    {
        try
        {
            Process.Start(StartInfo(path))?.Dispose();
        }
        catch (Exception e)
        {
            Log.Warn("dialogs", $"Could not open {path}: {e.Message}");
        }
    }

    public static ProcessStartInfo StartInfo(string path) =>
        OperatingSystem.IsWindows() ? new ProcessStartInfo(path) { UseShellExecute = true }
        : OperatingSystem.IsMacOS() ? new ProcessStartInfo("open") { ArgumentList = { path }, UseShellExecute = false }
        : new ProcessStartInfo("xdg-open") { ArgumentList = { path }, UseShellExecute = false };
}
