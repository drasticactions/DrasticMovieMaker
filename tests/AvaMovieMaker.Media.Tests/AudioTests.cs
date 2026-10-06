using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.TestSupport;

namespace AvaMovieMaker.Media.Tests;

public class AudioTests
{
    public AudioTests() => MediaFixture.Load();

    [Fact]
    public void ReadsAreSampleExact()
    {
        double seconds = 2;
        using var reader = new AudioReader(TestMedia.Ramp(seconds));
        float[] buf = new float[64 * 2];
        foreach (long pos in new long[] { 0, 48000, 1000, 95000, 47999, 12345, 70000, 3 })
        {
            reader.Read(pos, buf);
            for (int i = 0; i < 64; i++)
            {
                float expected = (pos + i) / (float)(seconds * 48000);
                Assert.Equal(expected, buf[i * 2], 4);
                Assert.Equal(expected, buf[i * 2 + 1], 4);
            }
        }
    }

    [Fact]
    public void MonoIsDuplicatedAndEndIsSilent()
    {
        using var reader = new AudioReader(TestMedia.Tone(seconds: 0.5, channels: 1));
        float[] buf = new float[480 * 2];
        reader.Read(100, buf);
        Assert.Contains(buf, v => Math.Abs(v) > 0.2f);
        for (int i = 0; i < 480; i++)
        {
            Assert.Equal(buf[i * 2], buf[i * 2 + 1], 5);
        }

        reader.Read(48000, buf);
        Assert.All(buf, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void ResamplesTo48k()
    {
        using var reader = new AudioReader(TestMedia.Tone(seconds: 1, sampleRate: 44100));
        Assert.InRange(reader.DurationSamples, 47990, 48010);
        float[] buf = new float[4800 * 2];
        reader.Read(4800, buf);
        float peak = buf.Max(Math.Abs);
        Assert.InRange(peak, 0.23f, 0.27f);
    }
}
