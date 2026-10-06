using AvaMovieMaker.Media.FFmpeg;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Tests;

public class RuntimeTests
{
    public RuntimeTests() => MediaFixture.Load();

    [Theory]
    [InlineData("again")]
    [InlineData("invalid")]
    [InlineData("io")]
    [InlineData("noentry")]
    public void ErrnoMatchesTheCLibraryFFmpegUses(string which)
    {
        (int errno, string[] texts) = which switch
        {
            "again" => (Errno.Again, new[] { "temporarily unavailable", "try again" }),
            "invalid" => (Errno.Invalid, new[] { "invalid argument" }),
            "io" => (Errno.IO, new[] { "i/o error", "input/output error" }),
            _ => (Errno.NoEntry, new[] { "no such file or directory" }),
        };
        string text = FFmpegRuntime.ErrorText(ffmpeg.AVERROR(errno)).ToLowerInvariant();
        Assert.Contains(texts, t => text.Contains(t, StringComparison.Ordinal));
    }

    [Fact]
    public void LibraryNamesFollowTheOS()
    {
        string expected = OperatingSystem.IsWindows() ? "avcodec-63.dll"
            : OperatingSystem.IsMacOS() ? "libavcodec.63.dylib"
            : "libavcodec.so.63";
        Assert.Equal(expected, FFmpegRuntime.FileName("avcodec", 63));
    }

    [Fact]
    public void LoadsTheBundledBuildWhenPresent()
    {
        bool bundled = FFmpegRuntime.BundledFolders().Any(folder =>
            FFmpegRuntime.Libraries.All(l => File.Exists(Path.Combine(folder, FFmpegRuntime.FileName(l.Name, l.Major)))));
        Assert.Equal(bundled ? "bundled" : "system", FFmpegRuntime.Source);
        Assert.Contains($"({FFmpegRuntime.Source})", FFmpegRuntime.VersionText, StringComparison.Ordinal);
    }
}
