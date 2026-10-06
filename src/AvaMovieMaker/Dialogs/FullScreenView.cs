using System.Windows.Input;
using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaMovieMaker.Controls;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Dialogs;

public sealed class FullScreenView : UserControl
{
    private readonly MonitorViewModel _monitor;
    private readonly IPreviewSurface? _previous;
    private bool _detached;

    public FullScreenView(MonitorViewModel monitor)
    {
        _monitor = monitor;
        _previous = monitor.Surface;
        Background = Brushes.Black;
        Focusable = true;
        Avalonia.Automation.AutomationProperties.SetName(this, Strings.FullScreenName);
        Cursor = new Cursor(StandardCursorType.None);
        var preview = new PreviewImage { Aspect = monitor.DisplayAspect };
        preview.PixelSizeChanged += (w, h) => monitor.Resize(w, h);
        Content = preview;
        monitor.Surface = preview;
        monitor.SourceChanged += OnSourceChanged;
        PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Leave?.Invoke(this, EventArgs.Empty);
        };
        KeyDown += OnKeyDown;
    }

    public event EventHandler? Leave;

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _monitor.SourceChanged -= OnSourceChanged;
        _monitor.Surface = _previous;
        if (_previous is PreviewImage p)
        {
            p.ReportSize();
        }

        _ = _monitor.Playback.RenderAsync(_monitor.Playback.Position);
    }

    private void OnSourceChanged(object? sender, EventArgs e) => Leave?.Invoke(this, EventArgs.Empty);

    public static ICommand? CommandFor(MonitorViewModel monitor, Key key, KeyModifiers mods, out bool leave)
    {
        leave = false;
        mods = Menus.CommandKeys.ToLogical(mods);
        bool commandKey = Menus.CommandKeys.Command == KeyModifiers.Meta ? key is Key.LWin or Key.RWin : key is Key.LeftCtrl or Key.RightCtrl;
        if (commandKey || (key is Key.LeftAlt or Key.RightAlt && mods.HasFlag(KeyModifiers.Control)))
        {
            return null;
        }

        bool project = monitor.Target == PreviewTarget.Project;
        ICommand? cmd = (key, mods) switch
        {
            (Key.Space or Key.K, KeyModifiers.None) => monitor.PlayPauseCommand,
            (Key.J, KeyModifiers.None) => monitor.PreviousFrameCommand,
            (Key.L, KeyModifiers.None) => monitor.NextFrameCommand,
            (Key.Left, KeyModifiers.Control | KeyModifiers.Alt) => monitor.BackCommand,
            (Key.Right, KeyModifiers.Control | KeyModifiers.Alt) => monitor.ForwardCommand,
            (Key.W, KeyModifiers.Control) when project => monitor.PlayProjectCommand,
            (Key.Q, KeyModifiers.Control) when project => monitor.RewindCommand,
            _ => null,
        };
        leave = cmd is null;
        return cmd;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e) => HandleKey(e);

    public void HandleKey(KeyEventArgs e)
    {
        ICommand? cmd = CommandFor(_monitor, e.Key, e.KeyModifiers, out bool leave);
        if (leave)
        {
            Leave?.Invoke(this, EventArgs.Empty);
        }
        else if (cmd is not null && cmd.CanExecute(null))
        {
            cmd.Execute(null);
        }

        e.Handled = true;
    }
}

public interface IFullScreenHost
{
    void Show(FullScreenView view, Visual owner);
}

public sealed class FullScreenWindowHost : IFullScreenHost
{
    public void Show(FullScreenView view, Visual owner)
    {
        var window = new Window
        {
            Title = Strings.FullScreenWindowTitle,
            Background = Brushes.Black,
            WindowDecorations = WindowDecorations.None,
            WindowState = WindowState.FullScreen,
            Content = view,
        };
        view.Leave += (_, _) => window.Close();
        window.Closed += (_, _) => view.Detach();
        window.Opened += (_, _) => view.Focus();
        if (TopLevel.GetTopLevel(owner) is Window ownerWindow)
        {
            window.Show(ownerWindow);
        }
        else
        {
            window.Show();
        }
    }
}
