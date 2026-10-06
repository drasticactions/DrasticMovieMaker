using System.Globalization;

namespace AvaMovieMaker.Time;

public static class TimeFormat
{
    public static string Format(MediaTime time)
    {
        long ticks = time.Ticks;
        string sign = ticks < 0 ? "-" : string.Empty;
        ticks = Math.Abs(ticks);

        const long tick100 = TimeSpan.TicksPerMillisecond * 10;
        long hundredths = (ticks + tick100 / 2) / tick100;
        long h = hundredths / 360000;
        long m = hundredths / 6000 % 60;
        long s = hundredths / 100 % 60;
        long f = hundredths % 100;
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{h}:{m:00}:{s:00}.{f:00}");
    }

    public static string FormatPair(MediaTime position, MediaTime duration) => $"{Format(position)} / {Format(duration)}";

    public static bool TryParse(string text, out MediaTime time)
    {
        time = MediaTime.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Trim().Split(':');
        if (parts.Length > 3)
        {
            return false;
        }

        double total = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v < 0)
            {
                return false;
            }

            total = total * 60 + v;
        }

        time = MediaTime.FromSeconds(total);
        return true;
    }
}
