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

    public string Codec { get; init; } = string.Empty;
}
