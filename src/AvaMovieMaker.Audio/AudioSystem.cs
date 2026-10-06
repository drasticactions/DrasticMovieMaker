using AvaMovieMaker.Audio.Native;

namespace AvaMovieMaker.Audio;

public static class AudioSystem
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    public static IAudioBackend Backend { get; set; } = new MiniAudioBackend();

    public static bool IsAvailable => Backend.IsAvailable;

    public static IReadOnlyList<AudioDeviceInfo> PlaybackDevices => Backend.PlaybackDevices;

    public static IReadOnlyList<AudioDeviceInfo> CaptureDevices => Backend.CaptureDevices;

    public static int IndexOf(string name, bool capture)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        foreach (AudioDeviceInfo d in capture ? CaptureDevices : PlaybackDevices)
        {
            if (d.Name == name)
            {
                return d.Index;
            }
        }

        return -1;
    }
}
