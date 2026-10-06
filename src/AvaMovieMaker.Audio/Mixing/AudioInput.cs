namespace AvaMovieMaker.Audio.Mixing;

public sealed record AudioInput
{
    public required string Path { get; init; }

    public long TimelineStart { get; init; }

    public long TimelineEnd { get; init; }

    public long SourceStart { get; init; }

    public double Speed { get; init; } = 1.0;

    public float Volume { get; init; } = 1f;

    public bool Mute { get; init; }

    public long FadeIn { get; init; }

    public long FadeOut { get; init; }

    public AudioGroup Group { get; init; }

    public float GainAt(long t)
    {
        if (Mute || t < TimelineStart || t >= TimelineEnd)
        {
            return 0f;
        }

        float g = Volume;
        if (FadeIn > 0 && t - TimelineStart < FadeIn)
        {
            g *= (t - TimelineStart) / (float)FadeIn;
        }

        if (FadeOut > 0 && TimelineEnd - t <= FadeOut)
        {
            g *= (TimelineEnd - t) / (float)FadeOut;
        }

        return g;
    }
}
