using AvaMovieMaker.Audio.Mixing;
using AvaMovieMaker.Rendering.Plan;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Planning;

public sealed class RenderPlan
{
    internal RenderPlan(IReadOnlyList<VideoSpan> video, IReadOnlyList<TitleSpan> titles, AudioPlan audio, MediaTime duration, ProjectSettings settings)
    {
        Video = video;
        Titles = titles;
        Audio = audio;
        Duration = duration;
        Settings = settings;
    }

    public static readonly RenderPlan Empty = new([], [], AudioPlan.Empty, MediaTime.Zero, new ProjectSettings());

    public IReadOnlyList<VideoSpan> Video { get; }

    public IReadOnlyList<TitleSpan> Titles { get; }

    public AudioPlan Audio { get; }

    public MediaTime Duration { get; }

    public ProjectSettings Settings { get; }

    public Rational FrameRate => Settings.FrameRate;

    public FramePlan PlanAt(MediaTime time)
    {
        var titles = new List<OverlayTitle>();
        foreach (TitleSpan t in Titles)
        {
            if (time >= t.Start && time < t.End)
            {
                titles.Add(new OverlayTitle(t.Content, (time - t.Start).Seconds, t.Length.Seconds));
            }
        }

        int i = -1;
        for (int k = Video.Count - 1; k >= 0; k--)
        {
            if (time >= Video[k].Start && time < Video[k].End)
            {
                i = k;
                break;
            }
        }

        if (i < 0)
        {
            return new FramePlan(time, ClipInput.Black, null, null, titles);
        }

        VideoSpan b = Video[i];
        if (i > 0 && b.TransitionId is not null && time < b.Start + b.TransitionLength && b.TransitionLength > MediaTime.Zero)
        {
            VideoSpan a = Video[i - 1];
            double progress = (time - b.Start) / b.TransitionLength;
            return new FramePlan(time, a.InputAt(time), b.InputAt(time),
                new TransitionInstance(b.TransitionId, progress, b.Seed, b.TransitionLength.Seconds), titles);
        }

        return new FramePlan(time, b.InputAt(time), null, null, titles);
    }

    public AudioPlan PlanAudio() => Audio;
}
