using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Views;
using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Tests;

public sealed class DragDropTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static MediaItem Picture(string name)
    {
        var m = new MediaItem { Kind = MediaKind.Picture, Path = $"/nonexistent/{name}.png", Name = name, Clips = [new SourceClip { Name = name }] };
        Shell.Session.Editor.ImportMedia([m]);
        return m;
    }

    private static void TwoClips()
    {
        MediaItem a = Picture("a"), b = Picture("b");
        Shell.Session.Editor.InsertVideoClips(0, [Shell.Session.Editor.NewVideoClip(a), Shell.Session.Editor.NewVideoClip(b)]);
        TestApp.Pump();
    }

    private static void Drag(MainWindow w, DragPayload payload, params Point[] path)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DragPayload.Format, payload));
        w.DragDrop(path[0], RawDragEventType.DragEnter, data, DragDropEffects.Copy | DragDropEffects.Move);
        foreach (Point p in path)
        {
            w.DragDrop(p, RawDragEventType.DragOver, data, DragDropEffects.Copy | DragDropEffects.Move);
        }

        w.DragDrop(path[^1], RawDragEventType.Drop, data, DragDropEffects.Copy | DragDropEffects.Move);
        TestApp.Pump();
    }

    private static Point In(Visual v, MainWindow w, double x, double y) => v.TranslatePoint(new Point(x, y), w)!.Value;

    [AvaloniaFact]
    public void Effect_and_transition_drop_on_the_storyboard()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            StoryboardStrip strip = w.GetVisualDescendants().OfType<StoryboardStrip>().First(s => s.IsEffectivelyVisible);

            Drag(w, DragPayload.ForEffect("sepia-tone"), In(strip, w, 40, 60), In(strip, w, 2 + StoryboardStrip.Pitch + 60, 60));
            Assert.Equal(["sepia-tone"], Shell.Project.VideoTrack[1].Effects.Select(e => e.EffectId));

            Drag(w, DragPayload.ForTransition("fade"), In(strip, w, 300, 60), In(strip, w, 2 + StoryboardStrip.Pitch - StoryboardStrip.SlotGap - StoryboardStrip.SlotWidth / 2, 60));
            Assert.Equal("fade", Shell.Project.VideoTrack[1].TransitionIn?.TransitionId);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Effect_and_transition_drop_on_the_timeline()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            AvaMovieMaker.ViewModels.Timeline.TimelineViewModel vm = Shell.Timeline;
            double X(double seconds) => TimelineControl.HeaderWidth + vm.ToPixels(MediaTime.FromSeconds(seconds));
            double videoY = tl.RowCenter(AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video);

            Drag(w, DragPayload.ForEffect("sepia-tone"), In(tl, w, X(1), videoY), In(tl, w, X(7), videoY));
            Assert.Equal(["sepia-tone"], Shell.Project.VideoTrack[1].Effects.Select(e => e.EffectId));

            Drag(w, DragPayload.ForTransition("fade"), In(tl, w, X(1), videoY), In(tl, w, X(6), videoY));
            Assert.Equal("fade", Shell.Project.VideoTrack[1].TransitionIn?.TransitionId);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void A_drop_right_after_entering_still_drops_and_the_drag_image_follows()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            StoryboardStrip strip = w.GetVisualDescendants().OfType<StoryboardStrip>().First(s => s.IsEffectivelyVisible);
            Point cell = In(strip, w, 2 + StoryboardStrip.Pitch + 60, 60);
            var image = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(20, 10), new Vector(96, 96));
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DragPayload.Format, DragPayload.ForEffect("sepia-tone").WithImage(image, new Point(5, 5))));
            const DragDropEffects all = DragDropEffects.Copy | DragDropEffects.Move;

            w.DragDrop(cell, RawDragEventType.DragEnter, data, all);
            w.DragDrop(cell, RawDragEventType.DragOver, data, all);
            TestApp.Pump();
            Avalonia.Controls.Primitives.OverlayLayer layer = Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(w)!;
            Avalonia.Controls.Control ghost = Assert.Single(layer.Children, c => !c.IsHitTestVisible && c.Opacity < 1);
            Assert.Equal(cell.X - 5, Avalonia.Controls.Canvas.GetLeft(ghost), 1);

            w.DragDrop(new Point(300, 150), RawDragEventType.DragOver, data, all);
            w.DragDrop(cell, RawDragEventType.DragEnter, data, all);
            w.DragDrop(cell, RawDragEventType.Drop, data, all);
            TestApp.Pump();
            Assert.Equal(["sepia-tone"], Shell.Project.VideoTrack[1].Effects.Select(e => e.EffectId));
            Assert.DoesNotContain(ghost, layer.Children);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void A_click_in_the_contents_pane_selects_only_that_item()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Picture("a");
            Picture("b");
            TestApp.Pump();
            var thumbs = w.GetVisualDescendants().OfType<Avalonia.Controls.ListBox>().First(l => l.Name == "Thumbs");
            var items = thumbs.GetVisualDescendants().OfType<Avalonia.Controls.ListBoxItem>().ToList();
            Assert.Equal(2, items.Count);
            foreach (Avalonia.Controls.ListBoxItem item in items)
            {
                Point p = In(item, w, item.Bounds.Width / 2, item.Bounds.Height / 2);
                w.MouseDown(p, MouseButton.Left);
                w.MouseUp(p, MouseButton.Left);
                TestApp.Pump();
            }

            Assert.Equal(["b"], Shell.Contents.SelectedItems.Select(i => i.Media.Name));

            Point first = In(items[0], w, items[0].Bounds.Width / 2, items[0].Bounds.Height / 2);
            w.MouseDown(first, MouseButton.Left, RawInputModifiers.Control);
            w.MouseUp(first, MouseButton.Left, RawInputModifiers.Control);
            TestApp.Pump();
            Assert.Equal(["a", "b"], Shell.Contents.SelectedItems.Select(i => i.Media.Name).Order());
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    private static void DragWith(MainWindow w, DragPayload payload, Point at, RawInputModifiers mods)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DragPayload.Format, payload));
        const DragDropEffects all = DragDropEffects.Copy | DragDropEffects.Move;
        w.DragDrop(at, RawDragEventType.DragEnter, data, all, mods);
        w.DragDrop(at, RawDragEventType.DragOver, data, all, mods);
        w.DragDrop(at, RawDragEventType.Drop, data, all, mods);
        TestApp.Pump();
    }

    [AvaloniaFact]
    public void Track_rules_ctrl_copy_and_audio_on_the_storyboard()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            var song = new MediaItem { Kind = MediaKind.Audio, Path = "/nonexistent/song.wav", Name = "song", Duration = MediaTime.FromSeconds(20), Clips = [new SourceClip { Name = "song", End = MediaTime.FromSeconds(20) }] };
            Shell.Session.Editor.ImportMedia([song]);
            StoryboardStrip strip = w.GetVisualDescendants().OfType<StoryboardStrip>().First(s => s.IsEffectivelyVisible);

            Shell.Session.Select([Shell.Project.VideoTrack[0].Id]);
            DragWith(w, DragPayload.ForStoryboardMove(), In(strip, w, 2 + StoryboardStrip.Pitch * 2 + 10, 60), RawInputModifiers.Control);
            Assert.Equal(3, Shell.Project.VideoTrack.Count);

            DragWith(w, DragPayload.ForMedia([(song, null)]), In(strip, w, 40, 60), RawInputModifiers.None);
            Assert.Single(Shell.Project.AudioMusicTrack);
            Assert.True(Shell.IsTimeline);
            TestApp.Pump();

            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            double videoY = tl.RowCenter(AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video);
            DragWith(w, DragPayload.ForMedia([(song, null)]), In(tl, w, TimelineControl.HeaderWidth + 20, videoY), RawInputModifiers.None);
            Assert.Single(Shell.Project.AudioMusicTrack);
            Assert.Equal(3, Shell.Project.VideoTrack.Count);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    private static void Choose(string? header)
    {
        Avalonia.Controls.ContextMenu menu = Assert.IsType<Avalonia.Controls.ContextMenu>(DropMenu.Current);
        if (header is null)
        {
            menu.Close();
        }
        else
        {
            menu.Items.OfType<Avalonia.Controls.MenuItem>().First(m => (string?)m.Header == header)
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.MenuItem.ClickEvent));
            menu.Close();
        }

        TestApp.Pump();
    }

    private static string[] MenuHeaders() =>
        [.. Assert.IsType<Avalonia.Controls.ContextMenu>(DropMenu.Current).Items.Select(i => i is Avalonia.Controls.MenuItem m ? (string)m.Header! : "-")];

    [AvaloniaFact]
    public void Right_drag_asks_copy_move_or_cancel()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            Guid first = Shell.Project.VideoTrack[0].Id;
            StoryboardStrip strip = w.GetVisualDescendants().OfType<StoryboardStrip>().First(s => s.IsEffectivelyVisible);
            Point end = In(strip, w, 2 + StoryboardStrip.Pitch * 2 + 10, 60);

            Shell.Session.Select([first]);
            DragWith(w, DragPayload.ForStoryboardMove().AsRightDrag(), end, RawInputModifiers.None);
            Assert.Equal(["_Copy", "_Move", "-", "Cancel"], MenuHeaders());
            Assert.Equal(first, Shell.Project.VideoTrack[0].Id);
            Choose("_Move");
            Assert.Equal(2, Shell.Project.VideoTrack.Count);
            Assert.Equal(first, Shell.Project.VideoTrack[1].Id);

            DragWith(w, DragPayload.ForStoryboardMove().AsRightDrag(), In(strip, w, 10, 60), RawInputModifiers.None);
            Choose("Cancel");
            DragWith(w, DragPayload.ForStoryboardMove().AsRightDrag(), In(strip, w, 10, 60), RawInputModifiers.None);
            Choose(null);
            Assert.Equal(first, Shell.Project.VideoTrack[1].Id);

            DragWith(w, DragPayload.ForStoryboardMove().AsRightDrag(), In(strip, w, 10, 60), RawInputModifiers.None);
            Choose("_Copy");
            Assert.Equal(3, Shell.Project.VideoTrack.Count);

            DragWith(w, DragPayload.ForEffect("sepia-tone").AsRightDrag(), In(strip, w, 40, 60), RawInputModifiers.None);
            Assert.Equal(["_Copy", "-", "Cancel"], MenuHeaders());
            Choose("_Copy");
            Assert.Single(Shell.Project.VideoTrack[0].Effects);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Titles_cross_between_video_and_title_overlay()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            var editor = Shell.Session.Editor;
            editor.InsertVideoClips(2, [editor.NewTitleClip(new TitleContent { Lines = ["Hello"] })], UndoNames.AddTitle);
            Guid title = Shell.Project.VideoTrack[2].Id;
            w.Height = 1100;
            TestApp.Pump();
            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            double overlayY = tl.RowCenter(AvaMovieMaker.ViewModels.Timeline.TimelineTrack.TitleOverlay);
            Assert.True(overlayY > 0 && overlayY < tl.Bounds.Height, $"{overlayY} {tl.Bounds} {Shell.Timeline.IsVideoExpanded}");

            Shell.Timeline.Click(AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video, title, ctrl: false, shift: false);
            DragWith(w, DragPayload.ForStoryboardMove(), In(tl, w, TimelineControl.HeaderWidth + 4, overlayY), RawInputModifiers.None);
            Assert.Equal(2, Shell.Project.VideoTrack.Count);
            TitleClip overlay = Assert.Single(Shell.Project.TitleOverlayTrack);
            Assert.Equal(title, overlay.Id);
            Assert.Equal(["Hello"], overlay.Content.Lines);

            Shell.Timeline.Click(AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video, Shell.Project.VideoTrack[0].Id, ctrl: false, shift: false);
            DragWith(w, DragPayload.ForStoryboardMove(), In(tl, w, TimelineControl.HeaderWidth + 200, overlayY), RawInputModifiers.None);
            Assert.Equal(2, Shell.Project.VideoTrack.Count);

            double videoY = tl.RowCenter(AvaMovieMaker.ViewModels.Timeline.TimelineTrack.Video);
            Point grab = In(tl, w, TimelineControl.HeaderWidth + Shell.Timeline.ToPixels(overlay.Start + overlay.Duration / 2), overlayY);
            w.MouseDown(grab, MouseButton.Left);
            w.MouseMove(grab + new Point(0, 4));
            w.MouseMove(In(tl, w, TimelineControl.HeaderWidth + 2, videoY));
            w.MouseUp(In(tl, w, TimelineControl.HeaderWidth + 2, videoY), MouseButton.Left);
            TestApp.Pump();
            Assert.Empty(Shell.Project.TitleOverlayTrack);
            Assert.Equal(title, Shell.Project.VideoTrack[0].Id);
            Assert.Equal(VideoClipKind.Title, Shell.Project.VideoTrack[0].Kind);

            Shell.Session.Undo.Undo();
            Assert.Single(Shell.Project.TitleOverlayTrack);
            Shell.Session.Undo.Undo();
            Assert.Empty(Shell.Project.TitleOverlayTrack);
            Assert.Equal(title, Shell.Project.VideoTrack[2].Id);
        }
        finally
        {
            TestApp.Close(w);
        }
    }
}
