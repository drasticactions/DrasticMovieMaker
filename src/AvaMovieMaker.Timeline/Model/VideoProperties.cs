namespace AvaMovieMaker.Timeline.Model;

public sealed record VideoProperties
{
    public int Width { get; init; }

    public int Height { get; init; }

    public long SampleAspectNum { get; init; } = 1;

    public long SampleAspectDen { get; init; } = 1;

    public long FrameRateNum { get; init; } = 30000;

    public long FrameRateDen { get; init; } = 1001;

    public int Rotation { get; init; }

    public bool FlipHorizontal { get; init; }

    public string Codec { get; init => field = value ?? string.Empty; } = string.Empty;

    // The size the video is shown at, after the sample aspect and rotation.
    public (int Width, int Height) DisplaySize
    {
        get
        {
            double sar = SampleAspectNum > 0 && SampleAspectDen > 0 ? SampleAspectNum / (double)SampleAspectDen : 1.0;
            int dw = (int)Math.Round(Width * sar);
            return Rotation is 90 or 270 ? (Height, dw) : (dw, Height);
        }
    }
}
