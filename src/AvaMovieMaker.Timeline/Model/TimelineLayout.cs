using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed class TimelineLayout
{
    private TimelineLayout(MediaTime[] starts, MediaTime[] lengths, MediaTime[] transitions)
    {
        Starts = starts;
        Lengths = lengths;
        Transitions = transitions;
    }

    public IReadOnlyList<MediaTime> Starts { get; }

    public IReadOnlyList<MediaTime> Lengths { get; }

    public IReadOnlyList<MediaTime> Transitions { get; }

    public int Count => Starts.Count;

    public MediaTime VideoEnd => Count == 0 ? MediaTime.Zero : Starts[^1] + Lengths[^1];

    public MediaTime End(int i) => Starts[i] + Lengths[i];

    public static TimelineLayout Compute(Project project) => Compute(project.VideoTrack);

    public static TimelineLayout Compute(IReadOnlyList<VideoClip> clips)
    {
        int n = clips.Count;
        var starts = new MediaTime[n];
        var lengths = new MediaTime[n];
        var trans = new MediaTime[n];
        MediaTime t = MediaTime.Zero;
        for (int i = 0; i < n; i++)
        {
            lengths[i] = MediaTime.Max(MediaTime.Zero, clips[i].Length);
            if (i > 0 && clips[i].TransitionIn is { } tr)
            {
                MediaTime available = lengths[i - 1] - trans[i - 1];
                trans[i] = MediaTime.Max(MediaTime.Zero, MediaTime.Min(tr.Duration, MediaTime.Min(available, lengths[i])));
            }

            starts[i] = t - trans[i];
            t = starts[i] + lengths[i];
        }

        return new TimelineLayout(starts, lengths, trans);
    }

    public int IndexAt(MediaTime time)
    {
        for (int i = Count - 1; i >= 0; i--)
        {
            if (time >= Starts[i] && time < End(i))
            {
                return i;
            }
        }

        return -1;
    }

    public int InsertIndexAt(MediaTime time)
    {
        for (int i = 0; i < Count; i++)
        {
            if (time < Starts[i] + Lengths[i] / 2)
            {
                return i;
            }
        }

        return Count;
    }
}
