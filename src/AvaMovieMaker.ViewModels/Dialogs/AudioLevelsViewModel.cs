using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed partial class AudioLevelsViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private bool _syncing;

    public AudioLevelsViewModel(ProjectSession session)
    {
        _session = session;
        Levels = session.Project.AudioLevels;
        session.Changed += (_, _) =>
        {
            _syncing = true;
            Levels = _session.Project.AudioLevels;
            _syncing = false;
        };
    }

    [ObservableProperty]
    public partial double Levels { get; set; }

    partial void OnLevelsChanged(double value)
    {
        if (!_syncing)
        {
            _session.Editor.SetAudioLevels(value);
        }
    }
}
