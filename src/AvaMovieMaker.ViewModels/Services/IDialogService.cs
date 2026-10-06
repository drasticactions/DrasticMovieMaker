using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Publish;

namespace AvaMovieMaker.ViewModels.Services;

public interface IDialogService
{
    Task<bool> ShowOptionsAsync(OptionsViewModel options);

    Task<bool> ShowProjectPropertiesAsync(ProjectPropertiesViewModel properties);

    Task ShowClipPropertiesAsync(ClipPropertiesViewModel properties);

    Task<bool> ShowClipVolumeAsync(ClipVolumeViewModel volume);

    Task<bool> ShowEffectsAsync(ClipEffectsViewModel effects);

    Task ShowPublishWizardAsync(PublishWizardViewModel wizard);

    Task ShowAboutAsync();

    Task ShowProgressAsync(Dialogs.ProgressViewModel progress);

    void ShowAudioLevels(AudioLevelsViewModel levels);

    void ShowFullScreen(Preview.MonitorViewModel monitor);

    Task<uint?> PickColorAsync(uint initial, Action<uint> changed);

    void PlayMovie(string path);

    void ApplyTheme(string theme);
}
