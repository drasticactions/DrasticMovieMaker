using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AvaMovieMaker.ViewModels.Shell;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(MenuBase.ClosedEvent, (_, _) =>
        {
            if (Shell is { } s)
            {
                s.MenuPrompt = null;
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        DragDrop.SetAllowDrop(this, true);

        Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null
                && this.FindControl<Control>("ContentsPane")?.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.IsEffectivelyVisible) is { } list)
            {
                list.Focus();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ShellViewModel shell)
            {
                shell.ZoomToFitRequested += (_, _) => this.FindControl<TimelineView>("TimelineView")?.ZoomToFit();
                ShowMenus(shell);
            }
        };
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private void ShowMenus(ShellViewModel shell)
    {
        if (this.FindControl<Menu>("MainMenu") is not { } bar)
        {
            return;
        }

        App.Platform.MenuHost.Show(bar, Menus.ShellMenus.Build(shell));
    }

    private TopLevel? _keys;

    static ShellView()
    {
        MenuItem.IsSelectedProperty.Changed.AddClassHandler<MenuItem>((item, e) =>
        {
            if (item.GetLogicalAncestors().OfType<ShellView>().FirstOrDefault() is not { Shell: { } shell })
            {
                return;
            }

            if (e.NewValue is true)
            {
                shell.MenuPrompt = Menus.InWindowMenuHost.GetPrompt(item);
            }
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _keys = TopLevel.GetTopLevel(this);
        _keys?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        if (_keys is not null)
        {
            _keys.ScalingChanged += OnScalingChanged;
        }

        UpdateThumbnailSizes();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateThumbnailSizes();
    }

    private void OnScalingChanged(object? sender, EventArgs e) => UpdateThumbnailSizes();

    private void UpdateThumbnailSizes()
    {
        if (Shell is not { } s || _keys is null)
        {
            return;
        }

        double scale = _keys.RenderScaling;
        static int Px(double dips, double scale) => (int)Math.Ceiling(dips * scale);
        s.Storyboard.SetThumbnailSize(Px(Controls.StoryboardStrip.PictureWidth, scale), Px(Controls.StoryboardStrip.PictureHeight, scale));
        s.Contents.SetThumbnailSize(Px(ShellMetrics.ThumbnailWidth, scale), Px(ShellMetrics.ThumbnailHeight, scale));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_keys is not null)
        {
            _keys.ScalingChanged -= OnScalingChanged;
        }

        _keys?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _keys = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Shell is not { } s || IsInWindowOrPopup(e.Source))
        {
            return;
        }

        object? focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        bool typing = focused is TextBox;
        KeyModifiers m = Menus.CommandKeys.ToLogical(e.KeyModifiers);

        if (focused is AvaMovieMaker.Controls.TimelineControl timeline && s.IsTimeline)
        {
            bool done = (e.Key, m) switch
            {
                (Key.Left, KeyModifiers.Control | KeyModifiers.Alt) => timeline.ScrollBy(-1),
                (Key.Right, KeyModifiers.Control | KeyModifiers.Alt) => timeline.ScrollBy(1),
                (Key.Up or Key.Down, KeyModifiers.Control | KeyModifiers.Alt) => true,
                (Key.Add, KeyModifiers.None) => Set(s.Timeline, true),
                (Key.Subtract, KeyModifiers.None) => Set(s.Timeline, false),
                _ => false,
            };
            if (done)
            {
                e.Handled = true;
                return;
            }
        }

        if (focused is AvaMovieMaker.Controls.TimelineControl or AvaMovieMaker.Controls.StoryboardStrip
            && (e.Key == Key.Escape || (e.Key == Key.Space && m != KeyModifiers.None)) && (m & ~(KeyModifiers.Control | KeyModifiers.Shift)) == 0)
        {
            return;
        }

        if (e.Key == Key.F4 && m == KeyModifiers.None && this.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Name == "ViewCombo" && c.IsEffectivelyVisible) is { } views)
        {
            views.Focus();
            views.IsDropDownOpen = true;
            e.Handled = true;
            return;
        }
        ICommand? cmd = CommandFor(s, e.Key, m, typing);
        if (m == KeyModifiers.Alt && s.IsTimeline && e.Key is Key.I or Key.R or Key.U or Key.M or Key.O)
        {
            s.Timeline.ActivateTrack(e.Key switch
            {
                Key.I => AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video,
                Key.R => AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Transition,
                Key.U => AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Audio,
                Key.M => AvaMovieMaker.ViewModels.Timeline.TimelineTrack.AudioMusic,
                _ => AvaMovieMaker.ViewModels.Timeline.TimelineTrack.TitleOverlay,
            });
            this.GetVisualDescendants().OfType<AvaMovieMaker.Controls.TimelineControl>().FirstOrDefault(t => t.IsEffectivelyVisible)?.Focus();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.F6 || (e.Key == Key.Tab && !typing)) && (m == KeyModifiers.None || m == KeyModifiers.Shift))
        {
            CyclePanes(m == KeyModifiers.Shift ? -1 : 1);
            e.Handled = true;
            return;
        }

        if (cmd is not null && cmd.CanExecute(null))
        {
            cmd.Execute(null);
            e.Handled = true;
        }
    }

    internal static ICommand? CommandFor(ShellViewModel s, Key key, KeyModifiers m, bool typing) => (key, m) switch
    {
        (Key.N, KeyModifiers.Control) => s.NewProjectCommand,
        (Key.O, KeyModifiers.Control) => s.OpenProjectCommand,
        (Key.S, KeyModifiers.Control) => s.SaveCommand,
        (Key.F12, KeyModifiers.None) => s.SaveAsCommand,
        (Key.P, KeyModifiers.Control) => s.PublishCommand,
        (Key.I, KeyModifiers.Control) => s.ImportMediaCommand,
        (Key.Z, KeyModifiers.Control) => typing ? null : s.UndoCommand,
        (Key.Back, KeyModifiers.Alt) => s.UndoCommand,
        (Key.Y, KeyModifiers.Control) => typing ? null : s.RedoCommand,
        (Key.X, KeyModifiers.Control) => typing ? null : s.CutCommand,
        (Key.Delete, KeyModifiers.Shift) => typing ? null : s.CutCommand,
        (Key.C, KeyModifiers.Control) => typing ? null : s.CopyCommand,
        (Key.Insert, KeyModifiers.Control) => typing ? null : s.CopyCommand,
        (Key.V, KeyModifiers.Control) => typing ? null : s.PasteCommand,
        (Key.Insert, KeyModifiers.Shift) => typing ? null : s.PasteCommand,
        (Key.Delete, KeyModifiers.None) => typing ? null : s.RemoveCommand,
        (Key.A, KeyModifiers.Control) => typing ? null : s.SelectAllCommand,
        (Key.F2, KeyModifiers.None) => s.RenameCommand,
        (Key.Delete, KeyModifiers.Control) => typing ? null : s.ClearTimelineCommand,
        (Key.T, KeyModifiers.Control) => s.ToggleStoryboardTimelineCommand,
        (Key.PageDown, KeyModifiers.None) => typing ? null : s.ZoomInCommand,
        (Key.PageUp, KeyModifiers.None) => typing ? null : s.ZoomOutCommand,
        (Key.F9, KeyModifiers.None) => s.ZoomToFitCommand,
        (Key.Enter, KeyModifiers.Alt) => s.Monitor.FullScreenCommand,
        (Key.D, KeyModifiers.Control) => s.AddToTimelineCommand,
        (Key.E, KeyModifiers.Control) => s.TakePictureCommand,
        (Key.U, KeyModifiers.Control) => s.AudioVolumeCommand,
        (Key.I, KeyModifiers.None) or (Key.I, KeyModifiers.Control | KeyModifiers.Shift) => typing ? null : s.TrimBeginningCommand,
        (Key.O, KeyModifiers.None) or (Key.O, KeyModifiers.Control | KeyModifiers.Shift) => typing ? null : s.TrimEndCommand,
        (Key.U, KeyModifiers.None) or (Key.Delete, KeyModifiers.Control | KeyModifiers.Shift) => typing ? null : s.ClearTrimPointsCommand,
        (Key.M, KeyModifiers.None) or (Key.L, KeyModifiers.Control) => typing ? null : s.SplitCommand,
        (Key.N, KeyModifiers.None) or (Key.M, KeyModifiers.Control) => typing ? null : s.CombineCommand,
        (Key.B, KeyModifiers.Control | KeyModifiers.Shift) => s.NudgeLeftCommand,
        (Key.N, KeyModifiers.Control | KeyModifiers.Shift) => s.NudgeRightCommand,
        (Key.OemComma, KeyModifiers.None) => typing ? null : s.NudgeLeftCommand,
        (Key.OemPeriod, KeyModifiers.None) => typing ? null : s.NudgeRightCommand,
        (Key.K, KeyModifiers.None) or (Key.Space, KeyModifiers.None) => typing ? null : s.PlayPauseClipCommand,
        (Key.K, KeyModifiers.Control) => s.Monitor.StopCommand,
        (Key.W, KeyModifiers.Control) => s.Monitor.PlayProjectCommand,
        (Key.Q, KeyModifiers.Control) => s.Monitor.RewindCommand,
        (Key.Left, KeyModifiers.Control | KeyModifiers.Alt) => s.Monitor.BackCommand,
        (Key.Right, KeyModifiers.Control | KeyModifiers.Alt) => s.Monitor.ForwardCommand,
        (Key.J, KeyModifiers.None) => typing ? null : s.Monitor.PreviousFrameCommand,
        (Key.Left, KeyModifiers.Alt) => s.Monitor.PreviousFrameCommand,
        (Key.L, KeyModifiers.None) => typing ? null : s.Monitor.NextFrameCommand,
        (Key.Right, KeyModifiers.Alt) => s.Monitor.NextFrameCommand,
        (Key.MediaPlayPause, _) => s.PlayPauseClipCommand,
        (Key.MediaStop, _) => s.Monitor.StopCommand,
        (Key.MediaPreviousTrack, _) => s.Monitor.PreviousFrameCommand,
        (Key.MediaNextTrack, _) => s.Monitor.NextFrameCommand,
        _ => null,
    };

    private static bool IsInWindowOrPopup(object? source) =>
        source is Visual v && v.GetSelfAndVisualAncestors().Any(a => a is AvaWpf.InPageWindow or Avalonia.Controls.Primitives.OverlayPopupHost);

    private static bool Set(AvaMovieMaker.ViewModels.Timeline.TimelineViewModel timeline, bool expanded)
    {
        timeline.IsVideoExpanded = expanded;
        return true;
    }

    private static List<string> DroppedNames(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().Select(f => f.Name).ToList() ?? [];

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File) && Shell is { } shell)
        {
            List<string> names = DroppedNames(e);
            bool accept = names.Count == 0 ? !shell.IsBusy : shell.CanDropFiles(names);
            e.DragEffects = accept ? DragDropEffects.Copy : DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Shell is not { } shell || !e.DataTransfer.Contains(DataFormat.File) || e.Handled
            || e.DataTransfer.TryGetFiles() is not { } files || !shell.CanDropFiles(DroppedNames(e)))
        {
            return;
        }

        e.Handled = true;
        (TopLevel.GetTopLevel(this) as Window)?.Activate();
        IReadOnlyList<string> paths = await App.Platform.DroppedFiles([.. files]);
        await shell.DropFilesAsync(paths);
    }

    private void CyclePanes(int step)
    {
        Control?[] panes = [this.FindControl<Control>("TasksPane"), this.FindControl<Control>("ContentsPane"), this.FindControl<Control>("MonitorPane"), this.FindControl<Control>("StoryboardPane"), this.FindControl<Control>("TimelineView")];
        var visible = panes.Where(p => p is { IsEffectivelyVisible: true }).Cast<Control>().ToList();
        if (visible.Count == 0)
        {
            return;
        }

        Control? focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        int current = visible.FindIndex(p => focused is not null && (p == focused || p.IsVisualAncestorOf(focused)));
        int next = ((current < 0 ? (step > 0 ? -1 : 0) : current) + step + visible.Count) % visible.Count;
        Control target = visible[next];
        Control? first = target.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);
        (first ?? target).Focus(NavigationMethod.Tab);
    }
}
