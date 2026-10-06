using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Encoders;

public sealed record EncoderSettings
{
    public ContainerFormat Container { get; init; } = ContainerFormat.Mp4;

    public int Width { get; init; } = 1280;

    public int Height { get; init; } = 720;

    public Rational FrameRate { get; init; } = Rational.Ntsc;

    public Rational SampleAspect { get; init; } = new(1, 1);

    public int Crf { get; init; } = 20;

    public long VideoBitrate { get; init; }

    public string Preset { get; init; } = "medium";

    public int CpuUsed { get; init; } = 2;

    public bool HardwareEncode { get; init; }

    public int AudioBitrate { get; init; } = 192_000;

    public bool IncludeAudio { get; init; } = true;

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public static string HardwareH264Encoder { get; } =
        OperatingSystem.IsMacOS() ? "h264_videotoolbox" : OperatingSystem.IsWindows() ? "h264_mf" : "h264_vaapi";

    public string VideoCodecName => Container switch
    {
        ContainerFormat.WebM => "libvpx-vp9",
        ContainerFormat.MkvLossless => "ffv1",
        _ => HardwareEncode ? HardwareH264Encoder : "libx264",
    };

    public string AudioCodecName => Container switch
    {
        ContainerFormat.WebM => "libopus",
        ContainerFormat.MkvLossless => "pcm_s16le",
        _ => "aac",
    };

    public string Extension => Container switch
    {
        ContainerFormat.WebM => ".webm",
        ContainerFormat.MkvLossless => ".mkv",
        _ => ".mp4",
    };
}
