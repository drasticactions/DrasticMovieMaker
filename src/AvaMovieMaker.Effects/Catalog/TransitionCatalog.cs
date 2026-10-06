namespace AvaMovieMaker.Effects.Catalog;

public static class TransitionCatalog
{
    public const string FallbackId = "fade";

    public static readonly IReadOnlyList<TransitionInfo> All =
    [
        Dissolve("bars-horizontal", nameof(Strings.TransitionBarsHorizontal), "Bars", "bars-horizontal"),
        Dissolve("bars-vertical", nameof(Strings.TransitionBarsVertical), "Dissolve, Vertical", "bars-vertical"),
        Wipe("bow-tie-horizontal", nameof(Strings.TransitionBowTieHorizontal), "Bow Tie, Horizontal", WipeShape.BowTieHorizontal, 0.1f),
        Wipe("bow-tie-vertical", nameof(Strings.TransitionBowTieVertical), "Bow Tie, Vertical", WipeShape.BowTieVertical, 0.1f),
        Wipe("checkerboard-across", nameof(Strings.TransitionCheckerboardAcross), "Checkerboard, Across", WipeShape.Checkerboard, 0) with { Columns = 6, Rows = 10 },
        Wipe("circle", nameof(Strings.TransitionCircle), "Circle", WipeShape.Circle, 0.1f),
        Wipe("circles", nameof(Strings.TransitionCircles), "Circles", WipeShape.Circles, 0.1f) with { Count = 8 },
        Wipe("diagonal-box-out", nameof(Strings.TransitionDiagonalBoxOut), "Diagonal, Box Out", WipeShape.Diamond, 0.1f),
        Wipe("diagonal-cross-out", nameof(Strings.TransitionDiagonalCrossOut), "Diagonal, Cross Out", WipeShape.DiagonalCross, 0.1f),
        Wipe("diagonal-down-right", nameof(Strings.TransitionDiagonalDownRight), "Diagonal, Down Right", WipeShape.Diagonal, 0.4f),
        Wipe("diamond", nameof(Strings.TransitionDiamond), "Diamond", WipeShape.Diamond, 0),
        Dissolve("dissolve", nameof(Strings.TransitionDissolve), "Dissolve", "fine"),
        Dissolve("dissolve-rough", nameof(Strings.TransitionDissolveRough), "Dissolve, Rough", "rough"),
        Wipe("eye", nameof(Strings.TransitionEye), "Eye", WipeShape.Eye, 0.1f),
        new() { Id = "fade", NameKey = nameof(Strings.TransitionFade), MswmmId = "Fade", Family = TransitionFamily.Pixelate, MaxBlock = 1 },
        Wipe("fan-in", nameof(Strings.TransitionFanIn), "Fan, In", WipeShape.FanIn, 0.1f),
        Wipe("fan-out", nameof(Strings.TransitionFanOut), "Fan, Out", WipeShape.FanOut, 0.1f),
        Wipe("fan-up", nameof(Strings.TransitionFanUp), "Fan, In, Vertical", WipeShape.FanUp, 0.1f),
        Wipe("filled-v-down", nameof(Strings.TransitionFilledVDown), "Filled V, Down", WipeShape.FilledV, 0.1f),
        Wipe("filled-v-left", nameof(Strings.TransitionFilledVLeft), "Filled V, Left", WipeShape.FilledV, 0.1f) with { Variant = "left" },
        Wipe("filled-v-right", nameof(Strings.TransitionFilledVRight), "Filled V, Right", WipeShape.FilledV, 0.1f) with { Variant = "right" },
        Wipe("filled-v-up", nameof(Strings.TransitionFilledVUp), "Filled V, Up", WipeShape.FilledV, 0.1f) with { FlipY = true },
        Plane("flip", nameof(Strings.TransitionFlip), "Flip, Right", "flip"),
        Wipe("heart", nameof(Strings.TransitionHeart), "Heart", WipeShape.Heart, 0.1f),
        Wipe("inset-down-left", nameof(Strings.TransitionInsetDownLeft), "Inset, Down Left", WipeShape.Inset, 0) with { FlipX = true },
        Wipe("inset-down-right", nameof(Strings.TransitionInsetDownRight), "Inset, Down Right", WipeShape.Inset, 0),
        Wipe("inset-up-left", nameof(Strings.TransitionInsetUpLeft), "Inset, Up Left", WipeShape.Inset, 0) with { FlipX = true, FlipY = true },
        Wipe("inset-up-right", nameof(Strings.TransitionInsetUpRight), "Inset, Up Right", WipeShape.Inset, 0) with { FlipY = true },
        Wipe("iris", nameof(Strings.TransitionIris), "Iris", WipeShape.Iris, 0),
        Wipe("keyhole", nameof(Strings.TransitionKeyhole), "Keyhole", WipeShape.Keyhole, 0.1f),
        Plane("page-curl-up-left", nameof(Strings.TransitionPageCurlUpLeft), "Page Curl, Up Left", "curl-left"),
        Plane("page-curl-up-right", nameof(Strings.TransitionPageCurlUpRight), "Page Curl, Up Right", "curl-right"),
        new() { Id = "pixelate", NameKey = nameof(Strings.TransitionPixelate), MswmmId = "Pixelate Transition", Family = TransitionFamily.Pixelate, MaxBlock = 50 },
        Wipe("rectangle", nameof(Strings.TransitionRectangle), "Rectangle", WipeShape.Box, 0),
        Wipe("reveal-down", nameof(Strings.TransitionRevealDown), "Reveal, Down", WipeShape.Vertical, 0),
        Wipe("reveal-right", nameof(Strings.TransitionRevealRight), "Reveal, Right", WipeShape.Horizontal, 0),
        Plane("roll", nameof(Strings.TransitionRoll), "Roll", "roll"),
        Particles("shatter-in", nameof(Strings.TransitionShatterIn), "Shatter, In", "in", 600),
        Particles("shatter-right", nameof(Strings.TransitionShatterRight), "Shatter, Right", "right", 200),
        Particles("shatter-up-left", nameof(Strings.TransitionShatterUpLeft), "Shatter, Up Left", "up-left", 64),
        Particles("shatter-up-right", nameof(Strings.TransitionShatterUpRight), "Shatter, Up Right", "up-right", 700),
        Plane("shrink-in", nameof(Strings.TransitionShrinkIn), "Shrink, In", "shrink"),
        Plane("slide", nameof(Strings.TransitionSlide), "Slide", "slide"),
        Plane("slide-up-center", nameof(Strings.TransitionSlideUpCenter), "Slide, Up Center", "slide-center"),
        Plane("spin", nameof(Strings.TransitionSpin), "Spin", "spin"),
        Wipe("split-horizontal", nameof(Strings.TransitionSplitHorizontal), "Split, Horizontal", WipeShape.BarnDoorHorizontal, 0),
        Wipe("split-vertical", nameof(Strings.TransitionSplitVertical), "Split, Vertical", WipeShape.BarnDoorVertical, 0),
        Wipe("star-5-points", nameof(Strings.TransitionStar5Points), "Star, 5 Points", WipeShape.Star, 0.1f) with { Count = 5 },
        Wipe("stars-5-points", nameof(Strings.TransitionStars5Points), "Stars, 5 Points", WipeShape.Stars, 0.1f) with { Count = 5, Columns = 3, Rows = 5 },
        Wipe("sweep-in", nameof(Strings.TransitionSweepIn), "Sweep, In", WipeShape.SweepIn, 0.1f),
        Wipe("sweep-out", nameof(Strings.TransitionSweepOut), "Sweep, Out", WipeShape.SweepOut, 0.1f),
        Wipe("sweep-up", nameof(Strings.TransitionSweepUp), "Swinging Door, Bottom", WipeShape.SweepUp, 0.1f),
        Wipe("wheel-4-spokes", nameof(Strings.TransitionWheel4Spokes), "Wheel, 4 Spokes", WipeShape.Wheel, 0.05f) with { Count = 4 },
        Particles("whirlwind", nameof(Strings.TransitionWhirlwind), "Whirlwind", "whirl", 200),
        Particles("whirlwind-from-top", nameof(Strings.TransitionWhirlwindFromTop), "Whirlwind from top", "whirl-top", 200),
        Wipe("wipe-narrow-down", nameof(Strings.TransitionWipeNarrowDown), "Wipe, Narrow Down", WipeShape.Vertical, 0.2f),
        Wipe("wipe-narrow-right", nameof(Strings.TransitionWipeNarrowRight), "Wipe, Narrow Right", WipeShape.Horizontal, 0.2f),
        Wipe("wipe-normal-down", nameof(Strings.TransitionWipeNormalDown), "Wipe, Normal Down", WipeShape.Vertical, 0.4f),
        Wipe("wipe-normal-right", nameof(Strings.TransitionWipeNormalRight), "Wipe, Normal Right", WipeShape.Horizontal, 0.4f),
        Wipe("wipe-wide-down", nameof(Strings.TransitionWipeWideDown), "Wipe, Wide Down", WipeShape.Vertical, 0.6f),
        Wipe("wipe-wide-right", nameof(Strings.TransitionWipeWideRight), "Wipe, Wide Right", WipeShape.Horizontal, 0.6f),
        Wipe("zig-zag-horizontal", nameof(Strings.TransitionZigZagHorizontal), "Zig Zag, Horizontal", WipeShape.ZigZagHorizontal, 0.1f) with { Count = 9 },
        Wipe("zig-zag-vertical", nameof(Strings.TransitionZigZagVertical), "Zig Zag, Vertical", WipeShape.ZigZagVertical, 0.1f) with { Count = 9 },
    ];

