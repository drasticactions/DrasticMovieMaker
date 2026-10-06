using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.ViewModels.Tests;

public class TimelineKeyboardTests
{
    [Fact]
    public void Alt_track_keys_activate_and_expand()
    {
        using var h = new Harness();
        TimelineViewModel t = h.Shell.Timeline;
        Assert.Equal(TimelineTrack.Video, t.ActiveTrack);
        Assert.False(t.IsVideoExpanded);
        t.ActivateTrack(TimelineTrack.Audio);
        Assert.True(t.IsVideoExpanded);
        Assert.Equal(TimelineTrack.Audio, t.ActiveTrack);
        t.IsVideoExpanded = false;
        Assert.Equal(TimelineTrack.Video, t.ActiveTrack);
        t.MoveTrack(1);
        Assert.Equal(TimelineTrack.AudioMusic, t.ActiveTrack);
        t.MoveTrack(5);
        Assert.Equal(TimelineTrack.TitleOverlay, t.ActiveTrack);
    }

    [Fact]
    public void Arrows_walk_the_clips_of_the_active_track()
    {
        using var h = new Harness();
        Guid a = h.AddTitle("one");
        h.Session.Editor.AddTitle(new Effects.Titles.TitleContent { Lines = ["two"] }, null, Time.MediaTime.Zero);
        TimelineViewModel t = h.Shell.Timeline;
        var order = h.Session.Project.VideoTrack.Select(c => c.Id).ToList();
        Assert.True(t.SelectClip(1));
        Assert.Equal([order[0]], h.Session.SelectedClips);
        Assert.True(t.SelectClip(1));
        Assert.Equal([order[1]], h.Session.SelectedClips);
        Assert.True(t.SelectClip(-1, toEnd: true));
        Assert.Equal([order[0]], h.Session.SelectedClips);
        Assert.True(t.SelectClip(1, extend: true));
        Assert.Equal(2, h.Session.SelectedClips.Count);
        Assert.Contains(a, order);
    }
}

internal static class ShellViewModelTestsHelpers
{
    public static void SetView(Harness h, ContentsView view)
    {
        if (view == ContentsView.Effects)
        {
            h.Shell.ShowEffectsCommand.Execute(null);
        }
        else if (view == ContentsView.Transitions)
        {
            h.Shell.ShowTransitionsCommand.Execute(null);
        }
    }
}
