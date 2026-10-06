namespace AvaMovieMaker.Audio.Mixing;

public static class LevelsLaw
{
    public static (float Video, float Music) Gains(double levels)
    {
        levels = Math.Clamp(levels, -1, 1);
        float video = levels <= 0 ? 1f : (float)(1 - levels);
        float music = levels >= 0 ? 1f : (float)(1 + levels);
        return (video, music);
    }
}
