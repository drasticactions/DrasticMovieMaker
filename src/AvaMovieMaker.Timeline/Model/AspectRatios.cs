namespace AvaMovieMaker.Timeline.Model;

public static class AspectRatios
{
    public static IReadOnlyList<AspectRatio> All { get; } =
    [
        AspectRatio.Standard4x3,
        AspectRatio.Widescreen16x9,
        AspectRatio.Vertical9x16,
        AspectRatio.Square1x1,
        AspectRatio.Portrait4x5,
    ];

    public static (int Num, int Den) Ratio(AspectRatio aspect) => aspect switch
    {
        AspectRatio.Widescreen16x9 => (16, 9),
        AspectRatio.Vertical9x16 => (9, 16),
        AspectRatio.Square1x1 => (1, 1),
        AspectRatio.Portrait4x5 => (4, 5),
        _ => (4, 3),
    };

    public static double Value(AspectRatio aspect)
    {
        (int num, int den) = Ratio(aspect);
        return num / (double)den;
    }

    public static bool IsPortrait(AspectRatio aspect)
    {
        (int num, int den) = Ratio(aspect);
        return num < den;
    }

    public static bool HasDvd(AspectRatio aspect) => aspect is AspectRatio.Standard4x3 or AspectRatio.Widescreen16x9;

    public static string Label(AspectRatio aspect) => aspect switch
    {
        AspectRatio.Widescreen16x9 => "16:9",
        AspectRatio.Vertical9x16 => "9:16",
        AspectRatio.Square1x1 => "1:1",
        AspectRatio.Portrait4x5 => "4:5",
        _ => "4:3",
    };

    public static string Id(AspectRatio aspect) => Label(aspect);

    public static bool TryParse(string? id, out AspectRatio aspect)
    {
        foreach (AspectRatio a in All)
        {
            if (string.Equals(Id(a), id?.Trim(), StringComparison.Ordinal))
            {
                aspect = a;
                return true;
            }
        }

        aspect = AspectRatio.Standard4x3;
        return false;
    }

    public static (int Width, int Height) SizeForShortSide(AspectRatio aspect, int shortSide)
    {
        (int num, int den) = Ratio(aspect);
        int longSide = Even(shortSide * (double)Math.Max(num, den) / Math.Min(num, den));
        return num >= den ? (longSide, shortSide) : (shortSide, longSide);
    }

    internal static int Even(double v) => (int)Math.Round(v / 2) * 2;
}
