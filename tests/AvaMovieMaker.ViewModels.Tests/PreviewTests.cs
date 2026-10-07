using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using AvaMovieMaker.Timeline.Playback;
using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Timeline;
using SkiaSharp;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class PreviewTests
{
    private static (List<Guid> Clips, ContentItemViewModel Picture) Setup(Harness h)
    {
        for (int i = 2; i >= 0; i--)
        {
            h.Session.Editor.AddTitle(new TitleContent { Lines = [$"t{i}"] }, null, MediaTime.Zero);
        }

        string path = SamplePictures.For(AspectRatio.Standard4x3).First;
        h.Session.Editor.ImportMedia([new MediaItem { Kind = MediaKind.Picture, Path = path, Name = "flower" }]);
        h.Shell.Monitor.ShowProject(seekStart: true);
        return ([.. h.Session.Project.VideoTrack.Select(c => c.Id)], h.Shell.Contents.Items.Single());
    }

    private static MediaTime StartOf(Harness h, int i) => TimelineLayout.Compute(h.Session.Project).Starts[i];

    private static void Stop(Harness h) => h.Shell.Monitor.Playback.Pause();

    [Fact]
    public void Selecting_an_item_loads_it_paused_and_keeps_the_project_position()
    {
        using var h = new Harness();
        (List<Guid> clips, ContentItemViewModel picture) = Setup(h);
        MonitorViewModel m = h.Shell.Monitor;
        MediaTime at = StartOf(h, 1) + MediaTime.FromSeconds(0.5);
        m.Seek(at);
        Assert.Equal(at, h.Shell.Timeline.Playhead);

        h.Shell.Contents.Select([picture]);
        Assert.Equal(PreviewTarget.Item, m.Target);
        Assert.Equal("flower", m.Caption);
        Assert.False(m.IsPlaying);
        Assert.Equal(MediaTime.Zero, m.Position);
        Assert.Equal("0:00:00.00 / 0:00:05.00", m.TimeText);
        m.Seek(MediaTime.FromSeconds(2));
        Assert.Equal(at, h.Shell.Timeline.Playhead);
        Assert.Equal(at, m.ProjectPosition);

        h.Session.Select([clips[2]]);
        Assert.Equal(PreviewTarget.Project, m.Target);
        Assert.Equal(at, m.Position);
        Assert.Equal(at, h.Shell.Timeline.Playhead);
        Assert.Equal("Storyboard: t2", m.Caption);
    }

    [Fact]
    public void Double_click_plays_the_item()
    {
        using var h = new Harness();
        (_, ContentItemViewModel picture) = Setup(h);
        try
        {
            h.Shell.Contents.PreviewCommand.Execute(picture);
            Assert.Equal(PreviewTarget.Item, h.Shell.Monitor.Target);
            Assert.True(h.Shell.Monitor.IsPlaying);
            Assert.Equal("Pause _Clip", h.Shell.Monitor.PlayClipText);
            Assert.Equal("_Play Storyboard", h.Shell.PlayBoardText);
        }
        finally
        {
            Stop(h);
        }
    }

    [Fact]
    public void Ctrl_w_after_an_item_plays_the_project_from_its_saved_position()
    {
        using var h = new Harness();
        (List<Guid> clips, ContentItemViewModel picture) = Setup(h);
        MonitorViewModel m = h.Shell.Monitor;
        MediaTime at = StartOf(h, 1) + MediaTime.FromSeconds(0.25);
        m.Seek(at);
        h.Shell.Contents.Select([picture]);
        m.Seek(MediaTime.FromSeconds(3));
        try
        {
            m.PlayProjectCommand.Execute(null);
            Assert.Equal(PreviewTarget.Project, m.Target);
            Assert.True(m.IsProjectPlaying);
            Assert.True(m.Position >= at && m.Position < at + MediaTime.FromSeconds(1), $"{m.Position}");
            Assert.Equal([clips[1]], h.Session.SelectedClips);
            Assert.Equal("_Pause Storyboard", h.Shell.PlayBoardText);
            Assert.Equal("Pause Storyboard (Ctrl+W)", h.Shell.PlayBoardTip);
            Assert.Equal("pause", h.Shell.PlayBoardGlyph);

            m.PlayProjectCommand.Execute(null);
            Assert.False(m.IsPlaying);
            Assert.Equal("_Play Storyboard", h.Shell.PlayBoardText);
            Assert.Equal("Play Storyboard (Ctrl+W)", h.Shell.PlayBoardTip);
            Assert.Equal("Rewind Storyboard (Ctrl+Q)", h.Shell.RewindBoardTip);
            Assert.Equal("play", h.Shell.PlayBoardGlyph);
            Assert.Equal("Play _Clip", m.PlayClipText);
        }
        finally
        {
            Stop(h);
        }
    }

    [Fact]
    public void Caption_follows_the_selection_not_the_indicator()
    {
        using var h = new Harness();
        (List<Guid> clips, _) = Setup(h);
        MonitorViewModel m = h.Shell.Monitor;
        h.Session.Select([clips[0]]);
        Assert.Equal("Storyboard: t0", m.Caption);
        m.Seek(StartOf(h, 2) + MediaTime.FromSeconds(0.1));
        Assert.Equal("Storyboard: t0", m.Caption);

        Assert.True(h.Session.Editor.SetTransition(clips[1], "fade"));
        h.Shell.IsTimeline = true;
        h.Shell.Timeline.ActivateTrack(TimelineTrack.Transition);
        h.Shell.Timeline.Click(TimelineTrack.Transition, clips[1], ctrl: false, shift: false);
        Assert.Equal("Timeline: " + Effects.Catalog.TransitionCatalog.Get("fade").Name, m.Caption);

        h.Shell.Timeline.ClearSelection();
        Assert.Equal(string.Empty, m.Caption);
    }

    [Fact]
    public void Back_and_forward_select_the_neighbor_and_move_the_indicator()
    {
        using var h = new Harness();
        (List<Guid> clips, _) = Setup(h);
        MonitorViewModel m = h.Shell.Monitor;
        h.Session.Select([clips[0]]);
        m.ForwardCommand.Execute(null);
        Assert.Equal([clips[1]], h.Session.SelectedClips);
        Assert.Equal(StartOf(h, 1), m.Position);
        m.ForwardCommand.Execute(null);
        Assert.Equal([clips[2]], h.Session.SelectedClips);
        m.BackCommand.Execute(null);
        Assert.Equal([clips[1]], h.Session.SelectedClips);
        Assert.Equal(StartOf(h, 1), m.Position);
        Assert.Equal(StartOf(h, 1), h.Shell.Timeline.Playhead);

        h.Shell.IsTimeline = true;
        m.ForwardCommand.Execute(null);
        Assert.Equal([clips[2]], h.Session.SelectedClips);
        Assert.Equal(StartOf(h, 2), m.Position);
    }

    [Fact]
    public void Frame_steps_at_the_ends_go_to_the_neighbor_clip()
    {
        using var h = new Harness();
        Assert.False(h.Shell.Monitor.PreviousFrameCommand.CanExecute(null));
        Assert.False(h.Shell.Monitor.BackCommand.CanExecute(null));
        (List<Guid> clips, _) = Setup(h);
        MonitorViewModel m = h.Shell.Monitor;
        Assert.True(m.PreviousFrameCommand.CanExecute(null));

        h.Session.Select([clips[1]]);
        m.Seek(MediaTime.Zero);
        m.PreviousFrameCommand.Execute(null);
        Assert.Equal([clips[0]], h.Session.SelectedClips);

        m.Seek(m.Duration);
        m.NextFrameCommand.Execute(null);
        Assert.Equal([clips[1]], h.Session.SelectedClips);
        Assert.Equal(StartOf(h, 1), m.Position);

        MediaTime before = m.Position;
        m.NextFrameCommand.Execute(null);
        Assert.True(m.Position > before && m.Position < before + MediaTime.FromSeconds(0.1));
    }

    [Fact]
    public void Rewind_selects_the_first_clip_at_the_beginning()
    {
        using var h = new Harness();
        (List<Guid> clips, _) = Setup(h);
        h.Session.Select([clips[2]]);
        h.Shell.Monitor.Seek(StartOf(h, 2));
        h.Shell.Monitor.RewindCommand.Execute(null);
        Assert.Equal([clips[0]], h.Session.SelectedClips);
        Assert.Equal(MediaTime.Zero, h.Shell.Monitor.Position);
    }

    [Fact]
    public void Full_screen_needs_pictures_and_starts_playback()
    {
        using var h = new Harness();
        MonitorViewModel m = h.Shell.Monitor;
        Assert.False(m.FullScreenCommand.CanExecute(null));
        Assert.False(h.Shell.TakePictureCommand.CanExecute(null));
        Assert.False(m.PlayProjectCommand.CanExecute(null));
        Setup(h);
        Assert.True(m.FullScreenCommand.CanExecute(null));
        Assert.True(h.Shell.TakePictureCommand.CanExecute(null));
        try
        {
            m.FullScreenCommand.Execute(null);
            Assert.Contains(m, h.Dialogs.Shown);
            Assert.True(m.IsPlaying);
        }
        finally
        {
            Stop(h);
        }

        m.ShowItem(new MediaItem { Kind = MediaKind.Audio, Path = "/nonexistent/tone.flac", Name = "tone", Duration = MediaTime.FromSeconds(2) }, null, play: false);
        Assert.False(m.HasVideo);
        Assert.False(m.FullScreenCommand.CanExecute(null));
        Assert.False(h.Shell.TakePictureCommand.CanExecute(null));
        Assert.Equal("tone", m.Caption);
    }

    [Fact]
    public void Catalog_samples_use_built_in_pictures_with_nothing_imported()
    {
        using var h = new Harness();
        MonitorViewModel m = h.Shell.Monitor;
        ContentsViewModel c = h.Shell.Contents;
        c.View = ContentsView.Effects;
        CatalogItemViewModel effect = c.Effects[0];
        c.SelectedCatalogItem = effect;
        Assert.Equal(PreviewTarget.Item, m.Target);
        Assert.Equal(effect.Name, m.Caption);
        Assert.False(m.IsPlaying);
        Assert.Equal(MediaTime.FromSeconds(3), m.Duration);
        Assert.Empty(h.Session.Project.Media);

        c.View = ContentsView.Transitions;
        c.SelectedCatalogItem = c.Transitions[0];
        Assert.Equal(c.Transitions[0].Name, m.Caption);
        Assert.Equal(MediaTime.FromSeconds(4.5), m.Duration);
        try
        {
            c.PreviewCatalogCommand.Execute(c.Transitions[0]);
            Assert.True(m.IsPlaying);
        }
        finally
        {
            Stop(h);
        }
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, 640, 480, "4x3")]
    [InlineData(AspectRatio.Widescreen16x9, 640, 360, "16x9")]
    [InlineData(AspectRatio.Vertical9x16, 360, 640, "9x16")]
    [InlineData(AspectRatio.Square1x1, 640, 640, "1x1")]
    [InlineData(AspectRatio.Portrait4x5, 512, 640, "4x5")]
    public void Sample_pictures_are_our_own_drawings_at_the_project_aspect(AspectRatio aspect, int width, int height, string tag)
    {
        (string a, string b) = SamplePictures.For(aspect);
        Assert.StartsWith(AppPaths.CacheDir, a, StringComparison.Ordinal);
        Assert.NotEqual(a, b);
        Assert.Contains($"-{tag}-v1.png", a, StringComparison.Ordinal);
        foreach (string p in new[] { a, b })
        {
            using SKBitmap bmp = SKBitmap.Decode(p);
            Assert.Equal(width, bmp.Width);
            Assert.Equal(height, bmp.Height);
        }
    }

    [Theory]
    [InlineData(AspectRatio.Standard4x3, AspectRatio.Vertical9x16)]
    [InlineData(AspectRatio.Widescreen16x9, AspectRatio.Vertical9x16)]
    [InlineData(AspectRatio.Vertical9x16, AspectRatio.Widescreen16x9)]
    [InlineData(AspectRatio.Square1x1, AspectRatio.Widescreen16x9)]
    [InlineData(AspectRatio.Portrait4x5, AspectRatio.Widescreen16x9)]
    public void Framing_effects_preview_on_pictures_of_the_other_orientation(AspectRatio project, AspectRatio sample)
    {
        Assert.Equal(SamplePictures.For(sample), SamplePictures.Other(project));
        RenderPlan plan = ShellViewModel.CatalogSamplePlan("fill-frame", false, new ProjectSettings { Aspect = project });
        Assert.Equal(SamplePictures.For(sample).First, plan.Video[0].Path);
        Assert.Equal(AvaMovieMaker.Rendering.Compositing.FrameFitMode.Fill, plan.Video[0].Fit);
        RenderPlan sepia = ShellViewModel.CatalogSamplePlan("sepia-tone", false, new ProjectSettings { Aspect = project });
        Assert.Equal(SamplePictures.For(project).First, sepia.Video[0].Path);
    }

    [Fact]
    public void Picture_names_take_the_lowest_free_number()
    {
        using var temp = new TempFolder();
        Assert.Equal("cardA_0001.jpg", PictureNames.Suggest(temp.Path, "cardA"));
        File.WriteAllBytes(temp.File("cardA_0001.jpg"), [1]);
        File.WriteAllBytes(temp.File("cardA_0003.jpg"), [1]);
        Assert.Equal("cardA_0002.jpg", PictureNames.Suggest(temp.Path, "cardA"));
        Assert.Equal("a_b_c_0001.jpg", PictureNames.Suggest(temp.Path, "a/b:c"));
        Assert.Equal("VideoTape", PictureNames.Clean("  "));
        Assert.True(PictureNames.IsJpeg("x.JPE"));
        Assert.False(PictureNames.IsJpeg("x.png"));
    }

    [Fact]
    public async Task Take_picture_pauses_saves_jpeg_in_pictures_and_imports_it()
    {
        Harness.RequireFFmpeg();
        using var temp = new TempFolder();
        using var h = new Harness();
        (_, ContentItemViewModel picture) = Setup(h);
        h.Session.Project.Properties = h.Session.Project.Properties with { Author = "Tim" };
        MonitorViewModel m = h.Shell.Monitor;
        string? suggested = null, folder = null;
        string target = temp.File("shot");
        h.Files.PictureSave = (name, start) =>
        {
            suggested = name;
            folder = start;
            return target;
        };

        m.PlayProjectCommand.Execute(null);
        await h.Shell.TakePictureCommand.ExecuteAsync(null);
        Assert.False(m.IsPlaying);
        Assert.Equal("VideoTape_0001.jpg", suggested);
        Assert.Equal(AppPaths.PicturesDir, folder);
        Assert.True(Directory.Exists(AppPaths.PicturesDir));

        string saved = target + ".jpg";
        byte[] bytes = await File.ReadAllBytesAsync(saved, TestContext.Current.CancellationToken);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        IReadOnlyDictionary<ushort, string> tags = JpegMetadata.Read(bytes);
        Assert.Equal("shot", tags[0x9C9B]);
        Assert.Equal("Tim", tags[0x9C9D]);
        Assert.Equal("AvaMovieMaker", tags[0x0131]);
        Assert.Contains(h.Session.Project.Media, x => x.Path == saved && x.Kind == MediaKind.Picture);
        Assert.Equal(PreviewTarget.Project, m.Target);

        h.Shell.Contents.Select([picture]);
        h.Files.PictureSave = (name, _) =>
        {
            suggested = name;
            return null;
        };
        await h.Shell.TakePictureCommand.ExecuteAsync(null);
        Assert.Equal("flower_0001.jpg", suggested);
    }

    [Fact]
    public void Time_display_rounds_to_the_nearest_hundredth()
    {
        Assert.Equal("0:00:30.05", TimeFormat.Format(MediaTime.FromSeconds(30.047)));
        Assert.Equal("0:00:30.04", TimeFormat.Format(MediaTime.FromSeconds(30.044)));
        Assert.Equal("0:01:00.00", TimeFormat.Format(MediaTime.FromSeconds(59.996)));
    }

    [Fact]
    public void Item_playback_never_moves_the_timeline_indicator()
    {
        using var h = new Harness();
        (_, ContentItemViewModel picture) = Setup(h);
        MediaTime at = MediaTime.FromSeconds(1);
        h.Shell.Monitor.Seek(at);
        h.Shell.Contents.PreviewCommand.Execute(picture);
        try
        {
            SpinWait.SpinUntil(() => h.Shell.Monitor.Playback.Position > MediaTime.FromSeconds(0.2), 3000);
        }
        finally
        {
            Stop(h);
        }

        Assert.Equal(PlaybackState.Paused, h.Shell.Monitor.Playback.State);
        Assert.Equal(at, h.Shell.Timeline.Playhead);
    }
}
