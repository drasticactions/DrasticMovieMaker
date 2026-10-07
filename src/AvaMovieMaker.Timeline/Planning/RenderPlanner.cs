using AvaMovieMaker.Audio.Mixing;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Media;
using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Planning;

public static class RenderPlanner
{
    public static readonly MediaTime AudioFadeLength = MediaTime.FromSeconds(1);

    public static RenderPlan Build(Project project)
    {
        TimelineLayout layout = TimelineLayout.Compute(project);
        var spans = new List<VideoSpan>(layout.Count);
        var audio = new List<AudioInput>();
        for (int i = 0; i < layout.Count; i++)
        {
            VideoClip c = project.VideoTrack[i];
            MediaItem? m = c.Kind == VideoClipKind.Title ? null : project.FindMedia(c.MediaId);
            string? path = m is null || m.Missing ? null : m.Path;
            spans.Add(new VideoSpan
            {
                ClipId = c.Id,
                Start = layout.Starts[i],
                Length = layout.Lengths[i],
                Path = path,
                IsPicture = c.Kind == VideoClipKind.Picture,
                Title = c.Kind == VideoClipKind.Title ? c.Title : null,
                SourceIn = c.In,
                Speed = c.Speed,
                Effects = c.Effects.Select(e => e.EffectId).Where(e => !EffectCatalog.IsFraming(e)).ToList(),
                Fit = EffectCatalog.FitOf(c.Effects.Select(e => e.EffectId)),
                TransitionId = c.TransitionIn?.TransitionId,
                TransitionLength = layout.Transitions[i],
                FadeIn = c.VideoFadeIn,
                FadeOut = c.VideoFadeOut,
                Seed = SeedOf(c.Id),
            });

            if (c.Kind == VideoClipKind.Video && path is not null && m!.HasAudio && !c.Audio.Mute)
            {
                MediaTime fadeIn = c.Audio.FadeIn ? MediaTime.Min(AudioFadeLength, layout.Lengths[i] / 2) : MediaTime.Zero;
                fadeIn = MediaTime.Max(fadeIn, layout.Transitions[i]);
                MediaTime fadeOut = c.Audio.FadeOut ? MediaTime.Min(AudioFadeLength, layout.Lengths[i] / 2) : MediaTime.Zero;
                if (i + 1 < layout.Count)
                {
                    fadeOut = MediaTime.Max(fadeOut, layout.Transitions[i + 1]);
                }

                audio.Add(new AudioInput
                {
                    Path = path,
                    TimelineStart = AudioReader.ToSample(layout.Starts[i]),
                    TimelineEnd = AudioReader.ToSample(layout.End(i)),
                    SourceStart = AudioReader.ToSample(c.In),
                    Speed = c.Speed,
                    Volume = (float)c.Audio.Volume,
                    FadeIn = AudioReader.ToSample(fadeIn),
                    FadeOut = AudioReader.ToSample(fadeOut),
                    Group = AudioGroup.Video,
                });
            }
        }

        foreach (AudioClip a in project.AudioMusicTrack)
        {
            MediaItem? m = project.FindMedia(a.MediaId);
            if (m is null || m.Missing || a.Audio.Mute || !(m.HasAudio || m.Kind == MediaKind.Audio))
            {
                continue;
            }

            MediaTime fl = MediaTime.Min(AudioFadeLength, a.Length / 2);
            audio.Add(new AudioInput
            {
                Path = m.Path,
                TimelineStart = AudioReader.ToSample(a.Start),
                TimelineEnd = AudioReader.ToSample(a.End),
                SourceStart = AudioReader.ToSample(a.In),
                Volume = (float)a.Audio.Volume,
                FadeIn = a.Audio.FadeIn ? AudioReader.ToSample(fl) : 0,
                FadeOut = a.Audio.FadeOut ? AudioReader.ToSample(fl) : 0,
                Group = AudioGroup.Music,
            });
        }

        var titles = project.TitleOverlayTrack.Select(t => new TitleSpan(t.Start, t.Duration, t.Content)).ToList();
        MediaTime duration = project.Duration;
        var audioPlan = new AudioPlan(audio, AudioReader.ToSample(duration), project.AudioLevels);
        return new RenderPlan(spans, titles, audioPlan, duration, project.Settings);
    }

    public static RenderPlan ForMedia(MediaItem m, SourceClip? clip, ProjectSettings settings)
    {
        var p = new Project { Settings = settings };
        p.Media.Add(m);
        var editor = new Editing.TimelineEditor(p, new Undo.UndoStack());
        if (m.Kind == MediaKind.Audio)
        {
            p.AudioMusicTrack.Add(editor.NewAudioClip(m, clip, MediaTime.Zero));
        }
        else
        {
            p.VideoTrack.Add(editor.NewVideoClip(m, clip));
        }

        return Build(p);
    }

    public static int SeedOf(Guid id)
    {
        Span<byte> b = stackalloc byte[16];
        id.TryWriteBytes(b);
        int h = 17;
        foreach (byte x in b)
        {
            h = unchecked(h * 31 + x);
        }

        return h & 0x7FFFFFFF;
    }
}
