using System.Diagnostics;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Timeline.Export;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Publish;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class PublishWizardTests
{
    private static PublishWizardViewModel Wizard(Harness h) =>
        new(h.Session, h.Engine, h.Files, h.Dialogs, h.Messages, h.Dispatcher, h.Store.Settings);

    [Fact]
    public void Starts_on_where_and_walks_to_settings()
    {
        using var h = new Harness();
        h.AddTitle("Movie");
        PublishWizardViewModel w = Wizard(h);
        Assert.Equal(PublishPage.Where, w.Page);
        Assert.True(w.IsWherePage);
        Assert.Equal("Where do you want to publish your movie?", w.Heading);
        Assert.False(w.CanGoBack);
        Assert.Equal("_Next", w.NextText);
        Assert.Single(w.Destinations);

        w.NextCommand.Execute(null);
        Assert.Equal(PublishPage.Name, w.Page);
        Assert.Equal("Name the movie you are publishing", w.Heading);
        Assert.True(w.CanGoBack);
        Assert.Equal("Movie", w.FileName);

        w.NextCommand.Execute(null);
        Assert.Equal(PublishPage.Settings, w.Page);
        Assert.Equal("Choose the settings for your movie", w.Heading);
        Assert.Equal("_Publish", w.NextText);

        w.BackCommand.Execute(null);
        Assert.Equal(PublishPage.Name, w.Page);
        w.BackCommand.Execute(null);
        Assert.Equal(PublishPage.Where, w.Page);
    }

    [Theory]
    [InlineData("", "", "Movie")]
    [InlineData("My Holiday", "/x/project.ammproj", "My Holiday")]
    [InlineData("", "/x/project.ammproj", "project")]
    [InlineData("  <Best> [cut]: 2006?  ", null, "Best cut 2006")]
    [InlineData("a|b;c+d=e(f)g\"h*i\\j/k", null, "abcdefghijk")]
    public void Default_name_is_title_then_project_name_then_movie(string title, string? file, string expected)
    {
        var p = new Project { Properties = new ProjectProperties { Title = title }, FilePath = string.IsNullOrEmpty(file) ? null : file };
        Assert.Equal(expected, PublishWizardViewModel.DefaultName(p));
    }

    [Fact]
    public void Default_name_is_cut_to_64_characters()
    {
        var p = new Project { Properties = new ProjectProperties { Title = new string('a', 100) } };
        Assert.Equal(64, PublishWizardViewModel.DefaultName(p).Length);
    }

    [Fact]
    public void Name_page_never_proposes_an_existing_file()
    {
        using var temp = new TempFolder();
        using var h = new Harness();
        h.AddTitle("Movie");
        h.Store.Settings.LastPublishFolder = temp.Path;
        File.WriteAllBytes(temp.File("Movie.webm"), [0]);
        PublishWizardViewModel w = Wizard(h);
        w.Page = PublishPage.Name;
        Assert.Equal("Movie_0001", w.FileName);

        File.WriteAllBytes(temp.File("Movie_0001.mp4"), [0]);
        File.WriteAllBytes(temp.File("Movie_0002.txt"), [0]);
        PublishWizardViewModel again = Wizard(h);
        again.Page = PublishPage.Name;
        Assert.Equal("Movie_0003", again.FileName);

        using var empty = new TempFolder();
        again.Folder = empty.Path;
        Assert.Equal("Movie", again.FileName);
        again.FileName = "Mine";
        again.Folder = temp.Path;
        Assert.Equal("Mine", again.FileName);
    }

    [Fact]
    public async Task Typed_existing_name_asks_before_replacing()
    {
        using var temp = new TempFolder();
        using var h = new Harness();
        h.AddTitle("Movie");
        File.WriteAllBytes(temp.File("old.mp4"), [1, 2, 3]);
        PublishWizardViewModel w = Wizard(h);
        w.Folder = temp.Path;
        w.FileName = "old";
        w.Page = PublishPage.Settings;
        h.Messages.Answer = MessageResult.No;
        await w.NextCommand.ExecuteAsync(null);
        MessageRequest ask = Assert.Single(h.Messages.Shown);
        Assert.Equal(MessageButtons.YesNo, ask.Buttons);
        Assert.Contains("old.mp4 already exists", ask.Text, StringComparison.Ordinal);
        Assert.Equal(PublishPage.Settings, w.Page);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(temp.File("old.mp4")));
    }

    [Fact]
    public async Task Missing_source_files_block_publishing()
    {
        using var temp = new TempFolder();
        using var h = new Harness();
        var media = new MediaItem { Kind = Media.MediaKind.Picture, Name = "gone", Path = "/nowhere/gone.png", Missing = true };
        h.Session.Editor.ImportMedia([media]);
        h.Session.Editor.InsertVideoClips(0, [h.Session.Editor.NewVideoClip(media)]);
        PublishWizardViewModel w = Wizard(h);
        w.Folder = temp.Path;
        w.Page = PublishPage.Settings;
        await w.NextCommand.ExecuteAsync(null);
        MessageRequest m = Assert.Single(h.Messages.Shown);
        Assert.Equal("Cannot complete Publish Movie", m.Title);
        Assert.StartsWith("This project contains source files that are missing", m.Text, StringComparison.Ordinal);
        Assert.Equal(PublishPage.Settings, w.Page);
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public async Task Empty_project_is_not_published()
    {
        using var temp = new TempFolder();
        using var h = new Harness();
        PublishWizardViewModel w = Wizard(h);
        w.Folder = temp.Path;
        w.Page = PublishPage.Settings;
        await w.NextCommand.ExecuteAsync(null);
        Assert.Equal("Your project is empty. Please add clips to the storyboard/timeline.", Assert.Single(h.Messages.Shown).Text);
        Assert.Equal(PublishPage.Settings, w.Page);
    }

    [Fact]
    public async Task Cancel_while_publishing_asks_first()
    {
        using var h = new Harness();
        h.AddTitle("Movie");
        PublishWizardViewModel w = Wizard(h);
        int closed = 0;
        w.Closed += (_, _) => closed++;
        w.Page = PublishPage.Progress;
        h.Messages.Answer = MessageResult.No;
        await w.CancelCommand.ExecuteAsync(null);
        MessageRequest ask = Assert.Single(h.Messages.Shown);
        Assert.Equal("Your movie has not been completely published.\nAre you sure you want to stop publishing your movie?", ask.Text);
        Assert.Equal(MessageButtons.YesNo, ask.Buttons);
        Assert.Equal(PublishPage.Progress, w.Page);
        Assert.Equal(0, closed);

        w.Page = PublishPage.Settings;
        await w.CancelCommand.ExecuteAsync(null);
        Assert.Equal(1, closed);
        Assert.Single(h.Messages.Shown);
    }

    [Fact]
    public void Play_movie_choice_is_remembered()
    {
        using var h = new Harness();
        h.AddTitle("Movie");
        PublishWizardViewModel w = Wizard(h);
        Assert.True(w.PlayWhenFinished);
        w.Page = PublishPage.Finish;
        w.PlayWhenFinished = false;
        w.NextCommand.Execute(null);
        Assert.False(h.Store.Settings.PlayPublishedMovie);
        Assert.False(Wizard(h).PlayWhenFinished);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("nul\0l")]
    [InlineData("a:b")]
    [InlineData("why?")]
    [InlineData("[draft]")]
    public void Invalid_file_names_disable_next(string name)
    {
        using var h = new Harness();
        PublishWizardViewModel w = Wizard(h);
        w.Page = PublishPage.Name;
        Assert.True(w.NextCommand.CanExecute(null));
        w.FileName = name;
        Assert.False(w.NextCommand.CanExecute(null));
        w.FileName = "Fine name";
        Assert.True(w.NextCommand.CanExecute(null));
        w.Folder = string.Empty;
        Assert.False(w.NextCommand.CanExecute(null));
    }

    [Fact]
    public void Mode_selects_the_effective_profile()
    {
        using var h = new Harness();
        h.AddTitle("Movie");
        PublishWizardViewModel w = Wizard(h);
        Assert.Equal(PublishMode.Best, w.Mode);
        Assert.True(w.IsBest);
        Assert.Same(PublishProfiles.Recommended, w.EffectiveProfile);

        w.IsCompress = true;
        w.CompressMegabytes = 25;
        Assert.Equal(PublishMode.Compress, w.Mode);
        Assert.Equal(PublishProfile.CompressTo(25L * 1024 * 1024, h.Session.Project.Duration), w.EffectiveProfile);
        Assert.Equal("compress", w.EffectiveProfile.Id);

        w.IsMore = true;
        ProfileOption web = w.MoreProfiles.First(p => p.Profile.Id == "web");
        w.SelectedProfile = web;
        Assert.Same(web.Profile, w.EffectiveProfile);
        Assert.DoesNotContain(w.MoreProfiles, p => p.Profile == PublishProfiles.Recommended);
        Assert.EndsWith(".mp4", w.OutputPath, StringComparison.Ordinal);

        w.SelectedProfile = w.MoreProfiles.First(p => p.Profile.Container == Media.Encoders.ContainerFormat.WebM);
        Assert.EndsWith(".webm", w.OutputPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Portrait_projects_hide_the_dvd_profile_and_show_their_size()
    {
        using var h = new Harness();
        h.AddTitle("Movie");
        Assert.Contains(Wizard(h).Profiles, p => p.Profile.Id == "dvd");
        h.Session.Editor.SetAspect(AvaMovieMaker.Timeline.Model.AspectRatio.Vertical9x16);
        PublishWizardViewModel w = Wizard(h);
        Assert.DoesNotContain(w.Profiles, p => p.Profile.Anamorphic);
        Assert.NotNull(w.SelectedProfile);
        Assert.Contains("Aspect ratio: 9:16", w.MovieSettingsText, StringComparison.Ordinal);
        w.IsMore = true;
        w.SelectedProfile = w.MoreProfiles.First(p => p.Profile.Id == "hd1080");
        Assert.Matches(@"1080\D+1920", w.MovieSettingsText);
    }

    [Fact]
    public void Settings_and_file_size_boxes_are_filled()
    {
        using var h = new Harness();
        h.AddTitle("Movie");
        PublishWizardViewModel w = Wizard(h);
        var changed = new List<string?>();
        w.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Contains("File type: MP4 (H.264, AAC)", w.MovieSettingsText, StringComparison.Ordinal);
        Assert.Contains("Aspect ratio: 4:3", w.MovieSettingsText, StringComparison.Ordinal);
        Assert.Equal(7, w.Details.Count);

        Assert.Contains("will vary based on the content", w.FileSizeText, StringComparison.Ordinal);
        Assert.DoesNotContain("Estimated space required", w.FileSizeText, StringComparison.Ordinal);
        Assert.Contains("Estimated disk space available", w.FileSizeText, StringComparison.Ordinal);

        w.IsCompress = true;
        Assert.Contains("Estimated space required", w.FileSizeText, StringComparison.Ordinal);
        Assert.Contains("Estimated disk space available", w.FileSizeText, StringComparison.Ordinal);
        Assert.Contains(nameof(PublishWizardViewModel.MovieSettingsText), changed);
        Assert.Contains(nameof(PublishWizardViewModel.FileSizeText), changed);
        Assert.Contains(nameof(PublishWizardViewModel.EffectiveProfile), changed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Omit_metadata_setting_controls_the_published_tags(bool omit)
    {
        Harness.RequireFFmpeg();
        Assert.SkipUnless(TestMedia.FFmpegCliAvailable, "ffprobe CLI not installed");
        using var temp = new TempFolder();
        using var h = new Harness();
        h.Store.Settings.OmitPublishedMetadata = omit;
        h.AddTitle("Movie");
        h.Session.Editor.SetProperties(new ProjectProperties { Title = "Metadata Title", Author = "Metadata Author" });
        PublishWizardViewModel w = Wizard(h);
        w.Folder = temp.Path;
        w.FileName = "movie";
        w.IsMore = true;
        w.SelectedProfile = w.MoreProfiles.First(p => p.Profile.Id == "web");
        w.Page = PublishPage.Settings;
        await w.NextCommand.ExecuteAsync(null);

        Assert.Null(w.Error);
        Assert.Equal(PublishPage.Finish, w.Page);
        Assert.Equal("Your movie has been published", w.Heading);
        Assert.True(File.Exists(w.OutputPath));
        string tags = Probe(w.OutputPath);
        if (omit)
        {
            Assert.DoesNotContain("Metadata Title", tags, StringComparison.Ordinal);
            Assert.DoesNotContain("Metadata Author", tags, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("Metadata Title", tags, StringComparison.Ordinal);
            Assert.Contains("Metadata Author", tags, StringComparison.Ordinal);
        }
    }

    private static string Probe(string path)
    {
        var psi = new ProcessStartInfo("ffprobe", ["-v", "error", "-show_entries", "format_tags", "-of", "default", path])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using Process p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return output;
    }
}
