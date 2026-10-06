using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Settings;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.AutoMovie;
using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void Reads_current_settings()
    {
        var settings = new AppSettings { OmitPublishedMetadata = true, AutoRecoveryMinutes = 7, CreateClipsOnImport = false, PictureDurationSeconds = 3 };
        var project = new ProjectSettings { Format = VideoFormat.Pal, Aspect = AspectRatio.Widescreen16x9, PictureDuration = MediaTime.FromSeconds(9) };
        var vm = new OptionsViewModel(settings, project);
        Assert.True(vm.OmitMetadata);
        Assert.Equal(7, vm.AutoRecoveryMinutes);
        Assert.False(vm.CreateClips);
        Assert.True(vm.IsPal);
        Assert.True(vm.IsWidescreen);
        Assert.Equal(3, vm.PictureSeconds, 6);
        Assert.Equal("(Default)", vm.PlaybackDevice);
        Assert.Equal("(Default)", vm.PlaybackDevices[0]);
    }

    [Fact]
    public void Apply_writes_settings_and_returns_project_settings()
    {
        var settings = new AppSettings();
        var vm = new OptionsViewModel(settings, new ProjectSettings())
        {
            OmitMetadata = true,
            OpenLastProject = true,
            TemporaryFolder = "/tmp/amm",
            AutoRecovery = false,
            AutoRecoveryMinutes = 99,
            CreateClips = false,
            HardwareDecode = false,
            HardwareEncode = true,
            PictureSeconds = 7,
            TransitionSeconds = 2,
            IsPal = true,
            IsWidescreen = true,
        };
        ProjectSettings p = vm.Apply(new ProjectSettings());
        Assert.True(settings.OmitPublishedMetadata);
        Assert.True(settings.OpenLastProjectOnStartup);
        Assert.Equal("/tmp/amm", settings.TemporaryFolder);
        Assert.False(settings.AutoRecoveryEnabled);
        Assert.Equal(60, settings.AutoRecoveryMinutes);
        Assert.False(settings.CreateClipsOnImport);
        Assert.False(settings.HardwareDecode);
        Assert.True(settings.HardwareEncode);
        Assert.Equal(string.Empty, settings.PlaybackDevice);
        Assert.Equal(MediaTime.FromSeconds(7), p.PictureDuration);
        Assert.Equal(MediaTime.FromSeconds(2), p.TransitionDuration);
        Assert.Equal(VideoFormat.Pal, p.Format);
        Assert.Equal(AspectRatio.Widescreen16x9, p.Aspect);

        Assert.Equal(7, settings.PictureDurationSeconds);
        Assert.Equal(2, settings.TransitionDurationSeconds);
        Assert.True(settings.PalVideo);
        Assert.True(settings.WidescreenVideo);
        Assert.Equal(p, OptionsViewModel.ForProject(settings));

        ProjectSettings opened = OptionsViewModel.ForProject(settings, new ProjectSettings { PictureDuration = MediaTime.FromSeconds(9) });
        Assert.Equal(MediaTime.FromSeconds(7), opened.PictureDuration);
        Assert.Equal(VideoFormat.Ntsc, opened.Format);
        Assert.Equal(AspectRatio.Standard4x3, opened.Aspect);
    }

    [Fact]
    public void Apply_clamps_durations()
    {
        var vm = new OptionsViewModel(new AppSettings(), new ProjectSettings()) { PictureSeconds = 1000, TransitionSeconds = 0.01 };
        ProjectSettings p = vm.Apply(new ProjectSettings());
        Assert.Equal(ProjectSettings.MaxPicture, p.PictureDuration);
        Assert.Equal(ProjectSettings.MinTransition, p.TransitionDuration);
    }

    [Fact]
    public void Restore_defaults()
    {
        var settings = new AppSettings();
        var vm = new OptionsViewModel(settings, new ProjectSettings())
        {
            PictureSeconds = 9,
            TransitionSeconds = 3,
            IsPal = true,
            IsWidescreen = true,
            AutoRecovery = false,
            AutoRecoveryMinutes = 30,
            OmitMetadata = true,
            OpenLastProject = true,
            TemporaryFolder = "/x",
            HardwareDecode = false,
            HardwareEncode = true,
        };
        vm.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(5, vm.PictureSeconds);
        Assert.Equal(1.25, vm.TransitionSeconds);
        Assert.False(vm.IsPal);
        Assert.False(vm.IsWidescreen);
        Assert.True(vm.AutoRecovery);
        Assert.Equal(10, vm.AutoRecoveryMinutes);
        Assert.False(vm.OmitMetadata);
        Assert.False(vm.OpenLastProject);
        Assert.Equal(Path.GetTempPath(), vm.TemporaryFolder);
        Assert.True(vm.HardwareDecode);
        Assert.False(vm.HardwareEncode);
        Assert.Equal(vm.PlaybackDevices[0], vm.PlaybackDevice);

        ProjectSettings p = vm.Apply(new ProjectSettings { Format = VideoFormat.Pal });
        Assert.Equal(VideoFormat.Ntsc, p.Format);
        Assert.Equal(AspectRatio.Standard4x3, p.Aspect);

        Assert.Equal(string.Empty, settings.TemporaryFolder);
    }

    [Fact]
    public void Reset_warnings_clears_dismissed_prompts_on_apply_only()
    {
        var settings = new AppSettings { DismissedWarnings = ["overlay-needs-timeline", "font-missing"] };
        var keep = new OptionsViewModel(settings, new ProjectSettings());
        keep.Apply(new ProjectSettings());
        Assert.Equal(2, settings.DismissedWarnings.Count);

        var vm = new OptionsViewModel(settings, new ProjectSettings());
        vm.ResetWarningsCommand.Execute(null);
        Assert.True(vm.WarningsReset);
        Assert.Equal(2, settings.DismissedWarnings.Count);
        vm.Apply(new ProjectSettings());
        Assert.Empty(settings.DismissedWarnings);
    }
}

