using AvaMovieMaker.Media.Probing;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Tests;

public class ProbeTests
{
    public ProbeTests() => MediaFixture.Load();

    [Fact]
    public void ProbesVideo()
    {
        string path = TestMedia.CounterCard(seconds: 2, width: 320, height: 240, audio: true);
        MediaInfo info = MediaProbe.Probe(path);
        Assert.Equal(MediaKind.Video, info.Kind);
        Assert.NotNull(info.Video);
        Assert.Equal(320, info.Video!.Width);
        Assert.Equal(240, info.Video.Height);
        Assert.Equal("h264", info.Video.Codec);
        Assert.Equal(Rational.Ntsc, info.Video.FrameRate);
        Assert.NotNull(info.Audio);
        Assert.Equal(48000, info.Audio!.SampleRate);
        Assert.InRange(info.Duration.Seconds, 1.9, 2.1);
    }

    [Fact]
    public void ProbesAudio()
    {
        MediaInfo info = MediaProbe.Probe(TestMedia.Tone(seconds: 1.5));
        Assert.Equal(MediaKind.Audio, info.Kind);
        Assert.Null(info.Video);
        Assert.Equal(2, info.Audio!.Channels);
        Assert.InRange(info.Duration.Seconds, 1.49, 1.51);
    }

    [Fact]
    public void ProbesPicture()
    {
        MediaInfo info = MediaProbe.Probe(TestMedia.Picture(640, 360, ".jpg"));
        Assert.Equal(MediaKind.Picture, info.Kind);
        Assert.Equal(MediaTime.Zero, info.Duration);
        Assert.Equal((640, 360), info.Video!.DisplaySize);
    }

    [Fact]
    public void ExifOrientationParses()
    {
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8,
            0, 1,
            0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, 6, 0, 0,
            0, 0, 0, 0,
        ];
        byte[] app1 = [.. "Exif\0\0"u8.ToArray(), .. tiff];
        int len = app1.Length + 2;
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE1, (byte)(len >> 8), (byte)len, .. app1, 0xFF, 0xD9];
        ExifReader.ExifData d = ExifReader.Parse(jpeg);
        Assert.Equal(6, d.Orientation);
        Assert.Equal((90, false), ExifReader.Transform(d.Orientation));
    }
}
