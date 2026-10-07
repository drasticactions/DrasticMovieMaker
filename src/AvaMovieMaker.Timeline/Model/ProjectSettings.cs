using System.Text.Json.Serialization;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed record ProjectSettings
{
    public static readonly MediaTime MinPicture = MediaTime.FromSeconds(0.125);
    public static readonly MediaTime MaxPicture = MediaTime.FromSeconds(30);
    public static readonly MediaTime MinTransition = MediaTime.FromSeconds(0.25);
    public static readonly MediaTime MaxTransition = MediaTime.FromSeconds(5);

    public MediaTime PictureDuration { get; init; } = MediaTime.FromSeconds(5);

    public MediaTime TransitionDuration { get; init; } = MediaTime.FromSeconds(1.25);

    public AspectRatio Aspect { get; init; } = AspectRatio.Standard4x3;

    public VideoFormat Format { get; init; } = VideoFormat.Ntsc;

    [JsonIgnore]
    public Rational FrameRate => Format == VideoFormat.Pal ? Rational.Pal : Rational.Ntsc;

    [JsonIgnore]
    public double DisplayAspect => AspectRatios.Value(Aspect);

    [JsonIgnore]
    public (int Width, int Height) PreviewSize => AspectRatios.SizeForShortSide(Aspect, 480);

    public ProjectSettings Clamped() => this with
    {
        PictureDuration = PictureDuration.Clamp(MinPicture, MaxPicture),
        TransitionDuration = TransitionDuration.Clamp(MinTransition, MaxTransition),
    };
}
