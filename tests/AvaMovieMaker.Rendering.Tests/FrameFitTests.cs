using AvaMovieMaker.Rendering.Compositing;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Tests;

public class FrameFitTests
{
    [Fact]
    public void CoverFillsTheHeightOfANarrowFrameAndCenters()
    {
        SKMatrix m = FrameFit.Matrix(1920, 1080, 1.0, 0, false, 1080, 1920, 1.0, cover: true);
        SKRect r = m.MapRect(new SKRect(0, 0, 1920, 1080));
        Assert.Equal(0, r.Top, 3);
        Assert.Equal(1920, r.Bottom, 3);
        Assert.Equal(540, r.MidX, 3);
        Assert.True(r.Width > 1080);
        Assert.Equal(new SKRect(0, 0, 1080, 1920), FrameFit.Rect(m, 1920, 1080, 1080, 1920));
    }

    [Fact]
    public void CoverFillsTheWidthOfAWideFrame()
    {
        SKMatrix m = FrameFit.Matrix(1080, 1920, 1.0, 0, false, 1920, 1080, 1.0, cover: true);
        SKRect r = m.MapRect(new SKRect(0, 0, 1080, 1920));
        Assert.Equal(0, r.Left, 3);
        Assert.Equal(1920, r.Right, 3);
        Assert.Equal(540, r.MidY, 3);
    }

    [Fact]
    public void FitKeepsTheWholeSource()
    {
        SKMatrix m = FrameFit.Matrix(1920, 1080, 1.0, 0, false, 1080, 1920, 1.0);
        SKRect r = FrameFit.Rect(m, 1920, 1080, 1080, 1920);
        Assert.Equal(0, r.Left, 3);
        Assert.Equal(1080, r.Right, 3);
        Assert.Equal(607.5f, r.Height, 2);
        Assert.Equal(960, r.MidY, 3);
    }

    [Fact]
    public void CoverAccountsForRotation()
    {
        SKMatrix m = FrameFit.Matrix(1920, 1080, 1.0, 90, false, 1080, 1920, 1.0, cover: true);
        SKRect r = m.MapRect(new SKRect(0, 0, 1920, 1080));
        Assert.Equal(new SKRect(0, 0, 1080, 1920), new SKRect(MathF.Round(r.Left), MathF.Round(r.Top), MathF.Round(r.Right), MathF.Round(r.Bottom)));
    }
}
