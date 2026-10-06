namespace AvaMovieMaker.Timeline.Model;

public sealed record ProjectState(
    IReadOnlyList<MediaItem> Media,
    IReadOnlyList<VideoClip> Video,
    IReadOnlyList<AudioClip> Audio,
    IReadOnlyList<TitleClip> Titles,
    ProjectProperties Properties,
    double AudioLevels,
    int AutoMovieSeed)
{
    public static ProjectState Capture(Project p) => new(
        [.. p.Media], [.. p.VideoTrack], [.. p.AudioMusicTrack], [.. p.TitleOverlayTrack],
        p.Properties, p.AudioLevels, p.AutoMovieSeed);

    public void Restore(Project p)
    {
        p.Media = [.. Media];
        p.VideoTrack = [.. Video];
        p.AudioMusicTrack = [.. Audio];
        p.TitleOverlayTrack = [.. Titles];
        p.Properties = Properties;
        p.AudioLevels = AudioLevels;
        p.AutoMovieSeed = AutoMovieSeed;
    }
}
