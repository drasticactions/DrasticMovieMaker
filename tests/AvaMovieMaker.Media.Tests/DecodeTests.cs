using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Tests;

public class DecodeTests
{
    public DecodeTests() => MediaFixture.Load();

    private static int Counter(DecodedFrame f)
    {
        byte[] bgra = f.ToBgra(out int w, out int h);
        return FrameCounter.Read(bgra, w, h);
    }

    [Fact]
    public void AccurateSeekGivesEveryFrameOnLongGop()
    {
        string path = TestMedia.CounterCard(seconds: 6, gop: 250);
        using var dec = new VideoDecoder(path, allowHardware: false);
        var rate = Rational.Ntsc;
        int frames = (int)MediaTime.FromSeconds(6).ToFrameFloor(rate);

        var order = Enumerable.Range(0, frames).OrderBy(i => (i * 7919) % frames).ToList();
        foreach (int n in order)
        {
            MediaTime t = MediaTime.FromFrame(n, rate) + MediaTime.FromMilliseconds(1);
            using DecodedFrame? f = dec.GetFrame(t);
            Assert.NotNull(f);
            Assert.Equal(n, Counter(f!));
        }
    }

    [Fact]
    public void SequentialReadReturnsConsecutiveFrames()
    {
        string path = TestMedia.CounterCard(seconds: 2, gop: 30);
        using var dec = new VideoDecoder(path, allowHardware: false);
        dec.SeekTo(MediaTime.FromFrame(10, Rational.Ntsc));
        for (int n = 10; n < 40; n++)
        {
            using DecodedFrame? f = dec.ReadNext();
            Assert.NotNull(f);
            Assert.Equal(n, Counter(f!));
        }
    }

    [Fact]
    public void PastEndHoldsLastFrame()
    {
        string path = TestMedia.CounterCard(seconds: 1, gop: 30);
        using var dec = new VideoDecoder(path, allowHardware: false);
        using DecodedFrame? f = dec.GetFrame(MediaTime.FromSeconds(5));
        Assert.NotNull(f);
        Assert.True(Counter(f!) >= 28);
    }

    [Fact]
    public void PictureDecodesAsRgba()
    {
        using var dec = new VideoDecoder(TestMedia.Picture(64, 48, ".png"), allowHardware: false);
        using DecodedFrame? f = dec.GetFrame(MediaTime.FromSeconds(3));
        Assert.NotNull(f);
        Assert.Equal(FramePixelFormat.Rgba, f!.Format);
        Assert.Equal(64, f.Width);
    }

    [Fact]
    public void PoolReusesAndEvicts()
    {
        using var pool = new DecoderPool(capacity: 2, allowHardware: false);
        string a = TestMedia.CounterCard(seconds: 1, hue: 10);
        string b = TestMedia.CounterCard(seconds: 1, hue: 100);
        string c = TestMedia.CounterCard(seconds: 1, hue: 200);
        VideoDecoder first;
        using (DecoderPool.Lease l = pool.Acquire(a))
        {
            first = l.Decoder;
        }

        using (DecoderPool.Lease l = pool.Acquire(a))
        {
            Assert.Same(first, l.Decoder);
            using DecoderPool.Lease l2 = pool.Acquire(a);
            Assert.NotSame(first, l2.Decoder);
        }

        using (pool.Acquire(b))
        using (pool.Acquire(c))
        {
        }

        Assert.True(pool.OpenCount <= 2);
    }

    [Fact]
    public void PoolKeepsADecoderNearEachPositionInOneFile()
    {
        using var pool = new DecoderPool(allowHardware: false);
        string path = TestMedia.CounterCard(seconds: 6, gop: 250);
        var rate = Rational.Ntsc;
        VideoDecoder early, late;
        using (DecoderPool.Lease l = pool.Acquire(path, MediaTime.FromFrame(10, rate)))
        {
            early = l.Decoder;
            l.Decoder.GetFrame(MediaTime.FromFrame(10, rate))!.Dispose();
        }

        using (DecoderPool.Lease l = pool.Acquire(path, MediaTime.FromFrame(150, rate)))
        {
            late = l.Decoder;
            l.Decoder.GetFrame(MediaTime.FromFrame(150, rate))!.Dispose();
        }

        Assert.NotSame(early, late);
        for (int n = 1; n <= 5; n++)
        {
            using (DecoderPool.Lease l = pool.Acquire(path, MediaTime.FromFrame(10 + n, rate)))
            {
                Assert.Same(early, l.Decoder);
                l.Decoder.GetFrame(MediaTime.FromFrame(10 + n, rate))!.Dispose();
            }

            using (DecoderPool.Lease l = pool.Acquire(path, MediaTime.FromFrame(150 + n, rate)))
            {
                Assert.Same(late, l.Decoder);
                l.Decoder.GetFrame(MediaTime.FromFrame(150 + n, rate))!.Dispose();
            }
        }

        using (DecoderPool.Lease l = pool.Acquire(path, MediaTime.FromFrame(80, rate)))
        {
            Assert.Same(early, l.Decoder);
        }

        Assert.Equal(2, pool.OpenCount);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    public void StillsDecodeAtAnyTime(string ext)
    {
        string path = TestMedia.Picture(320, 240, ext);
        using var dec = new VideoDecoder(path, allowHardware: false);
        using DecodedFrame first = dec.GetFrame(MediaTime.Zero)!;
        Assert.Equal(320, first.Width);
        using DecodedFrame later = dec.GetFrame(MediaTime.FromSeconds(3))!;
        Assert.Equal(240, later.Height);
    }
}
