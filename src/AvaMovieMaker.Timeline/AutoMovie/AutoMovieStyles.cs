namespace AvaMovieMaker.Timeline.AutoMovie;

public static class AutoMovieStyles
{
    public static readonly IReadOnlyList<AutoMovieStyle> All =
    [
        new()
        {
            Id = "fade-and-reveal",
            Name = Strings.AutoMovieStyleFadeAndReveal,
            Description = Strings.AutoMovieStyleFadeAndRevealDescription,
            Transitions = ["fade", "fade", "fade", "fade", "fade", "fade", "fade", "fade", "reveal-right", "reveal-right", "reveal-down"],
            TitleBackground = 0xFF41A66F,
        },
        new()
        {
            Id = "flip-and-slide",
            Name = Strings.AutoMovieStyleFlipAndSlide,
            Description = Strings.AutoMovieStyleFlipAndSlideDescription,
            Transitions =
            [
                "flip", "flip", "flip", "flip", "reveal-right", "reveal-right", "reveal-right", "reveal-right",
                "slide", "slide", "slide", "slide", "page-curl-up-left", "page-curl-up-right", "spin", "shatter-in",
            ],
            TitleBackground = 0xFF416FA6,
            MusicLevel = 40,
            AlwaysChronological = true,
        },
        new()
        {
            Id = "highlights-movie",
            Name = Strings.AutoMovieStyleHighlightsMovie,
            Description = Strings.AutoMovieStyleHighlightsMovieDescription,
            Transitions = [null, "fade", "fade", "fade", "fade", "fade", "reveal-right"],
        },
        new()
        {
            Id = "music-video",
            Name = Strings.AutoMovieStyleMusicVideo,
            Description = Strings.AutoMovieStyleMusicVideoDescription,
            Transitions = [null, null, null, null, "fade", "fade", "fade", "fade"],
            TitleAnimation = null,
            CreditsAnimation = null,
            MusicLevel = 100,
            MusicVideo = true,
        },
        new()
        {
            Id = "old-movie",
            Name = Strings.AutoMovieStyleOldMovie,
            Description = Strings.AutoMovieStyleOldMovieDescription,
            Transitions = [null, null, "fade"],
            Effects = ["film-age-old", "film-age-older"],
            QualityWeight = 1.0,
            EndCard = true,
        },
        new()
        {
            Id = "sports-highlights",
            Name = Strings.AutoMovieStyleSportsHighlights,
            Description = Strings.AutoMovieStyleSportsHighlightsDescription,
            Transitions = [null, null, "fade", "wipe-wide-right"],
            TitleAnimation = "exploding-outline",
            CreditsAnimation = "credits-exploding",
            TitleBackground = 0xFF3D7756,
            QualityWeight = 0.2,
            AllowShaky = true,
            Scoreboard = true,
        },
    ];

    public static AutoMovieStyle Default => All[1];

    public static AutoMovieStyle Get(string id) => All.FirstOrDefault(s => s.Id == id) ?? Default;
}
