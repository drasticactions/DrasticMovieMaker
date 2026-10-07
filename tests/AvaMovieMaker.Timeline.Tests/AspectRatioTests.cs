using System.Text.Json.Nodes;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Serialization;

namespace AvaMovieMaker.Timeline.Tests;

public class AspectRatioTests
{
    [Theory]
    [InlineData(AspectRatio.Standard4x3, 4, 3, "4:3", false, true)]
    [InlineData(AspectRatio.Widescreen16x9, 16, 9, "16:9", false, true)]
    [InlineData(AspectRatio.Vertical9x16, 9, 16, "9:16", true, false)]
    [InlineData(AspectRatio.Square1x1, 1, 1, "1:1", false, false)]
    [InlineData(AspectRatio.Portrait4x5, 4, 5, "4:5", true, false)]
    public void RatioLabelAndKind(AspectRatio aspect, int num, int den, string label, bool portrait, bool dvd)
    {
        Assert.Equal((num, den), AspectRatios.Ratio(aspect));
        Assert.Equal(num / (double)den, AspectRatios.Value(aspect));
        Assert.Equal(label, AspectRatios.Label(aspect));
        Assert.Equal(portrait, AspectRatios.IsPortrait(aspect));
        Assert.Equal(dvd, AspectRatios.HasDvd(aspect));
        Assert.True(AspectRatios.TryParse(AspectRatios.Id(aspect), out AspectRatio back));
        Assert.Equal(aspect, back);
    }

    [Fact]
    public void AllListsEveryRatioInMenuOrder()
    {
        Assert.Equal(Enum.GetValues<AspectRatio>(), AspectRatios.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("16x9")]
    [InlineData(null)]
    public void TryParseRejectsUnknownIds(string? id)
    {
        Assert.False(AspectRatios.TryParse(id, out AspectRatio a));
        Assert.Equal(AspectRatio.Standard4x3, a);
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, 480, 640, 480)]
    [InlineData(AspectRatio.Standard4x3, 720, 960, 720)]
    [InlineData(AspectRatio.Standard4x3, 1080, 1440, 1080)]
    [InlineData(AspectRatio.Widescreen16x9, 480, 854, 480)]
    [InlineData(AspectRatio.Widescreen16x9, 720, 1280, 720)]
    [InlineData(AspectRatio.Widescreen16x9, 1080, 1920, 1080)]
    [InlineData(AspectRatio.Vertical9x16, 480, 480, 854)]
    [InlineData(AspectRatio.Vertical9x16, 720, 720, 1280)]
    [InlineData(AspectRatio.Vertical9x16, 1080, 1080, 1920)]
    [InlineData(AspectRatio.Square1x1, 480, 480, 480)]
    [InlineData(AspectRatio.Square1x1, 720, 720, 720)]
    [InlineData(AspectRatio.Square1x1, 1080, 1080, 1080)]
    [InlineData(AspectRatio.Portrait4x5, 480, 480, 600)]
    [InlineData(AspectRatio.Portrait4x5, 720, 720, 900)]
    [InlineData(AspectRatio.Portrait4x5, 1080, 1080, 1350)]
    public void SizeForShortSide(AspectRatio aspect, int shortSide, int width, int height)
    {
        Assert.Equal((width, height), AspectRatios.SizeForShortSide(aspect, shortSide));
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, 640, 480)]
    [InlineData(AspectRatio.Widescreen16x9, 854, 480)]
    [InlineData(AspectRatio.Vertical9x16, 480, 854)]
    [InlineData(AspectRatio.Square1x1, 480, 480)]
    [InlineData(AspectRatio.Portrait4x5, 480, 600)]
    public void ProjectSettingsSizes(AspectRatio aspect, int width, int height)
    {
        var s = new ProjectSettings { Aspect = aspect };
        Assert.Equal((width, height), s.PreviewSize);
        Assert.Equal(AspectRatios.Value(aspect), s.DisplayAspect);
    }

    [Theory]
    [InlineData("standard4x3", AspectRatio.Standard4x3)]
    [InlineData("widescreen16x9", AspectRatio.Widescreen16x9)]
    public void VersionOneFilesLoad(string name, AspectRatio expected)
    {
        Project p = ProjectSerializer.FromJson(Json(1, name));
        Assert.Equal(expected, p.Settings.Aspect);
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, "standard4x3")]
    [InlineData(AspectRatio.Widescreen16x9, "widescreen16x9")]
    [InlineData(AspectRatio.Vertical9x16, "vertical9x16")]
    [InlineData(AspectRatio.Square1x1, "square1x1")]
    [InlineData(AspectRatio.Portrait4x5, "portrait4x5")]
    public void EveryRatioRoundTrips(AspectRatio aspect, string name)
    {
        var p = new Project { Settings = new ProjectSettings { Aspect = aspect } };
        string json = ProjectSerializer.ToJson(p);
        Assert.Equal(name, JsonNode.Parse(json)!["settings"]!["aspect"]!.GetValue<string>());
        Assert.Equal(aspect, ProjectSerializer.FromJson(json).Settings.Aspect);
    }

    [Fact]
    public void UnknownRatioLoadsAs4x3WithAWarning()
    {
        var warnings = new List<string>();
        void OnWrite(LogLevel level, string category, string message)
        {
            if (level == LogLevel.Warning && category == "project" && message.Contains("cinema21x9", StringComparison.Ordinal))
            {
                lock (warnings)
                {
                    warnings.Add(message);
                }
            }
        }

        Log.Written += OnWrite;
        try
        {
            Project p = ProjectSerializer.FromJson(Json(3, "cinema21x9"));
            Assert.Equal(AspectRatio.Standard4x3, p.Settings.Aspect);
            Assert.Single(warnings);
        }
        finally
        {
            Log.Written -= OnWrite;
        }
    }

    [Fact]
    public void SavedFilesAreVersionTwo()
    {
        using var temp = new TempFolder();
        string path = temp.File("v.dmmproj");
        Project p = ProjectSerializer.FromJson(Json(1, "widescreen16x9"));
        ProjectSerializer.Save(p, path);
        JsonNode node = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(2, node["version"]!.GetValue<int>());
        Assert.Equal(2, Project.FileVersion);
    }

    private static string Json(int version, string aspect) =>
        $$"""{ "format": "dmmproj", "version": {{version}}, "settings": { "aspect": "{{aspect}}" } }""";
}
