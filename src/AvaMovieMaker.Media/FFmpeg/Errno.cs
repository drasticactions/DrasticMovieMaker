namespace AvaMovieMaker.Media.FFmpeg;

internal static class Errno
{
    public static readonly int Again = OperatingSystem.IsMacOS() ? 35 : 11;

    public const int Invalid = 22;

    public const int IO = 5;

    public const int NoEntry = 2;
}
