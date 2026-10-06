using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;
using AvaMovieMaker.ViewModels.Session;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class SelectionTests
{
    private static List<Guid> Titles(Harness h, int n)
    {
        for (int i = 0; i < n; i++)
        {
            h.Session.Editor.AddTitle(new TitleContent { Lines = [$"t{i}"] }, null, MediaTime.Zero);
        }

        return [.. h.Session.Project.VideoTrack.Select(c => c.Id)];
    }

    [Fact]
    public void Click_ctrl_and_shift_on_a_track()
    {
        using var h = new Harness();
        List<Guid> ids = Titles(h, 4);
        TimelineViewModel t = h.Shell.Timeline;
        t.Click(TimelineTrack.Video, ids[1], ctrl: false, shift: false);
        Assert.Equal([ids[1]], h.Session.SelectedClips);
        t.Click(TimelineTrack.Video, ids[3], ctrl: false, shift: true);
        Assert.Equal([ids[1], ids[2], ids[3]], h.Session.SelectedClips);
        t.Click(TimelineTrack.Video, ids[2], ctrl: true, shift: false);
        Assert.Equal([ids[1], ids[3]], h.Session.SelectedClips);

        t.Click(TimelineTrack.Video, ids[1], ctrl: false, shift: false);
        Assert.Equal(2, h.Session.SelectedClips.Count);
        t.Collapse(TimelineTrack.Video, ids[1]);
        Assert.Equal([ids[1]], h.Session.SelectedClips);
    }

    [Fact]
    public void Keys_select_ranges_and_move_focus()
    {
        using var h = new Harness();
        List<Guid> ids = Titles(h, 4);
        TimelineViewModel t = h.Shell.Timeline;
        Assert.True(t.Key(SelectionKey.First));
        Assert.Equal([ids[0]], h.Session.SelectedClips);
        t.Key(SelectionKey.Next, shift: true);
        t.Key(SelectionKey.Next, shift: true);
        Assert.Equal([ids[0], ids[1], ids[2]], h.Session.SelectedClips);

        t.Key(SelectionKey.Next, ctrl: true);
        Assert.Equal(3, h.Session.SelectedClips.Count);
        Assert.Equal(ids[3], h.Session.SelectionActive);
        t.Key(SelectionKey.ToggleActive);
        Assert.Equal(4, h.Session.SelectedClips.Count);
        t.Key(SelectionKey.Clear);
        Assert.Empty(h.Session.SelectedClips);
    }

    [Fact]
    public void Remove_selects_the_next_clip_and_a_transition_is_removed_alone()
    {
        using var h = new Harness();
        List<Guid> ids = Titles(h, 3);
        TimelineViewModel t = h.Shell.Timeline;
        t.Click(TimelineTrack.Video, ids[1], ctrl: false, shift: false);
        h.Shell.RemoveCommand.Execute(null);
        Assert.Equal([ids[0], ids[2]], h.Session.Project.VideoTrack.Select(c => c.Id));
        Assert.Equal([ids[2]], h.Session.SelectedClips);

        Assert.True(h.Session.Editor.SetTransition(ids[2], "fade"));
        t.ActivateTrack(TimelineTrack.Transition);
        t.Click(TimelineTrack.Transition, ids[2], ctrl: false, shift: false);
        h.Shell.RemoveCommand.Execute(null);
        Assert.Equal(2, h.Session.Project.VideoTrack.Count);
        Assert.Null(h.Session.Project.VideoTrack[1].TransitionIn);
        Assert.Equal("Remove Transition", h.Session.Undo.UndoName);
    }

    [Fact]
    public void Select_all_takes_the_active_track_only()
    {
        using var h = new Harness();
        Titles(h, 2);
        h.Shell.IsTimeline = true;
        h.Shell.Timeline.ActivateTrack(TimelineTrack.TitleOverlay);
        h.Shell.SelectAllCommand.Execute(null);
        Assert.Empty(h.Session.SelectedClips);
        h.Shell.Timeline.ActivateTrack(TimelineTrack.Video);
        h.Shell.SelectAllCommand.Execute(null);
        Assert.Equal(2, h.Session.SelectedClips.Count);
    }
}

public sealed class ZoomTests
{
    [Fact]
    public void Thirteen_levels_fit_at_most_eighty_percent()
    {
        Assert.Equal(13, TimelineZoom.PixelsPerSecond.Length);
        Assert.Equal(4, TimelineZoom.PixelsPerSecond[TimelineZoom.DefaultStep]);

        Assert.Equal(8, TimelineZoom.Fit(30.05, 908));
        Assert.Equal(3, TimelineZoom.Fit(0, 908, current: 3));

        Assert.Equal(1, TimelineZoom.FramesPerPixel(8, 30));
        Assert.Equal(3, TimelineZoom.FramesPerPixel(7, 30));
        Assert.Equal(7, TimelineZoom.FramesPerPixel(6, 30));
        Assert.Equal(15, TimelineZoom.FramesPerPixel(5, 30));
    }
}
