using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Dialogs;
using AvaMovieMaker.Views;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Tests;

public sealed class PreviewMonitorTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static void WithTitle(Action<MainWindow> body)
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Shell.Session.Editor.AddTitle(new TitleContent { Lines = ["Hello"] }, null, MediaTime.Zero);
            Shell.Monitor.ShowProject(seekStart: true);
            TestApp.Pump();
            body(w);
        }
        finally
        {
            Shell.Monitor.Playback.Pause();
            TestApp.Pump();
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Preview_frames_follow_a_change_of_aspect_ratio() => WithTitle(w =>
    {
        static double Ratio() => Shell.Monitor.Playback.PreviewSize.Width / (double)Shell.Monitor.Playback.PreviewSize.Height;
        AspectRatio was = Shell.Aspect;
        try
        {
            foreach (AspectRatio aspect in new[] { AspectRatio.Widescreen16x9, AspectRatio.Vertical9x16, AspectRatio.Standard4x3 })
            {
                Shell.SetAspectRatioCommand.Execute(aspect);
                TestApp.Pump();
                Assert.Equal(AspectRatios.Value(aspect), Ratio(), 0.02);
            }
        }
        finally
        {
            Shell.SetAspectRatioCommand.Execute(was);
            TestApp.Pump();
        }
    });

    [AvaloniaFact]
    public void Board_play_button_becomes_pause_while_the_project_plays() => WithTitle(w =>
    {
        Button play = w.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Play Storyboard" && b.IsEffectivelyVisible);
        Button rewind = w.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Rewind Storyboard" && b.IsEffectivelyVisible);
        Assert.True(play.IsEffectivelyEnabled);
        Assert.Equal("Play Storyboard (Ctrl+W)", ToolTip.GetTip(play));
        Assert.Equal("Rewind Storyboard (Ctrl+Q)", ToolTip.GetTip(rewind));
        MenuItem playClip = w.GetLogicalDescendants().OfType<MenuItem>().First(m => ReferenceEquals(m.Command, Shell.PlayPauseClipCommand));
        Assert.Equal("Play _Clip", playClip.Header);

        play.Command!.Execute(null);
        TestApp.Pump();
        Assert.Equal("Pause Storyboard (Ctrl+W)", ToolTip.GetTip(play));
        Assert.Equal("pause", play.GetVisualDescendants().OfType<Glyph>().Single().Kind);
        Assert.Equal("Pause _Clip", playClip.Header);

        play.Command.Execute(null);
        TestApp.Pump();
        Assert.Equal("Play Storyboard (Ctrl+W)", ToolTip.GetTip(play));
        Assert.Equal("play", play.GetVisualDescendants().OfType<Glyph>().Single().Kind);
    });

    [AvaloniaFact]
    public void Full_screen_keys_keep_playback_keys_and_leave_on_others() => WithTitle(main =>
    {
        MonitorViewModel m = Shell.Monitor;
        Assert.Same(m.PlayPauseCommand, FullScreenView.CommandFor(m, Key.Space, KeyModifiers.None, out bool leave));
        Assert.False(leave);
        Assert.Same(m.PreviousFrameCommand, FullScreenView.CommandFor(m, Key.J, KeyModifiers.None, out _));
        Assert.Same(m.NextFrameCommand, FullScreenView.CommandFor(m, Key.L, KeyModifiers.None, out _));
        Assert.Same(m.BackCommand, FullScreenView.CommandFor(m, Key.Left, KeyModifiers.Control | KeyModifiers.Alt, out _));
        Assert.Same(m.PlayProjectCommand, FullScreenView.CommandFor(m, Key.W, KeyModifiers.Control, out _));
        Assert.Same(m.RewindCommand, FullScreenView.CommandFor(m, Key.Q, KeyModifiers.Control, out _));
        Assert.Null(FullScreenView.CommandFor(m, Key.LeftCtrl, KeyModifiers.Control, out leave));
        Assert.False(leave);
        Assert.Null(FullScreenView.CommandFor(m, Key.LeftAlt, KeyModifiers.Alt, out leave));
        Assert.True(leave);
        Assert.Null(FullScreenView.CommandFor(m, Key.Escape, KeyModifiers.None, out leave));
        Assert.True(leave);
        Assert.Null(FullScreenView.CommandFor(m, Key.Enter, KeyModifiers.Alt, out leave));
        Assert.True(leave);

        Window ShowFull()
        {
            var view = new FullScreenView(m);
            new FullScreenWindowHost().Show(view, view);
            TestApp.Pump();
            return (Window)TopLevel.GetTopLevel(view)!;
        }

        Window full = ShowFull();
        full.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        TestApp.Pump();
        Assert.True(full.IsVisible);
        full.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.None);
        TestApp.Pump();
        Assert.False(full.IsVisible);

        full = ShowFull();
        full.MouseDown(new Point(10, 10), MouseButton.Right);
        TestApp.Pump();
        Assert.False(full.IsVisible);

        full = ShowFull();
        m.ShowItem(new MediaItem { Kind = Media.MediaKind.Audio, Path = "/nonexistent/a.flac", Name = "a", Duration = MediaTime.FromSeconds(1) }, null, play: false);
        TestApp.Pump();
        Assert.False(full.IsVisible);

        Assert.Null(FullScreenView.CommandFor(m, Key.W, KeyModifiers.Control, out leave));
        Assert.True(leave);
        Assert.False(m.FullScreenCommand.CanExecute(null));
    });
}
