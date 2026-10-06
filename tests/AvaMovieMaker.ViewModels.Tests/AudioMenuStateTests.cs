using AvaMovieMaker.Media;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.ViewModels.Tests;

public class AudioMenuStateTests
{
    private static Guid Song(Harness h, string name)
    {
        var m = new MediaItem
        {
            Kind = MediaKind.Audio,
            Path = $"/media/{name}.wav",
            Name = name,
            Duration = MediaTime.FromSeconds(10),
            Audio = new AudioProperties { SampleRate = 48000, Channels = 2 },
            Clips = [new SourceClip { Name = name, End = MediaTime.FromSeconds(10) }],
        };
        h.Session.Editor.ImportMedia([m]);
        h.Session.Editor.AddAudioClip(m, null, MediaTime.FromSeconds(h.Session.Project.AudioMusicTrack.Count * 20));
        return h.Session.Project.AudioMusicTrack.Single(a => a.MediaId == m.Id).Id;
    }

    [Fact]
    public void Checks_follow_the_selected_clip()
    {
        using var h = new Harness();
        Guid a = Song(h, "a"), b = Song(h, "b");
        var changed = new List<string?>();
        h.Shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        h.Shell.Timeline.Click(TimelineTrack.AudioMusic, a, ctrl: false, shift: false);
        Assert.False(h.Shell.IsAudioMuted);
        Assert.False(h.Shell.IsAudioFadeIn);

        h.Shell.AudioMuteCommand.Execute(null);
        h.Shell.AudioFadeInCommand.Execute(null);
        Assert.True(h.Shell.IsAudioMuted);
        Assert.True(h.Shell.IsAudioFadeIn);
        Assert.False(h.Shell.IsAudioFadeOut);
        Assert.Contains(nameof(h.Shell.IsAudioMuted), changed);

        h.Shell.Timeline.Click(TimelineTrack.AudioMusic, b, ctrl: true, shift: false);
        Assert.False(h.Shell.IsAudioMuted);
        h.Shell.Timeline.Collapse(TimelineTrack.AudioMusic, b);
        Assert.False(h.Shell.IsAudioFadeIn);

        h.Shell.Timeline.Click(TimelineTrack.AudioMusic, a, ctrl: false, shift: false);
        h.Session.Undo.Undo();
        Assert.False(h.Shell.IsAudioFadeIn);
        Assert.True(h.Shell.IsAudioMuted);
    }
}
