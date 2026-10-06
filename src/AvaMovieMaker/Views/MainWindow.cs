using Avalonia.Controls;
using Avalonia.Input;
using AvaWpf;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Views;

public sealed class MainWindow : ThemeWindow
{
    private ShellViewModel? _shell;
    private bool _closeConfirmed;

    public MainWindow()
    {
        Width = ShellMetrics.WindowWidth;
        Height = ShellMetrics.WindowHeight;
        MinWidth = 640;
        MinHeight = 480;
        Title = ViewModels.Strings.AppName;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DragDrop.SetAllowDrop(this, true);
        DragGhost.Attach(this);
    }

    public ShellView? View { get; private set; }

    public void Attach(ShellViewModel shell)
    {
        _shell = shell;
        DataContext = shell;
        View = new ShellView { DataContext = shell };
        Content = View;
        Title = shell.WindowTitle;
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.WindowTitle))
            {
                Title = shell.WindowTitle;
            }
        };
        shell.CloseRequested += (_, _) =>
        {
            _closeConfirmed = true;
            Close();
        };
        Closing += async (_, e) =>
        {
            if (_closeConfirmed || _shell is null)
            {
                return;
            }

            e.Cancel = true;
            if (await _shell.ConfirmDiscardAsync())
            {
                _closeConfirmed = true;
                Close();
            }
        };
        Closed += (_, _) =>
        {
            _shell.Dispose();
            _shell.Engine.Dispose();
        };
    }
}
