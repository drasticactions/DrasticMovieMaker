using AvaMovieMaker.Media.FFmpeg;

namespace AvaMovieMaker.Media.Tests;

internal static class MediaFixture
{
    public static void Load()
    {
        if (FFmpegRuntime.Check() is { } error)
        {
            Assert.Fail(error);
        }
    }
}
