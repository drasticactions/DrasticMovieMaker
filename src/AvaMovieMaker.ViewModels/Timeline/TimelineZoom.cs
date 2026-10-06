namespace AvaMovieMaker.ViewModels.Timeline;

public static class TimelineZoom
{
    public static readonly double[] PixelsPerSecond = [0.2, 1.0 / 3, 0.4, 2.0 / 3, 1, 2, 4, 8, 16, 32, 64, 128, 256];

    public const int DefaultStep = 6;

    public const double FitShare = 0.8;

    public static int Clamp(int step) => Math.Clamp(step, 0, PixelsPerSecond.Length - 1);

    public static int Fit(double seconds, double width, int current = DefaultStep)
    {
        if (seconds <= 0 || width <= 0)
        {
            return current;
        }

        for (int i = PixelsPerSecond.Length - 1; i > 0; i--)
        {
            if (seconds * PixelsPerSecond[i] <= width * FitShare)
            {
                return i;
            }
        }

        return 0;
    }

    public static int FramesPerPixel(int step, double frameRate) =>
        Math.Max(1, (int)Math.Floor(frameRate / PixelsPerSecond[Clamp(step)] + 1e-9));
}
