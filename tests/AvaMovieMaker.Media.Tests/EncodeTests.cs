using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Media.Probing;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Tests;

public class EncodeTests
{
    public EncodeTests() => MediaFixture.Load();

    [Theory]
    [InlineData(ContainerFormat.Mp4)]
    [InlineData(ContainerFormat.WebM)]
    [InlineData(ContainerFormat.MkvLossless)]
    public void RoundTrip(ContainerFormat container)
    {
        using var temp = new TempFolder();
        var settings = new EncoderSettings
        {
            Container = container,
            Width = 160,
            Height = 120,
            FrameRate = Rational.Pal,
            Crf = 18,
            Preset = "ultrafast",
            CpuUsed = 8,
            Metadata = new Dictionary<string, string> { ["title"] = "Round trip" },
        };
        if (MovieEncoder.MissingEncoder(settings) is { } missing)
        {
            Assert.Skip($"{missing} is not in this FFmpeg build");
        }

        string path = temp.File("out" + settings.Extension);
        Encode(path, settings);

        MediaInfo info = MediaProbe.Probe(path);
        Assert.Equal(160, info.Video!.Width);
        Assert.NotNull(info.Audio);
        Assert.InRange(info.Duration.Seconds, 0.95, 1.1);

        using var dec = new VideoDecoder(path, allowHardware: false);
        using DecodedFrame? f = dec.GetFrame(MediaTime.FromFrame(12, Rational.Pal));
        byte[] bgra = f!.ToBgra(out _, out _);
        Assert.InRange(bgra[2], 112, 128);
    }

    [Fact]
    public void HardwareEncoderRoundTrip()
    {
        using var temp = new TempFolder();
        var settings = new EncoderSettings
        {
            Container = ContainerFormat.Mp4,
            Width = 320,
            Height = 240,
            FrameRate = Rational.Pal,
            HardwareEncode = true,
        };
        if (MovieEncoder.MissingEncoder(settings) is { } missing)
        {
            Assert.Skip($"{missing} is not in this FFmpeg build");
        }

        if (OperatingSystem.IsLinux() && !File.Exists(HardwareDevice.RenderNode))
        {
            Assert.Skip($"No VA-API device ({HardwareDevice.RenderNode})");
        }

        string path = temp.File("hw.mp4");
        Encode(path, settings);

        MediaInfo info = MediaProbe.Probe(path);
        Assert.Equal(320, info.Video!.Width);
        Assert.InRange(info.Duration.Seconds, 0.95, 1.1);

        using var dec = new VideoDecoder(path, allowHardware: true);
        using DecodedFrame? f = dec.GetFrame(MediaTime.FromFrame(12, Rational.Pal));
        if (OperatingSystem.IsMacOS())
        {
            Assert.True(dec.IsHardware, "VideoToolbox decodes H.264");
            Assert.False(f!.IsHardware, "VideoToolbox frames are copied to memory");
        }
        else if (OperatingSystem.IsWindows())
        {
            Assert.True(dec.IsHardware, "D3D11VA decodes H.264");
            Assert.False(f!.IsHardware, "D3D11VA frames are copied to memory");
        }

        byte[] bgra = f!.ToBgra(out _, out _);
        Assert.InRange(bgra[2], 100, 140);
    }

    private static unsafe void Encode(string path, EncoderSettings settings)
    {
        int w = settings.Width, h = settings.Height;
        byte[] rgba = new byte[w * h * 4];
        float[] audio = new float[48000 / 25 * 2];
        using var enc = new MovieEncoder(path, settings);
        for (int n = 0; n < 25; n++)
        {
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = (byte)(n * 10);
                rgba[i + 1] = 128;
                rgba[i + 2] = 64;
                rgba[i + 3] = 255;
            }

            fixed (byte* p = rgba)
            {
                enc.WriteVideo((IntPtr)p, w * 4, bgra: false);
            }

            for (int i = 0; i < audio.Length; i += 2)
            {
                audio[i] = audio[i + 1] = (float)(0.25 * Math.Sin(2 * Math.PI * 440 * (n * audio.Length / 2 + i / 2) / 48000.0));
            }

            enc.WriteAudio(audio);
        }

        enc.Finish();
    }

    [Fact]
    public void AbortDeletesPartialFile()
    {
        using var temp = new TempFolder();
        string path = temp.File("partial.mp4");
        var enc = new MovieEncoder(path, new EncoderSettings { Width = 64, Height = 64, Preset = "ultrafast" });
        enc.Abort();
        Assert.False(File.Exists(path));
    }
}
