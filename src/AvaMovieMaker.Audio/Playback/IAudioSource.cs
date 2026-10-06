namespace AvaMovieMaker.Audio.Playback;

public interface IAudioSource
{
    void Read(long position, Span<float> dest);
}
