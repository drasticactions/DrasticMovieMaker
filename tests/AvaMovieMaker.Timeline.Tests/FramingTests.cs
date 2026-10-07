using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Rendering.Plan;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using static AvaMovieMaker.Timeline.Tests.Fixture;

namespace AvaMovieMaker.Timeline.Tests;

public class FramingTests
{
    private static string[] Ids(Fixture f, Guid id) => [.. f.Project.VideoTrack[f.Project.IndexOfVideo(id)].Effects.Select(e => e.EffectId)];

    [Fact]
    public void FitOfReadsTheFramingEffect()
    {
        Assert.Equal(FrameFitMode.Fit, EffectCatalog.FitOf([]));
        Assert.Equal(FrameFitMode.Fit, EffectCatalog.FitOf(["sepia-tone"]));
        Assert.Equal(FrameFitMode.Fill, EffectCatalog.FitOf(["sepia-tone", "fill-frame"]));
        Assert.Equal(FrameFitMode.Blur, EffectCatalog.FitOf(["blurred-background", "blur"]));
        Assert.All(EffectCatalog.All.Where(e => e.Family == EffectFamily.Framing), e => Assert.Equal(string.Empty, e.MswmmId));
        Assert.Null(EffectCatalog.FindMswmmId(string.Empty));
    }

    [Fact]
    public void AddingAFramingEffectReplacesTheOtherInPlace()
    {
        Assert.Equal(["sepia-tone", "blurred-background", "blur"], EffectCatalog.WithEffect(["sepia-tone", "fill-frame", "blur"], "blurred-background"));
        Assert.Equal(["sepia-tone", "fill-frame"], EffectCatalog.WithEffect(["sepia-tone"], "fill-frame"));
        Assert.Equal(["fill-frame", "blur", "blur"], EffectCatalog.WithEffect(["fill-frame", "blur"], "blur"));
    }

    [Fact]
    public void EditorAddKeepsOneFramingEffect()
    {
        var f = new Fixture();
        Guid[] ids = f.AddVideos(5);
        Assert.True(f.Editor.AddEffect(ids, "sepia-tone"));
        Assert.True(f.Editor.AddEffect(ids, "fill-frame"));
        Assert.True(f.Editor.AddEffect(ids, "grayscale"));
        Assert.True(f.Editor.AddEffect(ids, "blurred-background"));
        Assert.Equal(["sepia-tone", "blurred-background", "grayscale"], Ids(f, ids[0]));
        Assert.True(f.Editor.ToggleEffect(ids[0], "fill-frame"));
        Assert.Equal(["sepia-tone", "fill-frame", "grayscale"], Ids(f, ids[0]));
        Assert.True(f.Editor.ToggleEffect(ids[0], "fill-frame"));
        Assert.Equal(["sepia-tone", "grayscale"], Ids(f, ids[0]));
        Assert.True(f.Editor.SetEffects(ids[0], ["fill-frame", "blur", "blurred-background"]));
        Assert.Equal(["blurred-background", "blur"], Ids(f, ids[0]));
    }

    [Fact]
    public void ReplacingAFramingEffectIsAllowedAtTheEffectLimit()
    {
        var f = new Fixture();
        Guid[] ids = f.AddVideos(5);
        f.Editor.SetEffects(ids[0], ["fill-frame", "blur", "sepia-tone", "grayscale", "sharpen", "posterize"]);
        Assert.False(f.Editor.AddEffect(ids, "threshold"));
        Assert.True(f.Editor.AddEffect(ids, "blurred-background"));
        Assert.Equal("blurred-background", Ids(f, ids[0])[0]);
        Assert.Equal(TimelineEditor.MaxEffects, Ids(f, ids[0]).Length);
    }

    [Fact]
    public void SetFitIsOneUndoStepAcrossTheSelection()
    {
        var f = new Fixture();
        Guid[] ids = f.AddVideos(5, 5);
        f.Editor.AddEffect([ids[1]], "fill-frame");
        f.Editor.AddEffect([ids[1]], "sepia-tone");
        Assert.True(f.Editor.SetFit(ids, FrameFitMode.Blur));
        Assert.Equal(UndoNames.ChangeFit, f.Undo.UndoName);
        Assert.Equal(["blurred-background"], Ids(f, ids[0]));
        Assert.Equal(["blurred-background", "sepia-tone"], Ids(f, ids[1]));
        Assert.False(f.Editor.SetFit(ids, FrameFitMode.Blur));

        f.Undo.Undo();
        Assert.Empty(Ids(f, ids[0]));
        Assert.Equal(["fill-frame", "sepia-tone"], Ids(f, ids[1]));
        f.Undo.Redo();

        Assert.True(f.Editor.SetFit(ids, FrameFitMode.Fit));
        Assert.Empty(Ids(f, ids[0]));
        Assert.Equal(["sepia-tone"], Ids(f, ids[1]));
    }

