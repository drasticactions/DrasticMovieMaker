using AvaMovieMaker.Time;

namespace AvaMovieMaker.Audio.Playback;

public interface IPlaybackClock
{
    MediaTime Position { get; }

    bool IsRunning { get; }
}
