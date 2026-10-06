using AvaMovieMaker.Timeline.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed partial class ClipVolumeViewModel : ObservableObject
{
    private readonly AudioSettings _original;

    public ClipVolumeViewModel(AudioSettings settings)
    {
        _original = settings;
        Volume = Math.Clamp(settings.Volume, 0, 1) * 100;
        Mute = settings.Mute;
    }

    [ObservableProperty]
    public partial double Volume { get; set; }

    [ObservableProperty]
    public partial bool Mute { get; set; }

    [RelayCommand]
    private void Reset()
    {
        Volume = 100;
        Mute = false;
    }

    public AudioSettings ToSettings() => _original with { Volume = Math.Clamp(Volume, 0, 100) / 100.0, Mute = Mute };
}