    private static readonly Dictionary<string, TransitionInfo> ById = All.ToDictionary(t => t.Id, StringComparer.Ordinal);
    private static readonly Dictionary<string, TransitionInfo> ByMswmmId = All.ToDictionary(t => t.MswmmId, StringComparer.OrdinalIgnoreCase);

    public static TransitionInfo? Find(string id) => ById.GetValueOrDefault(id);

    public static TransitionInfo Get(string id) => ById.GetValueOrDefault(id) ?? ById[FallbackId];

    public static TransitionInfo? FindMswmmId(string mswmmId) => ByMswmmId.GetValueOrDefault(mswmmId.Trim());

    private static TransitionInfo Wipe(string id, string nameKey, string mswmmId, WipeShape shape, float soft) =>
        new() { Id = id, NameKey = nameKey, MswmmId = mswmmId, Family = TransitionFamily.Wipe, Shape = shape, Softness = soft };

    private static TransitionInfo Dissolve(string id, string nameKey, string mswmmId, string variant) =>
        new() { Id = id, NameKey = nameKey, MswmmId = mswmmId, Family = TransitionFamily.Dissolve, Variant = variant };

    private static TransitionInfo Plane(string id, string nameKey, string mswmmId, string variant) =>
        new() { Id = id, NameKey = nameKey, MswmmId = mswmmId, Family = TransitionFamily.Plane, Variant = variant };

    private static TransitionInfo Particles(string id, string nameKey, string mswmmId, string variant, int count) =>
        new() { Id = id, NameKey = nameKey, MswmmId = mswmmId, Family = TransitionFamily.Particles, Variant = variant, Particles = count };
}
