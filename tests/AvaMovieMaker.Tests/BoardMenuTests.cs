using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Views;
using AvaMovieMaker.Media;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.Tests;

public sealed class BoardMenuTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static void TwoClips()
    {
        TimelineEditor editor = Shell.Session.Editor;
        var clips = new List<VideoClip>();
        foreach (string name in (string[])["a", "b"])
        {
            var m = new MediaItem { Kind = MediaKind.Picture, Path = $"/nonexistent/{name}.png", Name = name, Clips = [new SourceClip { Name = name }] };
            editor.ImportMedia([m]);
            clips.Add(editor.NewVideoClip(m));
        }

        editor.InsertVideoClips(0, clips);
        TestApp.Pump();
    }

    private static void RightClick(MainWindow w, Point at)
    {
        w.MouseDown(at, MouseButton.Right);
        w.MouseUp(at, MouseButton.Right);
        TestApp.Pump();
    }

    private static string[] Headers() =>
        [.. Assert.IsType<ContextMenu>(BoardMenus.Current).Items.Select(i => i is MenuItem m ? (string)m.Header! : "-")];

    private static void Choose(string header)
    {
        ContextMenu menu = Assert.IsType<ContextMenu>(BoardMenus.Current);
        MenuItem item = menu.Items.OfType<MenuItem>().First(m => (string?)m.Header == header);
        Assert.True(item.Command!.CanExecute(item.CommandParameter), header);
        item.Command.Execute(item.CommandParameter);
        menu.Close();
        TestApp.Pump();
    }

    private static void Close()
    {
        BoardMenus.Current?.Close();
        TestApp.Pump();
    }

    [AvaloniaFact]
    public void The_effect_star_menu_removes_the_effects()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            Shell.Session.Editor.AddEffect([Shell.Project.VideoTrack[1].Id], "sepia-tone");
            Shell.Session.Editor.AddEffect([Shell.Project.VideoTrack[1].Id], "blur");
            TestApp.Pump();
            StoryboardStrip strip = w.GetVisualDescendants().OfType<StoryboardStrip>().First(s => s.IsEffectivelyVisible);

            Point star = strip.TranslatePoint(new Point(2 + StoryboardStrip.Pitch + 4 + 4 + 12, 4 + StoryboardStrip.PictureHeight - 14), w)!.Value;
            RightClick(w, star);
            Assert.Equal([Shell.Project.VideoTrack[1].Id], Shell.Session.SelectedClips);
            Assert.Equal(["_Effects...", "-", "Cu_t", "_Copy", "_Paste", "_Remove Effect"], Headers());
            Choose("_Remove Effect");
            Assert.Empty(Shell.Project.VideoTrack[1].Effects);
            Assert.Equal("Remove Effects", Shell.Session.Undo.UndoName);
            Assert.Equal(2, Shell.Project.VideoTrack.Count);

            RightClick(w, strip.TranslatePoint(new Point(2 + 80, 40), w)!.Value);
            Assert.Equal([Shell.Project.VideoTrack[0].Id], Shell.Session.SelectedClips);
            Assert.Contains("_Effects...", Headers());
            Assert.Equal("_Play Storyboard", Headers()[5]);
            Close();

            RightClick(w, strip.TranslatePoint(new Point(2 + StoryboardStrip.Pitch * 4, 40), w)!.Value);
            Assert.Equal(["_Paste", "_Play Storyboard", "Select _All", "Cl_ear Storyboard"], Headers());
            Close();
        }
        finally
        {
            Close();
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Timeline_menus_follow_the_track()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            TwoClips();
            Assert.True(Shell.Session.Editor.SetTransition(Shell.Project.VideoTrack[1].Id, "fade"));
            Shell.Timeline.IsVideoExpanded = true;
            w.Height = 1100;
            TestApp.Pump();
            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            double X(Time.MediaTime t) => TimelineControl.HeaderWidth + Shell.Timeline.ToPixels(t);

            RightClick(w, tl.TranslatePoint(new Point(X(Time.MediaTime.FromSeconds(1)), tl.RowCenter(TimelineTrack.Video)), w)!.Value);
            Assert.Equal(["Cu_t", "_Copy", "_Paste", "Re_move", "-", "_Play Timeline", "Select _All", "-", "_Effects...", "Fade _In", "Fade _Out", "-", "_Browse for Missing File...", "P_roperties"], Headers());
            Assert.IsType<ContextMenu>(BoardMenus.Current).Items.OfType<MenuItem>().First(m => (string?)m.Header == "Fade _In").IsSelected = true;
            Assert.Equal(Strings.PromptVideoFadeIn, Shell.StatusText);
            Close();

            Time.MediaTime start = Shell.Timeline.Layout.Starts[1];
            RightClick(w, tl.TranslatePoint(new Point(X(start) + 3, tl.RowCenter(TimelineTrack.Transition)), w)!.Value);
            Assert.Contains("_Remove Transition", Headers());
            Choose("_Remove Transition");
            Assert.Null(Shell.Project.VideoTrack[1].TransitionIn);

            RightClick(w, tl.TranslatePoint(new Point(X(Time.MediaTime.FromSeconds(60)), tl.RowCenter(TimelineTrack.Video)), w)!.Value);
            Assert.Equal(["_Paste", "_Play Timeline", "Select _All", "Cl_ear Timeline"], Headers());
        }
        finally
        {
            Close();
            TestApp.Close(w);
        }
    }
}
