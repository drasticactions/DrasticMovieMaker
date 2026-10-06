namespace AvaMovieMaker.Effects.Catalog;

public static class TitleAnimationCatalog
{
    public const string DefaultTitle = "fade-in-and-out";
    public const string DefaultCredits = "credits-scroll-up-stacked";

    public const double BaseDuration = 3.5;

    public static readonly IReadOnlyList<TitleAnimationInfo> All =
    [
        T("basic-title", Strings.TitleAnimationBasicTitle, "Basic Title", TitleGroup.OneLine, "static", Strings.TitleAnimationBasicTitleDescription, 0, 0),
        T("fade-bounce-wipe", Strings.TitleAnimationFadeBounceWipe, "Fade, Bounce Wipe", TitleGroup.OneLine, "bounce-wipe", Strings.TitleAnimationFadeBounceWipeDescription, 0.5, 0.5, 2.0),
        T("fade-ellipse-wipe", Strings.TitleAnimationFadeEllipseWipe, "Fade, Ellipse Wipe", TitleGroup.OneLine, "ellipse-wipe", Strings.TitleAnimationFadeEllipseWipeDescription, 1, 1),
        T("fade-slow-zoom", Strings.TitleAnimationFadeSlowZoom, "Fade, Slow Zoom", TitleGroup.OneLine, "slow-zoom", Strings.TitleAnimationFadeSlowZoomDescription, 1, 1),
        T("fade-wipe", Strings.TitleAnimationFadeWipe, "Fade, Wipe", TitleGroup.OneLine, "fade-wipe", Strings.TitleAnimationFadeWipeDescription, 1, 1),
        T("flashing", Strings.TitleAnimationFlashing, "Flashing", TitleGroup.OneLine, "flashing", Strings.TitleAnimationFlashingDescription, 0, 0),
        T("fly-in-top-left", Strings.TitleAnimationFlyInTopLeft, "Fly In, Top Left", TitleGroup.OneLine, "fly-top-left", Strings.TitleAnimationFlyInTopLeftDescription, 1, 1),
        T("mirror", Strings.TitleAnimationMirror, "Mirror", TitleGroup.OneLine, "mirror", Strings.TitleAnimationMirrorDescription, 1, 1),
        T("news-banner", Strings.TitleAnimationNewsBanner, "News Banner", TitleGroup.OneLine, "news-banner", Strings.TitleAnimationNewsBannerDescription, 1, 1),
        T("paint-drip", Strings.TitleAnimationPaintDrip, "Paint Drip", TitleGroup.OneLine, "paint-drip", Strings.TitleAnimationPaintDripDescription, 1, 1, 3.0),
        T("scroll-banner", Strings.TitleAnimationScrollBanner, "Scroll, Banner", TitleGroup.OneLine, "scroll-banner", Strings.TitleAnimationScrollBannerDescription, 0.5, 0.5, 8.0),
        T("scroll-inverted", Strings.TitleAnimationScrollInverted, "Scroll, Inverted", TitleGroup.OneLine, "scroll-inverted", Strings.TitleAnimationScrollInvertedDescription, 0.5, 0.5, 8.0),
        T("spin-in", Strings.TitleAnimationSpinIn, "Spin, In", TitleGroup.OneLine, "spin-in", Strings.TitleAnimationSpinInDescription, 1, 1),
        T("spin-out", Strings.TitleAnimationSpinOut, "Spin, Out", TitleGroup.OneLine, "spin-out", Strings.TitleAnimationSpinOutDescription, 1, 1),
        T("stretch", Strings.TitleAnimationStretch, "Stretch", TitleGroup.OneLine, "stretch", Strings.TitleAnimationStretchDescription, 1, 1),
        T("subtitle", Strings.TitleAnimationSubtitle, "Subtitle", TitleGroup.OneLine, "subtitle", Strings.TitleAnimationSubtitleDescription, 0.5, 0.5),
        T("typewriter", Strings.TitleAnimationTypewriter, "Typewriter", TitleGroup.OneLine, "typewriter", Strings.TitleAnimationTypewriterDescription, 0, 1, 2.0),
        T("video-in-text", Strings.TitleAnimationVideoInText, "Video, In Text", TitleGroup.OneLine, "video-in-text", Strings.TitleAnimationVideoInTextDescription, 1, 1),
        T("wow", Strings.TitleAnimationWow, "Wow!", TitleGroup.OneLine, "wow", Strings.TitleAnimationWowDescription, 1, 1),
        T("zoom-in", Strings.TitleAnimationZoomIn, "Zoom, In", TitleGroup.OneLine, "zoom-in", Strings.TitleAnimationZoomInDescription, 1, 1),
        T("zoom-out", Strings.TitleAnimationZoomOut, "Zoom, Out", TitleGroup.OneLine, "zoom-out", Strings.TitleAnimationZoomOutDescription, 1, 1),
        T("zoom-up-and-in", Strings.TitleAnimationZoomUpAndIn, "Zoom, Up and In", TitleGroup.OneLine, "zoom-up", Strings.TitleAnimationZoomUpAndInDescription, 1, 1),
        T("exploding-outline", Strings.TitleAnimationExplodingOutline, "Exploding Outline", TitleGroup.TwoLines, "explode", Strings.TitleAnimationExplodingOutlineDescription, 2, 1) with { Outline = true, Shadow = false },
        T("ticker-tape", Strings.TitleAnimationTickerTape, "Ticker Tape", TitleGroup.OneLine, "ticker", Strings.TitleAnimationTickerTapeDescription, 1, 1, 3.0),

        T("fade-in-and-out", Strings.TitleAnimationFadeInAndOut, "Fade, In and Out", TitleGroup.TwoLines, "fade", Strings.TitleAnimationFadeInAndOutDescription, 1, 1),
        T("fly-in-fades", Strings.TitleAnimationFlyInFades, "Fly In, Fades", TitleGroup.TwoLines, "fly-in-fade", Strings.TitleAnimationFlyInFadesDescription, 0.5, 1),
        T("fly-out", Strings.TitleAnimationFlyOut, "Fly Out", TitleGroup.TwoLines, "fly-out", Strings.TitleAnimationFlyOutDescription, 1, 1),
        T("fly-in-fly-out", Strings.TitleAnimationFlyInFlyOut, "Fly In, Fly Out", TitleGroup.TwoLines, "fly-in-out", Strings.TitleAnimationFlyInFlyOutDescription, 0.5, 0.5),
        T("fly-in-left-and-right", Strings.TitleAnimationFlyInLeftAndRight, "Fly In, Left and Right", TitleGroup.TwoLines, "fly-left-right", Strings.TitleAnimationFlyInLeftAndRightDescription, 0.5, 1),
        T("moving-titles-layered", Strings.TitleAnimationMovingTitlesLayered, "Moving Titles, Layered", TitleGroup.TwoLines, "layered", Strings.TitleAnimationMovingTitlesLayeredDescription, 1, 1),
        T("news-video-inset", Strings.TitleAnimationNewsVideoInset, "News Video, Inset", TitleGroup.OneLine, "news-inset", Strings.TitleAnimationNewsVideoInsetDescription, 1, 1) with { VideoRect = [0.1f, 0.1f, 0.55f, 0.55f] },
        T("newspaper", Strings.TitleAnimationNewspaper, "Newspaper", TitleGroup.TwoLines, "newspaper", Strings.TitleAnimationNewspaperDescription, 1.5, 1, 2.0) with { VideoRect = [0.38f, 0.41f, 0.35f, 0.35f] },
        T("scroll-perspective", Strings.TitleAnimationScrollPerspective, "Scroll, Perspective", TitleGroup.OneLine, "perspective", Strings.TitleAnimationScrollPerspectiveDescription, 1, 1, 3.0),
        T("sports-scoreboard", Strings.TitleAnimationSportsScoreboard, "Sports Scoreboard", TitleGroup.TwoLines, "scoreboard", Strings.TitleAnimationSportsScoreboardDescription, 0.5, 0.5) with { MaxCharacters = 28 },

        T("credits-exploding", Strings.TitleAnimationCreditsExploding, "Credits: Exploding", TitleGroup.Credits, "credits-explode", Strings.TitleAnimationCreditsExplodingDescription, 2, 1) with { Outline = true, Shadow = false, MaxCharacters = 64 },
        T("credits-fade-in-and-out", Strings.TitleAnimationCreditsFadeInAndOut, "Credits: Fade, In and Out", TitleGroup.Credits, "credits-fade", Strings.TitleAnimationCreditsFadeInAndOutDescription, 1, 1) with { MaxCharacters = 64 },
        T("credits-fly-in-left-and-right", Strings.TitleAnimationCreditsFlyInLeftAndRight, "Credits: Fly In, Left and Right", TitleGroup.Credits, "credits-fly", Strings.TitleAnimationCreditsFlyInLeftAndRightDescription, 0.75, 0.75, 1.5) with { MaxCharacters = 48 },
        T("credits-mirror", Strings.TitleAnimationCreditsMirror, "Credits: Mirror", TitleGroup.Credits, "credits-mirror", Strings.TitleAnimationCreditsMirrorDescription, 1.5, 1.5, 1.5) with { MaxCharacters = 64 },
        T("credits-scroll-up-side-by-side", Strings.TitleAnimationCreditsScrollUpSideBySide, "Credits: Scroll, Up Side-by-Side", TitleGroup.Credits, "credits-scroll-side", Strings.TitleAnimationCreditsScrollUpSideBySideDescription, 0, 0, 1.5) with { MaxCharacters = 24 },
        T("credits-scroll-up-stacked", Strings.TitleAnimationCreditsScrollUpStacked, "Credits: Scroll, Up Stacked", TitleGroup.Credits, "credits-scroll", Strings.TitleAnimationCreditsScrollUpStackedDescription, 0, 0, 1.5) with { MaxCharacters = 48 },
        T("credits-video-left", Strings.TitleAnimationCreditsVideoLeft, "Credits: Video Left", TitleGroup.Credits, "credits-scroll", Strings.TitleAnimationCreditsVideoLeftDescription, 0, 0, 2.0) with { VideoRect = [0f, 0f, 0.5f, 1f], MaxCharacters = 24 },
        T("credits-video-top", Strings.TitleAnimationCreditsVideoTop, "Credits: Video Top", TitleGroup.Credits, "credits-scroll", Strings.TitleAnimationCreditsVideoTopDescription, 0, 0, 2.0) with { VideoRect = [0f, 0f, 1f, 0.5f], MaxCharacters = 48 },
        T("credits-zoom-in", Strings.TitleAnimationCreditsZoomIn, "Credits: Zoom, In", TitleGroup.Credits, "credits-zoom", Strings.TitleAnimationCreditsZoomInDescription, 1, 1) with { MaxCharacters = 64 },
    ];

