using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Probing;

public sealed record VideoStreamInfo
{
    public int StreamIndex { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public Rational SampleAspect { get; init; } = new(1, 1);

    public Rational FrameRate { get; init; } = Rational.Ntsc;

    public int Rotation { get; init; }

    public bool FlipHorizontal { get; init; }

    public string Codec { get; init; } = string.Empty;

    public string PixelFormat { get; init; } = string.Empty;

    public bool HasAlpha { get; init; }

    public (int Width, int Height) DisplaySize
    {
        get
        {
            double w = Width * (SampleAspect.IsValid ? SampleAspect.ToDouble() : 1.0);
            int dw = (int)Math.Round(w);
            return Rotation is 90 or 270 ? (Height, dw) : (dw, Height);
        }
    }
}