public sealed class AutoMovieTests
{
    [Fact]
    public void Pages_and_headings()
    {
        using var h = new Harness();
        AutoMovieViewModel a = h.Shell.AutoMovie;
        Assert.Equal(0, a.Page);
        Assert.True(a.IsStylePage);
        Assert.Equal("Select an AutoMovie editing style", a.Heading);

        a.ShowTitleCommand.Execute(null);
        Assert.Equal(1, a.Page);
        Assert.True(a.IsTitlePage);
        Assert.Equal("Enter text for title", a.Heading);

        a.ShowMusicCommand.Execute(null);
        Assert.Equal(2, a.Page);
        Assert.True(a.IsMusicPage);
        Assert.Equal("Add audio or background music", a.Heading);

        a.ShowStylesCommand.Execute(null);
        Assert.Equal("Select an AutoMovie editing style", a.Heading);

        a.ShowMusicCommand.Execute(null);
        h.Shell.AutoMovieCommand.Execute(null);
        Assert.Equal(0, a.Page);
        Assert.Equal(UpperPane.AutoMovie, h.Shell.Pane);
        a.CancelCommand.Execute(null);
        Assert.Equal(UpperPane.Contents, h.Shell.Pane);
    }

    [Fact]
    public void Six_styles_with_flip_and_slide_selected()
    {
        using var h = new Harness();
        AutoMovieViewModel a = h.Shell.AutoMovie;
        Assert.Equal(6, a.Styles.Count);
        Assert.Equal(["Fade and Reveal", "Flip and Slide", "Highlights Movie", "Music Video", "Old Movie", "Sports Highlights"], a.Styles.Select(s => s.Name));
        Assert.Same(a.Styles[1], a.SelectedStyle);
        Assert.Empty(a.MusicChoices);
    }

