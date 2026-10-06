namespace AvaMovieMaker.Audio.Native;

public sealed class MiniAudioBackend : IAudioBackend
{
    public bool IsAvailable => MiniAudioContext.TryGet(out _);

    public IReadOnlyList<AudioDeviceInfo> PlaybackDevices => MiniAudioContext.List(capture: false);

    public IReadOnlyList<AudioDeviceInfo> CaptureDevices => MiniAudioContext.List(capture: true);

    public IAudioDevice? OpenPlayback(string deviceName, int sampleRate, int channels, AudioPullHandler pull) =>
        NativeDevice.OpenPlayback(AudioSystem.IndexOf(deviceName, capture: false), sampleRate, channels, span => pull(span));
}
