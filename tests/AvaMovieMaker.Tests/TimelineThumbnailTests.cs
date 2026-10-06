using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Views;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.Tests;

public sealed class TimelineThumbnailTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    [AvaloniaFact]
    public void A_narrow_clip_still_shows_its_thumbnail()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TimelineEditor editor = Shell.Session.Editor;
            var content = new TitleContent { Lines = ["Hi"], BackgroundColor = 0xFF00FF00 };
            Time.MediaTime length = Time.MediaTime.FromSeconds(20 / Shell.Timeline.PixelsPerSecond);
            editor.InsertVideoClips(0, [editor.NewTitleClip(content) with { StillDuration = length }], UndoNames.AddTitle);
            TestApp.Pump();

            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            double width = Shell.Timeline.ToPixels(length) - Shell.Timeline.ToPixels(Time.MediaTime.Zero);
            Assert.InRange(width, 7, 40);
            Point inside = tl.TranslatePoint(new Point(TimelineControl.HeaderWidth + width / 2 + 1, tl.RowCenter(TimelineTrack.Video)), w)!.Value;

            using WriteableBitmap frame = w.CaptureRenderedFrame()!;
            using ILockedFramebuffer fb = frame.Lock();
            uint v;
            unsafe
            {
                v = ((uint*)(fb.Address + (int)inside.Y * fb.RowBytes))[(int)inside.X];
            }

            Assert.Equal(0xFFu, (v >> 8) & 0xFF);
            Assert.Equal(0u, v & 0xFF);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    private static uint[] Pixels(MainWindow w, Rect area)
    {
        using WriteableBitmap frame = w.CaptureRenderedFrame()!;
        using ILockedFramebuffer fb = frame.Lock();
        var list = new List<uint>();
        unsafe
        {
            for (int y = (int)area.Y; y < (int)area.Bottom; y++)
            {
                for (int x = (int)area.X; x < (int)area.Right; x++)
                {
                    list.Add(((uint*)(fb.Address + y * fb.RowBytes))[x]);
                }
            }
        }

        return [.. list];
    }

    [AvaloniaFact]
    public void A_narrow_clip_still_shows_its_effect_star()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TimelineEditor editor = Shell.Session.Editor;
            Time.MediaTime length = Time.MediaTime.FromSeconds(35 / Shell.Timeline.PixelsPerSecond);
            editor.InsertVideoClips(0, [editor.NewTitleClip(new TitleContent { Lines = ["Hi"] }) with { StillDuration = length }], UndoNames.AddTitle);
            TestApp.Pump();
            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            double width = Shell.Timeline.ToPixels(length) - Shell.Timeline.ToPixels(Time.MediaTime.Zero);
            Point topLeft = tl.TranslatePoint(new Point(TimelineControl.HeaderWidth, tl.RowCenter(TimelineTrack.Video) - 25), w)!.Value;
            var clipArea = new Rect(topLeft, new Size(width, 50));
            uint[] before = Pixels(w, clipArea);

            editor.AddEffect([Shell.Project.VideoTrack[0].Id], "sepia-tone");
            TestApp.Pump();
            uint[] after = Pixels(w, clipArea);
            int changed = before.Zip(after).Count(p => p.First != p.Second);
            Assert.True(changed > 150, $"{changed} pixels changed");

            w.MouseDown(new Point(topLeft.X + 12, topLeft.Y + 37), MouseButton.Right);
            w.MouseUp(new Point(topLeft.X + 12, topLeft.Y + 37), MouseButton.Right);
            TestApp.Pump();
            ContextMenu menu = Assert.IsType<ContextMenu>(BoardMenus.Current);
            Assert.Contains(menu.Items.OfType<MenuItem>(), m => (string?)m.Header == "_Remove Effect");
            menu.Close();
            TestApp.Pump();
        }
        finally
        {
            TestApp.Close(w);
        }
    }
}
