using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Preview;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class AspectAndFitTests
{
    private static List<Guid> AddPictures(Harness h, int count)
    {
        var item = new MediaItem { Kind = MediaKind.Picture, Path = SamplePictures.For(AspectRatio.Standard4x3).First, Name = "flower" };
        h.Session.Editor.ImportMedia([item]);
        h.Session.Editor.InsertVideoClips(0, [.. Enumerable.Range(0, count).Select(_ => h.Session.Editor.NewVideoClip(item))]);
        return [.. h.Session.Project.VideoTrack.Select(c => c.Id)];
    }

    [Fact]
    public void View_aspect_ratio_changes_only_the_project_as_one_undo_step()
    {
        using var h = new Harness();
        AddPictures(h, 2);
        var changed = new List<string?>();
        h.Shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        double before = h.Shell.Monitor.DisplayAspect;
        bool monitorNotified = false;
        h.Shell.Monitor.PropertyChanged += (_, e) => monitorNotified |= e.PropertyName == nameof(MonitorViewModel.DisplayAspect);

        h.Shell.SetAspectRatioCommand.Execute(AspectRatio.Vertical9x16);
        Assert.Equal(AspectRatio.Vertical9x16, h.Shell.Aspect);
        Assert.Contains(nameof(h.Shell.Aspect), changed);
        Assert.Equal("4:3", h.Store.Settings.DefaultAspect);
        Assert.Equal(UndoNames.ChangeAspectRatio, h.Session.Undo.UndoName);
        Assert.Equal("Undo Change Aspect Ratio", h.Shell.UndoText.Replace("_", string.Empty, StringComparison.Ordinal));
        Assert.True(h.Session.Undo.IsDirty);
        Assert.True(monitorNotified);
        Assert.Equal(9.0 / 16.0, h.Shell.Monitor.DisplayAspect);

        h.Shell.UndoCommand.Execute(null);
        Assert.Equal(AspectRatio.Standard4x3, h.Shell.Aspect);
        Assert.Equal(before, h.Shell.Monitor.DisplayAspect);
    }

    [Fact]
    public void Clip_video_fit_applies_to_the_selection_and_reports_the_shared_mode()
    {
        using var h = new Harness();
        List<Guid> ids = AddPictures(h, 2);
        h.Session.Select([ids[0]]);
        Assert.True(h.Shell.CanFit);
        Assert.Equal(FrameFitMode.Fit, h.Shell.SelectedFit);
        Assert.True(h.Shell.SetFitCommand.CanExecute(FrameFitMode.Fill));

        h.Shell.SetFitCommand.Execute(FrameFitMode.Fill);
        Assert.Equal(FrameFitMode.Fill, h.Shell.SelectedFit);
        Assert.Equal(UndoNames.ChangeFit, h.Session.Undo.UndoName);

        h.Session.Select(ids);
        Assert.Null(h.Shell.SelectedFit);
        h.Shell.SetFitCommand.Execute(FrameFitMode.Blur);
        Assert.Equal(FrameFitMode.Blur, h.Shell.SelectedFit);
        Assert.All(h.Session.Project.VideoTrack, c => Assert.Equal(["blurred-background"], c.Effects.Select(e => e.EffectId)));

        h.Shell.UndoCommand.Execute(null);
        Assert.Null(h.Shell.SelectedFit);
    }

    [Fact]
    public void Fit_is_unavailable_for_titles()
    {
        using var h = new Harness();
        Guid title = h.AddTitle("Hello");
        h.Session.Select([title]);
        Assert.False(h.Shell.CanFit);
        Assert.Null(h.Shell.SelectedFit);
        Assert.False(h.Shell.SetFitCommand.CanExecute(FrameFitMode.Fill));
    }

    [Fact]
    public void Effects_dialog_replaces_a_framing_effect_in_place()
    {
        var vm = new Dialogs.ClipEffectsViewModel(["sepia-tone", "fill-frame", "blur"]);
        vm.SelectedAvailable = vm.Available.Single(e => e.Id == "blurred-background");
        vm.AddCommand.Execute(null);
        Assert.Equal(["sepia-tone", "blurred-background", "blur"], vm.Result);
        Assert.Equal("blurred-background", vm.SelectedDisplayed!.Id);

        var full = new Dialogs.ClipEffectsViewModel(["fill-frame", "blur", "sepia-tone", "grayscale", "sharpen", "posterize"]);
        full.SelectedAvailable = full.Available.Single(e => e.Id == "threshold");
        Assert.False(full.AddCommand.CanExecute(null));
        full.SelectedAvailable = full.Available.Single(e => e.Id == "blurred-background");
        Assert.True(full.AddCommand.CanExecute(null));
    }
}