    [Fact]
    public void SetFitSkipsTitleClips()
    {
        var f = new Fixture();
        f.AddVideos(5);
        VideoClip title = f.Editor.NewTitleClip(new TitleContent { Lines = ["Hi"] });
        f.Editor.InsertVideoClips(1, [title]);
        Assert.False(TimelineEditor.CanFrame(title));
        Assert.False(f.Editor.SetFit([title.Id], FrameFitMode.Fill));
        Assert.True(f.Editor.SetFit([f.Project.VideoTrack[0].Id, title.Id], FrameFitMode.Fill));
        Assert.Empty(Ids(f, title.Id));
    }

    [Fact]
    public void PlannerPassesTheModeAndDropsFramingEffects()
    {
        var f = new Fixture();
        Guid[] ids = f.AddVideos(5);
        f.Editor.AddEffect(ids, "sepia-tone");
        f.Editor.AddEffect(ids, "blurred-background");
        FramePlan plan = RenderPlanner.Build(f.Project).PlanAt(S(1));
        var src = (MediaSource)plan.A.Source;
        Assert.Equal(FrameFitMode.Blur, src.Fit);
        Assert.Equal(["sepia-tone"], plan.A.Effects.Select(e => e.EffectId));
    }

    [Fact]
    public void PlannedFramingReachesTheCompositor()
    {
        var f = new Fixture();
        f.Project.Settings = f.Project.Settings with { Aspect = AspectRatio.Vertical9x16 };
        var pic = new MediaItem { Kind = Media.MediaKind.Picture, Path = TestSupport.TestMedia.Picture(640, 360, ".png", 30), Name = "wide" };
        f.Editor.ImportMedia([pic]);
        VideoClip Clip(params string[] effects) => f.Editor.NewVideoClip(pic) with { StillDuration = S(3), Effects = [.. effects.Select(e => new EffectRef(e))] };
        f.Editor.InsertVideoClips(0, [Clip(), Clip("fill-frame"), Clip("blurred-background")]);
        RenderPlan plan = RenderPlanner.Build(f.Project);
        using var pool = new Media.Decoding.DecoderPool(allowHardware: false);
        using RenderDevice device = RenderDevice.Create(preferGpu: false);
        using var comp = new Compositor(device, Effects.EffectLibrary.Instance, new PooledFrameProvider(pool));
        int RedAtTop(double seconds) => device.Thread.Invoke(() =>
        {
            using SkiaSharp.SKImage img = comp.Render(plan.PlanAt(S(seconds)), 90, 160);
            byte[] px = new byte[90 * 160 * 4];
            Compositor.ReadPixels(img, px, bgra: false);
            return px[(10 * 90 + 5) * 4];
        });

        int fit = RedAtTop(1.5), fill = RedAtTop(4.5), blur = RedAtTop(7.5);
        Assert.Equal(0, fit);
        Assert.True(fill > 150, $"fill {fill}");
        Assert.InRange(blur, (int)(fill * 0.75) - 8, (int)(fill * 0.75) + 8);
    }

    [Fact]
    public void SetAspectIsOneUndoStep()
    {
        var f = new Fixture();
        f.AddVideos(5);
        f.Editor.AddEffect([f.Project.VideoTrack[0].Id], "fill-frame");
        Assert.True(f.Editor.SetAspect(AspectRatio.Vertical9x16));
        Assert.False(f.Editor.SetAspect(AspectRatio.Vertical9x16));
        Assert.Equal(UndoNames.ChangeAspectRatio, f.Undo.UndoName);
        Assert.Equal(AspectRatio.Vertical9x16, f.Project.Settings.Aspect);
        Assert.Equal(["fill-frame"], Ids(f, f.Project.VideoTrack[0].Id));
        f.Undo.Undo();
        Assert.Equal(AspectRatio.Standard4x3, f.Project.Settings.Aspect);
        Assert.Equal(UndoNames.AddEffect, f.Undo.UndoName);
        f.Undo.Redo();
        Assert.Equal(AspectRatio.Vertical9x16, f.Project.Settings.Aspect);
    }
}
