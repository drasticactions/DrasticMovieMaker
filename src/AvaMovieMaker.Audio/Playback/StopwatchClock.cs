using System.Diagnostics;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Audio.Playback;

public sealed class StopwatchClock : IPlaybackClock
{
    private readonly Stopwatch _watch = new();
    private MediaTime _origin;

    public MediaTime Position => _origin + MediaTime.FromTimeSpan(_watch.Elapsed);

    public bool IsRunning => _watch.IsRunning;

    public void Start(MediaTime from)
    {
        _origin = from;
        _watch.Restart();
    }

    public void Stop() => _watch.Stop();
}
