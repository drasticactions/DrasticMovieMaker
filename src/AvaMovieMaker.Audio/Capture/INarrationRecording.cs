using AvaMovieMaker.Time;

namespace AvaMovieMaker.Audio.Capture;

public interface INarrationRecording : IDisposable
{
    string Path { get; }

    MediaTime Duration { get; }

    float Level { get; }

    float Gain { get; set; }

    event EventHandler? LimitReached;

    MediaTime Stop();
}
