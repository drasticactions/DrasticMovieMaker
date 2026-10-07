using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Export;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Tests;

public class PublishSizeTests
{
    [Theory]
    [InlineData("hd1080", AspectRatio.Standard4x3, 1440, 1080)]
    [InlineData("hd1080", AspectRatio.Widescreen16x9, 1920, 1080)]
    [InlineData("hd1080", AspectRatio.Vertical9x16, 1080, 1920)]
    [InlineData("hd1080", AspectRatio.Square1x1, 1080, 1080)]
    [InlineData("hd1080", AspectRatio.Portrait4x5, 1080, 1350)]
    [InlineData("webm1080", AspectRatio.Standard4x3, 1440, 1080)]
    [InlineData("webm1080", AspectRatio.Widescreen16x9, 1920, 1080)]
    [InlineData("webm1080", AspectRatio.Vertical9x16, 1080, 1920)]
    [InlineData("webm1080", AspectRatio.Square1x1, 1080, 1080)]
    [InlineData("webm1080", AspectRatio.Portrait4x5, 1080, 1350)]
    [InlineData("hd720", AspectRatio.Standard4x3, 960, 720)]
    [InlineData("hd720", AspectRatio.Widescreen16x9, 1280, 720)]
    [InlineData("hd720", AspectRatio.Vertical9x16, 720, 1280)]
    [InlineData("hd720", AspectRatio.Square1x1, 720, 720)]
    [InlineData("hd720", AspectRatio.Portrait4x5, 720, 900)]
    [InlineData("webm720", AspectRatio.Standard4x3, 960, 720)]
    [InlineData("webm720", AspectRatio.Widescreen16x9, 1280, 720)]
    [InlineData("webm720", AspectRatio.Vertical9x16, 720, 1280)]
    [InlineData("webm720", AspectRatio.Square1x1, 720, 720)]
    [InlineData("webm720", AspectRatio.Portrait4x5, 720, 900)]
    [InlineData("web", AspectRatio.Standard4x3, 640, 480)]
    [InlineData("web", AspectRatio.Widescreen16x9, 854, 480)]
    [InlineData("web", AspectRatio.Vertical9x16, 480, 854)]
    [InlineData("web", AspectRatio.Square1x1, 480, 480)]
    [InlineData("web", AspectRatio.Portrait4x5, 480, 600)]
    [InlineData("webm480", AspectRatio.Standard4x3, 640, 480)]
    [InlineData("webm480", AspectRatio.Widescreen16x9, 854, 480)]
    [InlineData("webm480", AspectRatio.Vertical9x16, 480, 854)]
    [InlineData("webm480", AspectRatio.Square1x1, 480, 480)]
    [InlineData("webm480", AspectRatio.Portrait4x5, 480, 600)]
    public void ProfileSizesFollowTheRatio(string id, AspectRatio aspect, int width, int height)
    {
        (int w, int h, Rational par) = PublishProfiles.Get(id).Size(new ProjectSettings { Aspect = aspect });
        Assert.Equal((width, height), (w, h));
        Assert.Equal(new Rational(1, 1), par);
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, VideoFormat.Ntsc, 480, 8, 9)]
    [InlineData(AspectRatio.Widescreen16x9, VideoFormat.Ntsc, 480, 32, 27)]
    [InlineData(AspectRatio.Standard4x3, VideoFormat.Pal, 576, 16, 15)]
    [InlineData(AspectRatio.Widescreen16x9, VideoFormat.Pal, 576, 64, 45)]
    public void DvdKeepsItsAnamorphicSizes(AspectRatio aspect, VideoFormat format, int height, int parNum, int parDen)
    {
        PublishProfile dvd = PublishProfiles.Get("dvd");
        Assert.True(dvd.IsAvailableFor(aspect));
        Assert.Equal((720, height, new Rational(parNum, parDen)), dvd.Size(new ProjectSettings { Aspect = aspect, Format = format }));
    }

    [Theory]
    [InlineData(AspectRatio.Vertical9x16)]
    [InlineData(AspectRatio.Square1x1)]
    [InlineData(AspectRatio.Portrait4x5)]
    public void DvdHasNoOtherRatios(AspectRatio aspect)
    {
        PublishProfile dvd = PublishProfiles.Get("dvd");
        Assert.False(dvd.IsAvailableFor(aspect));
        Assert.Throws<InvalidOperationException>(() => dvd.Size(new ProjectSettings { Aspect = aspect }));
        Assert.All(PublishProfiles.All.Where(p => p != dvd), p => Assert.True(p.IsAvailableFor(aspect)));
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, 1440, 1080)]
    [InlineData(AspectRatio.Widescreen16x9, 1920, 1080)]
    [InlineData(AspectRatio.Vertical9x16, 1080, 1920)]
    [InlineData(AspectRatio.Square1x1, 1080, 1080)]
    [InlineData(AspectRatio.Portrait4x5, 1080, 1350)]
    public void BestQualityUsesTheRotatedSourcesShortSide(AspectRatio aspect, int width, int height)
    {
        var phone = new VideoProperties { Width = 1920, Height = 1080, Rotation = 90 };
        Assert.Equal((1080, 1920), phone.DisplaySize);
        (int w, int h, _) = PublishProfiles.Recommended.Size(new ProjectSettings { Aspect = aspect }, phone.DisplaySize);
        Assert.Equal((width, height), (w, h));
    }

    [Fact]
    public void BestQualityClampsAndUsesTheSampleAspect()
    {
        var anamorphic = new VideoProperties { Width = 720, Height = 480, SampleAspectNum = 32, SampleAspectDen = 27 };
        Assert.Equal((853, 480), anamorphic.DisplaySize);
        Assert.Equal((854, 480), Size(AspectRatio.Widescreen16x9, anamorphic.DisplaySize));
        Assert.Equal((320, 240), Size(AspectRatio.Standard4x3, (200, 100)));
        Assert.Equal((1080, 1920), Size(AspectRatio.Vertical9x16, (4000, 3000)));

        static (int, int) Size(AspectRatio aspect, (int, int) source)
        {
            (int w, int h, _) = PublishProfiles.Recommended.Size(new ProjectSettings { Aspect = aspect }, source);
            return (w, h);
        }
    }

    [Fact]
    public void CompressToSizeUsesTheShortSide()
    {
        PublishProfile small = PublishProfile.CompressTo(1024 * 1024, MediaTime.FromSeconds(60));
        Assert.Equal((480, 854), Take(small.Size(new ProjectSettings { Aspect = AspectRatio.Vertical9x16 })));
        PublishProfile big = PublishProfile.CompressTo(1024L * 1024 * 1024, MediaTime.FromSeconds(60));
        Assert.Equal((720, 900), Take(big.Size(new ProjectSettings { Aspect = AspectRatio.Portrait4x5 })));

        static (int, int) Take((int W, int H, Rational _) s) => (s.W, s.H);
    }
}
