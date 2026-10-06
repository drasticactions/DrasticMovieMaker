namespace AvaMovieMaker.Audio;

public delegate void AudioPullHandler(Span<float> output);

public interface IAudioBackend
{
    bool IsAvailable { get; }

    IReadOnlyList<AudioDeviceInfo> PlaybackDevices { get; }

    IReadOnlyList<AudioDeviceInfo> CaptureDevices { get; }

    IAudioDevice? OpenPlayback(string deviceName, int sampleRate, int channels, AudioPullHandler pull);
}

public interface IAudioDevice : IDisposable
{
    int LatencyFrames { get; }

    bool Start();

    bool Stop();
}
