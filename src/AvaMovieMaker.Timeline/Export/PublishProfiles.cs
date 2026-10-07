using AvaMovieMaker.Media.Encoders;

namespace AvaMovieMaker.Timeline.Export;

public static class PublishProfiles
{
    public static readonly PublishProfile Recommended = new()
    {
        Id = "best",
        Name = Strings.PublishProfileBest,
        Container = ContainerFormat.Mp4,
        ShortSide = 720,
        MatchSource = true,
        Crf = 20,
        AudioBitrate = 192_000,
        EstimatedVideoBitrate = 5_000_000,
    };

    public static readonly IReadOnlyList<PublishProfile> All =
    [
        Recommended,
        new() { Id = "hd1080", Name = Strings.PublishProfileHd1080, Container = ContainerFormat.Mp4, ShortSide = 1080, Crf = 20, AudioBitrate = 192_000, EstimatedVideoBitrate = 8_000_000 },
        new() { Id = "hd720", Name = Strings.PublishProfileHd720, Container = ContainerFormat.Mp4, ShortSide = 720, Crf = 21, AudioBitrate = 160_000, EstimatedVideoBitrate = 4_000_000 },
        new() { Id = "dvd", Name = Strings.PublishProfileDvd, Container = ContainerFormat.Mp4, ShortSide = 480, Anamorphic = true, Crf = 20, AudioBitrate = 160_000, EstimatedVideoBitrate = 2_500_000 },
        new() { Id = "web", Name = Strings.PublishProfileWeb, Container = ContainerFormat.Mp4, ShortSide = 480, Crf = 23, AudioBitrate = 128_000, EstimatedVideoBitrate = 1_200_000 },
        new() { Id = "webm1080", Name = Strings.PublishProfileWebM1080, Container = ContainerFormat.WebM, ShortSide = 1080, Crf = 31, CpuUsed = 2, AudioBitrate = 160_000, EstimatedVideoBitrate = 4_000_000 },
        new() { Id = "webm720", Name = Strings.PublishProfileWebM720, Container = ContainerFormat.WebM, ShortSide = 720, Crf = 32, CpuUsed = 3, AudioBitrate = 128_000, EstimatedVideoBitrate = 2_000_000 },
        new() { Id = "webm480", Name = Strings.PublishProfileWebM480, Container = ContainerFormat.WebM, ShortSide = 480, Crf = 33, CpuUsed = 4, AudioBitrate = 96_000, EstimatedVideoBitrate = 1_000_000 },
    ];

    public static PublishProfile Get(string id) => All.FirstOrDefault(p => p.Id == id) ?? Recommended;
}