    private static readonly Dictionary<string, TitleAnimationInfo> ById = All.ToDictionary(t => t.Id, StringComparer.Ordinal);
    private static readonly Dictionary<string, TitleAnimationInfo> ByMswmmId = All.ToDictionary(t => t.MswmmId, StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ReferenceOrder =
    [
        "Fade, In and Out",
        "Fly In, Fades",
        "Fly Out",
        "Fly In, Fly Out",
        "Fly In, Top Left",
        "Typewriter",
        "Ticker Tape",
        "News Banner",
        "Scroll, Perspective",
        "Flashing",
        "Zoom, Out",
        "Zoom, In",
        "Spin, In",
        "Spin, Out",
        "News Video, Inset",
        "Moving Titles, Layered",
        "Fade, Slow Zoom",
        "Zoom, Up and In",
        "Stretch",
        "Subtitle",
        "Basic Title",
        "Video, In Text",
        "Wow!",
        "Fade, Wipe",
        "Fade, Bounce Wipe",
        "Fade, Ellipse Wipe",
        "Mirror",
        "Scroll, Banner",
        "Scroll, Inverted",
        "Exploding Outline",
        "Paint Drip",
        "Fly In, Left and Right",
        "Sports Scoreboard",
        "Newspaper",
        "Credits: Zoom, In",
        "Credits: Fade, In and Out",
        "Credits: Scroll, Up Side-by-Side",
        "Credits: Scroll, Up Stacked",
        "Credits: Mirror",
        "Credits: Exploding",
        "Credits: Fly In, Left and Right",
        "Credits: Video Left",
        "Credits: Video Top",
    ];

    public static int ListOrder(TitleAnimationInfo info)
    {
        int i = Array.IndexOf(ReferenceOrder, info.MswmmId);
        return i < 0 ? int.MaxValue : i;
    }

    public static TitleAnimationInfo Get(string id) => ById.GetValueOrDefault(id) ?? ById[DefaultTitle];

    public static TitleAnimationInfo? Find(string id) => ById.GetValueOrDefault(id);

    public static TitleAnimationInfo? FindMswmmId(string mswmmId) => ByMswmmId.GetValueOrDefault(mswmmId.Trim());

    public static double DefaultDuration(string id, int creditRows = 0, int words = 2)
    {
        TitleAnimationInfo a = Get(id);
        if (a.IsCredits)
        {
            return Math.Max(BaseDuration, 2.0 * Math.Max(1, creditRows)) * a.DurationMultiplier;
        }

        double main = (0.73 + 0.25 * Math.Max(1, words)) * a.DurationMultiplier;
        return Math.Max(1.0, a.Entrance + a.Exit + main);
    }

    public static double DefaultDuration(Titles.TitleContent content)
    {
        int words = content.Lines.Sum(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
        return DefaultDuration(content.AnimationId, content.Credits.Count, words);
    }

    private static TitleAnimationInfo T(string id, string name, string mswmmId, TitleGroup group, string kind, string description, double entrance, double exit, double multiplier = 1.0) =>
        new() { Id = id, Name = name, MswmmId = mswmmId, Group = group, Kind = kind, Description = description, Entrance = entrance, Exit = exit, DurationMultiplier = multiplier };
}
