using AvaMovieMaker.Rendering.Compositing;

namespace AvaMovieMaker.Effects.Catalog;

public static class EffectCatalog
{
    public const string FillFrame = "fill-frame";
    public const string BlurredBackground = "blurred-background";

    public static readonly IReadOnlyList<EffectInfo> All =
    [
        E("3d-ripple", nameof(Strings.Effect3DRipple), "3D Ripple", EffectFamily.Distort, "ripple"),
        E("blur", nameof(Strings.EffectBlur), "Blur", EffectFamily.Filter, "blur"),
        new() { Id = BlurredBackground, NameKey = nameof(Strings.EffectBlurredBackground), Family = EffectFamily.Framing, Operation = "frame", Values = [2] },
        E("brightness-decrease", nameof(Strings.EffectBrightnessDecrease), "Brightness, Decrease", EffectFamily.Color, "brightness", -1),
        E("brightness-increase", nameof(Strings.EffectBrightnessIncrease), "Brightness, Increase", EffectFamily.Color, "brightness", 1),
        E("ease-in", nameof(Strings.EffectEaseIn), "Ease In", EffectFamily.Geometry, "ease", 1),
        E("ease-out", nameof(Strings.EffectEaseOut), "Ease Out", EffectFamily.Geometry, "ease", -1),
        E("edge-detection", nameof(Strings.EffectEdgeDetection), "Edge Detection", EffectFamily.Filter, "edges"),
        E("fade-in-from-black", nameof(Strings.EffectFadeInFromBlack), "Fade In, From Black", EffectFamily.Color, "fade", 1, 0),
        E("fade-in-from-white", nameof(Strings.EffectFadeInFromWhite), "Fade In, From White", EffectFamily.Color, "fade", 1, 1),
        E("fade-out-to-black", nameof(Strings.EffectFadeOutToBlack), "Fade Out, To Black", EffectFamily.Color, "fade", -1, 0),
        E("fade-out-to-white", nameof(Strings.EffectFadeOutToWhite), "Fade Out, To White", EffectFamily.Color, "fade", -1, 1),
        new() { Id = FillFrame, NameKey = nameof(Strings.EffectFillFrame), Family = EffectFamily.Framing, Operation = "frame", Values = [1] },
        E("film-age-old", nameof(Strings.EffectFilmAgeOld), "Film Age, Old", EffectFamily.Film, "age", 0.25f, 0.05f, 0.10f, 0.05f, 128, 0.00f, 3),
        E("film-age-older", nameof(Strings.EffectFilmAgeOlder), "Film Age, Older", EffectFamily.Film, "age", 0.40f, 0.10f, 0.20f, 0.15f, 7, 0.05f, 5),
        E("film-age-oldest", nameof(Strings.EffectFilmAgeOldest), "Film Age, Oldest", EffectFamily.Film, "age", 1.00f, 0.20f, 0.30f, 0.40f, 5, 0.20f, 7),
        E("film-grain", nameof(Strings.EffectFilmGrain), "Film Grain", EffectFamily.Film, "grain"),
        E("grayscale", nameof(Strings.EffectGrayscale), "Grayscale", EffectFamily.Color, "grayscale"),
        E("hue-cycle", nameof(Strings.EffectHueCycle), "Hue", EffectFamily.Color, "hue"),
        E("mirror-horizontal", nameof(Strings.EffectMirrorHorizontal), "Mirror, Horizontal", EffectFamily.Geometry, "mirror", 1, 0),
        E("mirror-vertical", nameof(Strings.EffectMirrorVertical), "Mirror, Vertical", EffectFamily.Geometry, "mirror", 0, 1),
        E("pan-down-and-zoom-out", nameof(Strings.EffectPanDownAndZoomOut), "Pan, Down and Zoom Out", EffectFamily.Geometry, "pan", 0.5f, 0.33f, 1.5f, 0.5f, 0.5f, 1.0f),
        E("pan-left-to-right", nameof(Strings.EffectPanLeftToRight), "Pan, Left to Right", EffectFamily.Geometry, "pan", 0.33f, 0.5f, 1.5f, 0.67f, 0.5f, 1.5f),
        E("pan-upper-left-to-lower-right", nameof(Strings.EffectPanUpperLeftToLowerRight), "Pan, Upper Left to Lower Right", EffectFamily.Geometry, "pan", 0.33f, 0.33f, 1.5f, 0.67f, 0.67f, 1.5f),
        E("pan-upper-left-to-upper-right", nameof(Strings.EffectPanUpperLeftToUpperRight), "Pan, Upper Left to Upper Right", EffectFamily.Geometry, "pan", 0.33f, 0.33f, 1.5f, 0.67f, 0.33f, 1.5f),
        E("pan-upper-right-to-upper-left", nameof(Strings.EffectPanUpperRightToUpperLeft), "Pan, Upper Right to Upper Left", EffectFamily.Geometry, "pan", 0.67f, 0.33f, 1.5f, 0.33f, 0.33f, 1.5f),
        E("pixelate", nameof(Strings.EffectPixelate), "Pixelate", EffectFamily.Filter, "pixelate"),
        E("posterize", nameof(Strings.EffectPosterize), "Posterize", EffectFamily.Color, "posterize", 4),
        E("rotate-90", nameof(Strings.EffectRotate90), "Rotate 90", EffectFamily.Geometry, "rotate", 90),
        E("rotate-180", nameof(Strings.EffectRotate180), "Rotate 180", EffectFamily.Geometry, "rotate", 180),
        E("rotate-270", nameof(Strings.EffectRotate270), "Rotate 270", EffectFamily.Geometry, "rotate", 270),
        E("sepia-tone", nameof(Strings.EffectSepiaTone), "Sepia Tone", EffectFamily.Color, "sepia"),
        E("sharpen", nameof(Strings.EffectSharpen), "Sharpen", EffectFamily.Filter, "sharpen"),
        new() { Id = "slow-down-half", NameKey = nameof(Strings.EffectSlowDownHalf), MswmmId = "Slow Down, Half", Family = EffectFamily.Speed, Operation = "speed", Speed = 0.5 },
        new() { Id = "speed-up-double", NameKey = nameof(Strings.EffectSpeedUpDouble), MswmmId = "Speed Up, Double", Family = EffectFamily.Speed, Operation = "speed", Speed = 2.0 },
        E("spin-360", nameof(Strings.EffectSpin360), "Spin 360", EffectFamily.Geometry, "spin"),
        E("threshold", nameof(Strings.EffectThreshold), "Threshold", EffectFamily.Color, "threshold"),
        E("warp", nameof(Strings.EffectWarp), "Dream", EffectFamily.Distort, "warp"),
        E("watercolor", nameof(Strings.EffectWatercolor), "Watercolor", EffectFamily.Filter, "watercolor"),
        E("zoom-focus-lower-left", nameof(Strings.EffectZoomFocusLowerLeft), "Zoom, Focus Lower Left", EffectFamily.Geometry, "pan", 0.33f, 0.67f, 1.5f, 0.33f, 0.67f, 1.5f),
        E("zoom-focus-lower-right", nameof(Strings.EffectZoomFocusLowerRight), "Zoom, Focus Lower Right", EffectFamily.Geometry, "pan", 0.67f, 0.67f, 1.5f, 0.67f, 0.67f, 1.5f),
        E("zoom-focus-upper-left", nameof(Strings.EffectZoomFocusUpperLeft), "Zoom, Focus Upper Left", EffectFamily.Geometry, "pan", 0.33f, 0.33f, 1.5f, 0.33f, 0.33f, 1.5f),
        E("zoom-focus-upper-right", nameof(Strings.EffectZoomFocusUpperRight), "Zoom, Focus Upper Right", EffectFamily.Geometry, "pan", 0.67f, 0.33f, 1.5f, 0.67f, 0.33f, 1.5f),
        E("zoom-in-to-lower-left", nameof(Strings.EffectZoomInToLowerLeft), "Zoom In, to Lower Left", EffectFamily.Geometry, "pan", 0.5f, 0.5f, 1.0f, 0.33f, 0.67f, 1.5f),
        E("zoom-in-to-lower-right", nameof(Strings.EffectZoomInToLowerRight), "Zoom In, to Lower Right", EffectFamily.Geometry, "pan", 0.5f, 0.5f, 1.0f, 0.67f, 0.67f, 1.5f),
        E("zoom-in-to-upper-left", nameof(Strings.EffectZoomInToUpperLeft), "Zoom In, to Upper Left", EffectFamily.Geometry, "pan", 0.5f, 0.5f, 1.0f, 0.33f, 0.33f, 1.5f),
        E("zoom-in-to-upper-right", nameof(Strings.EffectZoomInToUpperRight), "Zoom In, to Upper Right", EffectFamily.Geometry, "pan", 0.5f, 0.5f, 1.0f, 0.67f, 0.33f, 1.5f),
        E("zoom-out-from-lower-left", nameof(Strings.EffectZoomOutFromLowerLeft), "Zoom Out, from Lower Left", EffectFamily.Geometry, "pan", 0.33f, 0.67f, 1.5f, 0.5f, 0.5f, 1.0f),
        E("zoom-out-from-lower-right", nameof(Strings.EffectZoomOutFromLowerRight), "Zoom Out, from Lower Right", EffectFamily.Geometry, "pan", 0.67f, 0.67f, 1.5f, 0.5f, 0.5f, 1.0f),
        E("zoom-out-from-upper-left", nameof(Strings.EffectZoomOutFromUpperLeft), "Zoom Out, from Upper Left", EffectFamily.Geometry, "pan", 0.33f, 0.33f, 1.5f, 0.5f, 0.5f, 1.0f),
        E("zoom-out-from-upper-right", nameof(Strings.EffectZoomOutFromUpperRight), "Zoom Out, from Upper Right", EffectFamily.Geometry, "pan", 0.67f, 0.33f, 1.5f, 0.5f, 0.5f, 1.0f),
    ];

