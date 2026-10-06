using System.Globalization;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Export;

public sealed record PublishProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public ContainerFormat Container { get; init; }

    public int Height { get; init; }

    public int Width4x3 { get; init; }

    public int Width16x9 { get; init; }

    public int Crf { get; init; }

    public long VideoBitrate { get; init; }

    public string Preset { get; init; } = "medium";

    public int CpuUsed { get; init; } = 2;

    public int AudioBitrate { get; init; }

    public long EstimatedVideoBitrate { get; init; }

    public bool MatchSource { get; init; }

    public bool Anamorphic { get; init; }

    public string FileTypeName => Container == ContainerFormat.WebM ? "WebM (VP9, Opus)" : "MP4 (H.264, AAC)";

    public (int Width, int Height, Rational PixelAspect) Size(ProjectSettings settings, (int Width, int Height)? largestSource = null)
    {
        bool wide = settings.Aspect == AspectRatio.Widescreen16x9;
        if (Anamorphic)
        {
            int h = settings.Format == VideoFormat.Pal ? 576 : 480;
            Rational par = settings.Format == VideoFormat.Pal
                ? (wide ? new Rational(64, 45) : new Rational(16, 15))
                : (wide ? new Rational(32, 27) : new Rational(8, 9));
            return (720, h, par);
        }

        int height = Height;
        if (MatchSource && largestSource is { } src)
        {
            height = Math.Clamp(src.Height, 240, 1080);
        }

        int width = wide ? (Width16x9 > 0 && !MatchSource ? Width16x9 : Even(height * 16.0 / 9)) : (Width4x3 > 0 && !MatchSource ? Width4x3 : Even(height * 4.0 / 3));
        return (width, Even(height), new Rational(1, 1));
    }

    private static int Even(double v) => (int)Math.Round(v / 2) * 2;

    public EncoderSettings Encoder(ProjectSettings project, bool hardware, IReadOnlyDictionary<string, string> metadata, (int, int)? largestSource = null, Rational? frameRate = null)
    {
        (int w, int h, Rational par) = Size(project, largestSource);
        return new EncoderSettings
        {
            Container = Container,
            Width = w,
            Height = h,
            SampleAspect = par,
            FrameRate = frameRate ?? project.FrameRate,
            Crf = Crf,
            VideoBitrate = VideoBitrate,
            Preset = Preset,
            CpuUsed = CpuUsed,
            HardwareEncode = hardware && Container == ContainerFormat.Mp4,
            AudioBitrate = AudioBitrate,
            Metadata = metadata,
        };
    }

    public string DisplayName =>
        string.Format(CultureInfo.CurrentCulture, Strings.PublishProfileDisplayName, Name, FormatBitrate(EstimatedVideoBitrate + AudioBitrate));

    public static string FormatBitrate(long bps) => bps >= 1_000_000
        ? string.Format(CultureInfo.CurrentCulture, Strings.BitrateMbps, bps / 1_000_000.0)
        : string.Format(CultureInfo.CurrentCulture, Strings.BitrateKbps, bps / 1000);

    public static PublishProfile CompressTo(long targetBytes, MediaTime duration)
    {
        double seconds = Math.Max(1, duration.Seconds);
        int audio = 128_000;
        long video = Math.Max(100_000, (long)(targetBytes * 8 * 0.95 / seconds) - audio);
        return PublishProfiles.Recommended with
        {
            Id = "compress",
            Name = Strings.PublishProfileCompressTo,
            AudioBitrate = audio,
            VideoBitrate = video,
            EstimatedVideoBitrate = video,
            Height = video < 1_500_000 ? 480 : 720,
            MatchSource = false,
        };
    }

    public long EstimateBytes(MediaTime duration) => (long)((EstimatedVideoBitrate + AudioBitrate) / 8.0 * duration.Seconds);
}
