namespace AvaMovieMaker.TestSupport;

public static class FrameCounter
{
    public const int Bits = 16;
    public const int StripHeight = 16;

    public static int Read(ReadOnlySpan<byte> bgra, int width, int height, int stride = 0)
    {
        if (stride == 0)
        {
            stride = width * 4;
        }

        int y = Math.Min(height - 1, StripHeight / 2);
        int value = 0;
        for (int b = 0; b < Bits; b++)
        {
            int x = (int)((b + 0.5) * width / Bits);
            int o = y * stride + x * 4;
            int luma = (bgra[o] + bgra[o + 1] * 2 + bgra[o + 2]) / 4;
            if (luma > 128)
            {
                value |= 1 << b;
            }
        }

        return value;
    }
}