    private static readonly Dictionary<string, EffectInfo> ById = All.ToDictionary(e => e.Id, StringComparer.Ordinal);
    private static readonly Dictionary<string, EffectInfo> ByMswmmId = All.Where(e => e.MswmmId.Length > 0).ToDictionary(e => e.MswmmId, StringComparer.OrdinalIgnoreCase);

    public static EffectInfo? Find(string id) => ById.GetValueOrDefault(id);

    public static EffectInfo? FindMswmmId(string mswmmId) => ByMswmmId.GetValueOrDefault(mswmmId.Trim());

    public static double SpeedOf(IEnumerable<string> effectIds)
    {
        double speed = 1.0;
        foreach (string id in effectIds)
        {
            if (Find(id) is { Family: EffectFamily.Speed } e)
            {
                speed *= e.Speed;
            }
        }

        return speed;
    }

    public static bool IsFraming(string id) => Find(id) is { Family: EffectFamily.Framing };

    public static FrameFitMode FitOf(IEnumerable<string> effectIds)
    {
        FrameFitMode mode = FrameFitMode.Fit;
        foreach (string id in effectIds)
        {
            if (Find(id) is { Family: EffectFamily.Framing } e)
            {
                mode = e.Values[0] >= 2 ? FrameFitMode.Blur : FrameFitMode.Fill;
            }
        }

        return mode;
    }

    public static string? EffectFor(FrameFitMode mode) => mode switch
    {
        FrameFitMode.Fill => FillFrame,
        FrameFitMode.Blur => BlurredBackground,
        _ => null,
    };

    public static List<string> WithEffect(IEnumerable<string> effectIds, string added)
    {
        var list = effectIds.ToList();
        int at = list.FindIndex(IsFraming);
        if (IsFraming(added) && at >= 0)
        {
            list.RemoveAll(IsFraming);
            list.Insert(at, added);
            return list;
        }

        list.Add(added);
        return list;
    }

    public static List<string> WithFit(IEnumerable<string> effectIds, FrameFitMode mode)
    {
        var list = effectIds.ToList();
        int at = list.FindIndex(IsFraming);
        list.RemoveAll(IsFraming);
        if (EffectFor(mode) is { } id)
        {
            list.Insert(at >= 0 ? at : list.Count, id);
        }

        return list;
    }

    private static EffectInfo E(string id, string nameKey, string mswmmId, EffectFamily family, string op, params float[] values) =>
        new() { Id = id, NameKey = nameKey, MswmmId = mswmmId, Family = family, Operation = op, Values = values };
}
