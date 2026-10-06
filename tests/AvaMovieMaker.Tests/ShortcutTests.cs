using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaMovieMaker.Views;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.Tests;

public sealed class ShortcutTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static void Press(MainWindow w, PhysicalKey key, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.KeyPressQwerty(key, mods);
        w.KeyReleaseQwerty(key, mods);
        TestApp.Pump();
    }

    [AvaloniaFact]
    public void Board_zoom_and_track_keys()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            w.View!.Focus();
            bool timeline = Shell.IsTimeline;
            Press(w, PhysicalKey.T, RawInputModifiers.Control);
            Assert.NotEqual(timeline, Shell.IsTimeline);
            if (!Shell.IsTimeline)
            {
                Press(w, PhysicalKey.T, RawInputModifiers.Control);
            }

            int zoom = Shell.Timeline.ZoomStep;
            Press(w, PhysicalKey.PageDown);
            Assert.Equal(zoom + 1, Shell.Timeline.ZoomStep);
            Press(w, PhysicalKey.PageUp);
            Assert.Equal(zoom, Shell.Timeline.ZoomStep);

            Press(w, PhysicalKey.U, RawInputModifiers.Alt);
            Assert.Equal(TimelineTrack.Audio, Shell.Timeline.ActiveTrack);
            Assert.True(Shell.Timeline.IsVideoExpanded);
            Press(w, PhysicalKey.M, RawInputModifiers.Alt);
            Assert.Equal(TimelineTrack.AudioMusic, Shell.Timeline.ActiveTrack);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Timeline_table_keys_and_f4()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
                TestApp.Pump();
            }

            w.Activate();
            Shell.Timeline.ActivateTrack(TimelineTrack.AudioMusic);
            TestApp.Pump();

            Press(w, PhysicalKey.I, RawInputModifiers.Alt);
            Assert.Equal(TimelineTrack.Video, Shell.Timeline.ActiveTrack);
            Assert.IsType<AvaMovieMaker.Controls.TimelineControl>(TopLevel.GetTopLevel(w)!.FocusManager!.GetFocusedElement());
            Shell.Timeline.IsVideoExpanded = true;
            Press(w, PhysicalKey.NumPadSubtract);
            Assert.False(Shell.Timeline.IsVideoExpanded);
            Press(w, PhysicalKey.NumPadAdd);
            Assert.True(Shell.Timeline.IsVideoExpanded);

            double scroll = Shell.Timeline.ScrollSeconds;
            Press(w, PhysicalKey.ArrowRight, RawInputModifiers.Control | RawInputModifiers.Alt);
            Assert.True(Shell.Timeline.ScrollSeconds > scroll);
            Press(w, PhysicalKey.ArrowLeft, RawInputModifiers.Control | RawInputModifiers.Alt);
            Assert.Equal(scroll, Shell.Timeline.ScrollSeconds, 6);

            Press(w, PhysicalKey.F4);
            Assert.Contains(w.GetVisualDescendants().OfType<Avalonia.Controls.ComboBox>(), c => c.Name == "ViewCombo" && c.IsDropDownOpen);
            Press(w, PhysicalKey.Escape);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void A_clicked_toolbar_button_does_not_take_space()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TestApp.Pump();
            w.View!.Focus();
            IInputElement? focused = w.FocusManager!.GetFocusedElement();

            Button zoomIn = w.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Zoom In");
            Avalonia.Point at = zoomIn.TranslatePoint(new Avalonia.Point(zoomIn.Bounds.Width / 2, zoomIn.Bounds.Height / 2), w)!.Value;
            int zoom = Shell.Timeline.ZoomStep;
            w.MouseDown(at, MouseButton.Left);
            w.MouseUp(at, MouseButton.Left);
            TestApp.Pump();
            Assert.Equal(zoom + 1, Shell.Timeline.ZoomStep);
            Assert.Same(focused, w.FocusManager!.GetFocusedElement());

            Press(w, PhysicalKey.Space);
            Assert.Equal(zoom + 1, Shell.Timeline.ZoomStep);

            foreach (string name in (string[])["Import Media", "Undo", "Publish Movie", "Rewind Timeline", "Play Timeline", "Zoom Out", "Split", "Tasks", "Collections", "Views"])
            {
                Assert.False(w.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Cast<Control>()
                    .Concat(w.GetVisualDescendants().OfType<Button>())
                    .First(b => AutomationProperties.GetName(b) == name).Focusable, name);
            }
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Space_plays_and_pauses_with_the_timeline_focused()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            var editor = Shell.Session.Editor;
            editor.InsertVideoClips(0, [editor.NewTitleClip(new AvaMovieMaker.Effects.Titles.TitleContent { Lines = ["Hi"] })]);
            TestApp.Pump();

            AvaMovieMaker.Controls.TimelineControl tl = w.GetVisualDescendants().OfType<AvaMovieMaker.Controls.TimelineControl>().First(t => t.IsEffectivelyVisible);
            Point at = tl.TranslatePoint(new Point(AvaMovieMaker.Controls.TimelineControl.HeaderWidth + 10, tl.RowCenter(TimelineTrack.Video)), w)!.Value;
            w.MouseDown(at, MouseButton.Left);
            w.MouseUp(at, MouseButton.Left);
            TestApp.Pump();
            Assert.Same(tl, w.FocusManager!.GetFocusedElement());

            Press(w, PhysicalKey.Space);
            Assert.True(Shell.Monitor.IsPlaying);
            Press(w, PhysicalKey.Space);
            Assert.False(Shell.Monitor.IsPlaying);

            Press(w, PhysicalKey.Escape);
            Assert.Empty(Shell.Session.SelectedClips);
        }
        finally
        {
            if (Shell.Monitor.IsPlaying)
            {
                Shell.PlayPauseClipCommand.Execute(null);
            }

            TestApp.Close(w);
        }
    }
}
