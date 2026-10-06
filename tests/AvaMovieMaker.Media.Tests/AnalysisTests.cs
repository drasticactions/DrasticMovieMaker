using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;
using SkiaSharp;

namespace AvaMovieMaker.Media.Tests;

public class AnalysisTests
{
    public AnalysisTests() => MediaFixture.Load();

    [Fact]
    public void ThumbnailFitsCell()
    {
        using var temp = new TempFolder();
        MediaCache.RootOverride = temp.Path;
        using SKBitmap? bmp = Thumbnailer.Get(TestMedia.CounterCard(seconds: 2), MediaTime.FromSeconds(1), 80, 60);
        Assert.NotNull(bmp);
        Assert.Equal(80, bmp!.Width);
        Assert.Equal(60, bmp.Height);
    }

    [Fact]
    public void RepresentativeTimeIsOneSecondOrHalf()
    {
        Assert.Equal(MediaTime.FromSeconds(1), Thumbnailer.RepresentativeTime(MediaTime.Zero, MediaTime.FromSeconds(10)));
        Assert.Equal(MediaTime.FromSeconds(5.25), Thumbnailer.RepresentativeTime(MediaTime.FromSeconds(5), MediaTime.FromSeconds(0.5)));
    }

    [Fact]
    public void WaveformHasPeaks()
    {
        Waveform w = WaveformBuilder.Build(TestMedia.Tone(seconds: 1));
        Assert.InRange(w.BucketCount, 99, 101);
        Assert.InRange(w.Max[50], 0.23f, 0.26f);
        Assert.InRange(w.Min[50], -0.26f, -0.23f);
        (float[] min, _, double bucket) = w.LevelFor(0.04);
        Assert.Equal(0.04, bucket, 6);
        Assert.InRange(min.Length, 24, 26);
    }

    [Fact]
    public void DetectsHardCuts()
    {
        string path = TestMedia.SceneCuts([2.0, 4.5, 6.0], 8);
        var clips = ClipDetector.Detect(path);
        Assert.Equal(4, clips.Count);
        Assert.InRange(clips[1].Start.Seconds, 1.95, 2.05);
        Assert.InRange(clips[2].Start.Seconds, 4.45, 4.55);
        Assert.InRange(clips[3].Start.Seconds, 5.95, 6.05);
    }

    [Fact]
    public void CutsCloserThanFifteenFramesAreIgnored()
    {
        string path = TestMedia.SceneCuts([2.0, 2.3, 3.3], 5);
        var clips = ClipDetector.Detect(path);
        Assert.Equal(3, clips.Count);
        Assert.InRange(clips[1].Start.Seconds, 1.95, 2.05);
        Assert.InRange(clips[2].Start.Seconds, 3.25, 3.35);
    }

    [Fact]
    public void CutNeedsDifferenceAboveThresholdAndTwiceTheMean()
    {
        Assert.True(ClipDetector.IsCut(0.5, 0.0));
        Assert.False(ClipDetector.IsCut(0.16, 0.0));
        Assert.True(ClipDetector.IsCut(0.17, 0.08));
        Assert.False(ClipDetector.IsCut(0.30, 0.15));
        int[] a = new int[256], b = new int[256];
        a[10] = 100;
        b[10] = 60;
        b[200] = 40;
        Assert.Equal(0.4, ClipDetector.Difference(a, b, 100), 6);
        Assert.Equal(0.0, ClipDetector.Difference(a, a, 100), 6);
    }

    [Fact]
    public void FramesAreHalvedToAtMostAHundredPixels()
    {
        Assert.Equal(1, ClipDetector.ReductionFactor(100));
        Assert.Equal(2, ClipDetector.ReductionFactor(160));
        Assert.Equal(8, ClipDetector.ReductionFactor(720));
        Assert.Equal(32, ClipDetector.ReductionFactor(1920));
    }

    [Fact]
    public void DvRecordingTimeIsReadFromVauxPacks()
    {
        byte[] frame = new byte[120000];
        int o = (3 * 80) + 3 + (3 * 5);
        frame[o] = 0x63;
        frame[o + 1] = 0xFF;
        frame[o + 2] = 0x80 | 0x45;
        frame[o + 3] = 0x80 | 0x59;
        frame[o + 4] = 0xC0 | 0x23;
        Assert.Equal(new TimeSpan(23, 59, 45), ClipDetector.DvRecordingTime(frame));
        Assert.Null(ClipDetector.DvRecordingTime(new byte[120000]));

        Assert.False(ClipDetector.IsRecordingBreak(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11)));
        Assert.True(ClipDetector.IsRecordingBreak(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12)));
        Assert.True(ClipDetector.IsRecordingBreak(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(8)));
        Assert.True(ClipDetector.IsRecordingBreak(null, TimeSpan.FromSeconds(8)));
        Assert.True(ClipDetector.IsRecordingBreak(TimeSpan.FromSeconds(8), null));
        Assert.False(ClipDetector.IsRecordingBreak(null, null));
    }

    [Fact]
    public void DvRecordingBreaksSplitAStillPicture()
    {
        string path = TestMedia.DvRecordings(1, "10:00:00", "10:00:01", "14:30:00");
        Assert.Equal([60], ClipDetector.DvRecordingBreaks(path));
        var clips = ClipDetector.Detect(path);
        Assert.Equal(2, clips.Count);
        Assert.InRange(clips[1].Start.Seconds, 1.95, 2.05);
        Assert.Empty(ClipDetector.DvRecordingBreaks(TestMedia.CounterCard(seconds: 1)));
    }
}