    [Fact]
    public async Task Create_without_media_warns()
    {
        using var h = new Harness();
        await h.Shell.AutoMovie.CreateCommand.ExecuteAsync(null);
        Assert.Equal(Strings.AutoMovieNeedsClips, Assert.Single(h.Messages.Shown).Text);
        Assert.True(h.Session.Project.IsEmpty);
        Assert.False(h.Shell.AutoMovie.IsBusy);
    }

    [Fact]
    public async Task Create_needs_thirty_seconds_of_content()
    {
        using var h = new Harness();
        h.Session.Project.Media.Add(new AvaMovieMaker.Timeline.Model.MediaItem
        {
            Kind = AvaMovieMaker.Media.MediaKind.Video,
            Path = "/media/short.mp4",
            Name = "short",
            Duration = AvaMovieMaker.Time.MediaTime.FromSeconds(20),
            Clips = [new AvaMovieMaker.Timeline.Model.SourceClip { Name = "short", End = AvaMovieMaker.Time.MediaTime.FromSeconds(20) }],
        });
        h.Session.Project.Media.Add(new AvaMovieMaker.Timeline.Model.MediaItem { Kind = AvaMovieMaker.Media.MediaKind.Picture, Path = "/media/p.jpg", Name = "p" });
        h.Shell.Contents.Refresh();
        await h.Shell.AutoMovie.CreateCommand.ExecuteAsync(null);
        Assert.Equal(Strings.AutoMovieNotEnoughContent, Assert.Single(h.Messages.Shown).Text);
        Assert.Empty(h.Session.Project.VideoTrack);
    }
}

public sealed class ClipEffectsTests
{
    [Fact]
    public void Starts_with_the_clip_effects_and_drops_unknown_ids()
    {
        string a = EffectCatalog.All[0].Id, b = EffectCatalog.All[1].Id;
        var vm = new ClipEffectsViewModel([a, "no-such-effect", b]);
        Assert.Equal([a, b], vm.Result);
        Assert.Equal(EffectCatalog.All.Count, vm.Available.Count);
        Assert.Equal(vm.Available.Select(e => e.Name).Order(StringComparer.CurrentCulture), vm.Available.Select(e => e.Name));
    }

    [Fact]
    public void Add_remove_and_reorder()
    {
        var vm = new ClipEffectsViewModel([]);
        Assert.False(vm.AddCommand.CanExecute(null));
        Assert.False(vm.RemoveCommand.CanExecute(null));

        vm.SelectedAvailable = vm.Available[0];
        Assert.True(vm.AddCommand.CanExecute(null));
        vm.AddCommand.Execute(null);
        vm.SelectedAvailable = vm.Available[1];
        vm.AddCommand.Execute(null);
        vm.SelectedAvailable = vm.Available[2];
        vm.AddCommand.Execute(null);
        string e0 = vm.Available[0].Id, e1 = vm.Available[1].Id, e2 = vm.Available[2].Id;
        Assert.Equal([e0, e1, e2], vm.Result);
        Assert.Equal(e2, vm.SelectedDisplayed!.Id);
        Assert.False(vm.MoveDownCommand.CanExecute(null));
        Assert.True(vm.MoveUpCommand.CanExecute(null));

        vm.MoveUpCommand.Execute(null);
        Assert.Equal([e0, e2, e1], vm.Result);
        Assert.Equal(e2, vm.SelectedDisplayed!.Id);
        vm.MoveUpCommand.Execute(null);
        Assert.Equal([e2, e0, e1], vm.Result);
        Assert.False(vm.MoveUpCommand.CanExecute(null));
        vm.MoveDownCommand.Execute(null);
        Assert.Equal([e0, e2, e1], vm.Result);

        vm.RemoveCommand.Execute(null);
        Assert.Equal([e0, e1], vm.Result);
        Assert.Equal(e1, vm.SelectedDisplayed!.Id);
        vm.RemoveCommand.Execute(null);
        Assert.Equal(e0, vm.SelectedDisplayed!.Id);
        vm.RemoveCommand.Execute(null);
        Assert.Empty(vm.Result);
        Assert.Null(vm.SelectedDisplayed);
        Assert.False(vm.RemoveCommand.CanExecute(null));
    }
}

