namespace AvaMovieMaker.Audio.Mixing;

public static class SoftLimiter
{
    public static readonly float Ceiling = MathF.Pow(10, -0.3f / 20);

    public const float Knee = 0.8f;

    public static float Apply(float x)
    {
        float a = MathF.Abs(x);
        if (a <= Knee)
        {
            return x;
        }

        float room = Ceiling - Knee;
        float y = Knee + room * MathF.Tanh((a - Knee) / room);
        return MathF.CopySign(y, x);
    }

    public static void Apply(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Apply(samples[i]);
        }
    }
}
