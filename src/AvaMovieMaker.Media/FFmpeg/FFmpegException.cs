namespace AvaMovieMaker.Media.FFmpeg;

public sealed class FFmpegException(string message, int errorCode = 0) : Exception(message)
{
    public int ErrorCode { get; } = errorCode;
}