public sealed class ClipVolumeTests
{
    [Fact]
    public void Reads_percent_and_resets()
    {
        var vm = new ClipVolumeViewModel(new AudioSettings { Volume = 0.5, Mute = true, FadeIn = true });
        Assert.Equal(50, vm.Volume, 6);
        Assert.True(vm.Mute);
        vm.ResetCommand.Execute(null);
        Assert.Equal(100, vm.Volume);
        Assert.False(vm.Mute);
        AudioSettings s = vm.ToSettings();
        Assert.Equal(1.0, s.Volume, 6);
        Assert.False(s.Mute);
        Assert.True(s.FadeIn);
    }

    [Fact]
    public void Clamps_to_100_percent_as_movie_maker()
    {
        var vm = new ClipVolumeViewModel(AudioSettings.Default) { Volume = 400 };
        Assert.Equal(1.0, vm.ToSettings().Volume, 6);
        vm.Volume = -5;
        Assert.Equal(0, vm.ToSettings().Volume, 6);

        Assert.Equal(100, new ClipVolumeViewModel(new AudioSettings { Volume = 1.4 }).Volume, 6);
    }
}

public sealed class ProjectPropertiesTests
{
    [Fact]
    public void Fills_the_default_author_and_round_trips()
    {
        var vm = new ProjectPropertiesViewModel(new ProjectProperties { Title = "T", Copyright = "C", Rating = "R", Description = "D" }, MediaTime.FromSeconds(65), "Default Author");
        Assert.Equal("Default Author", vm.Author);
        Assert.False(string.IsNullOrEmpty(vm.TotalTime));
        Assert.Equal(new ProjectProperties { Title = "T", Author = "Default Author", Copyright = "C", Rating = "R", Description = "D" }, vm.ToProperties());

        var withAuthor = new ProjectPropertiesViewModel(new ProjectProperties { Author = "Someone" }, MediaTime.Zero, "Default Author");
        Assert.Equal("Someone", withAuthor.Author);
    }

    [Fact]
    public void Empty_title_shows_the_project_name_without_storing_it()
    {
        var vm = new ProjectPropertiesViewModel(new ProjectProperties(), MediaTime.Zero, string.Empty, "steps-basic");
        Assert.Equal("steps-basic", vm.Title);
        Assert.Equal(string.Empty, vm.Author);
        Assert.Equal(string.Empty, vm.ToProperties().Title);
        vm.Title = "Real title";
        Assert.Equal("Real title", vm.ToProperties().Title);

        Assert.Equal(string.Empty, new ProjectPropertiesViewModel(new ProjectProperties(), MediaTime.Zero, string.Empty).Title);
    }

    [Fact]
    public void Text_is_cut_to_movie_maker_limits()
    {
        var vm = new ProjectPropertiesViewModel(new ProjectProperties(), MediaTime.Zero, string.Empty)
        {
            Title = new string('t', 200),
            Author = new string('a', 200),
            Rating = new string('r', 30),
            Description = new string('d', 600),
        };
        ProjectProperties p = vm.ToProperties();
        Assert.Equal((128, 128, 20, 512), (p.Title.Length, p.Author.Length, p.Rating.Length, p.Description.Length));
    }

    [Fact]
    public void Apply_raises_apply_requested()
    {
        var vm = new ProjectPropertiesViewModel(new ProjectProperties(), MediaTime.Zero, "A");
        int raised = 0;
        vm.ApplyRequested += (_, _) => raised++;
        vm.Title = "New";
        vm.Apply();
        Assert.Equal(1, raised);
        Assert.Equal("New", vm.ToProperties().Title);
    }
}
