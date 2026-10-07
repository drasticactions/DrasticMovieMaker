using AvaMovieMaker.Media;
using AvaMovieMaker.Settings;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Publish;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class ShellTests
{
    [Fact]
    public void Window_title_is_the_app_name_only()
    {
        using var h = new Harness();
        Assert.Equal("Drastic Movie Maker", h.Shell.WindowTitle);
        h.AddTitle("Hello");
        Assert.Equal("Drastic Movie Maker", h.Shell.WindowTitle);
    }

    [Fact]
    public void Menu_wording_follows_the_board_in_view()
    {
        using var h = new Harness();
        var changed = new List<string?>();
        h.Shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(h.Shell.IsTimeline);
        Assert.Equal("C_lear Storyboard", h.Shell.ClearText);
        Assert.Equal("_Add to Storyboard", h.Shell.AddToText);
        Assert.Equal("_Play Storyboard", h.Shell.PlayBoardText);
        Assert.Equal("_Rewind Storyboard", h.Shell.RewindBoardText);

        h.Shell.IsTimeline = true;
        Assert.Equal("C_lear Timeline", h.Shell.ClearText);
        Assert.Equal("_Add to Timeline", h.Shell.AddToText);
        Assert.Equal("_Play Timeline", h.Shell.PlayBoardText);
        Assert.Equal("_Rewind Timeline", h.Shell.RewindBoardText);
        Assert.Contains(nameof(ShellViewModel.ClearText), changed);
        Assert.Contains(nameof(ShellViewModel.AddToText), changed);
        Assert.Contains(nameof(ShellViewModel.PlayBoardText), changed);
        Assert.True(h.Store.Settings.ShowTimeline);

        h.Shell.ToggleStoryboardTimelineCommand.Execute(null);
        Assert.False(h.Shell.IsTimeline);
        Assert.Equal("C_lear Storyboard", h.Shell.ClearText);
    }

    [Fact]
    public void Timeline_setting_is_restored_on_start()
    {
        using var h = new Harness(new AppSettings { AutoRecoveryEnabled = false, ShowTimeline = true });
        Assert.True(h.Shell.IsTimeline);
        Assert.Equal("_Play Timeline", h.Shell.PlayBoardText);
    }

    [Fact]
    public void Publish_needs_a_clip()
    {
        using var h = new Harness();
        Assert.False(h.Shell.CanPublish);
        Assert.False(h.Shell.PublishCommand.CanExecute(null));
        Assert.False(h.Shell.PublishToComputerCommand.CanExecute(null));

        h.AddTitle("Hello");
        Assert.True(h.Shell.CanPublish);
        Assert.True(h.Shell.PublishCommand.CanExecute(null));
        Assert.True(h.Shell.PublishToComputerCommand.CanExecute(null));

        h.Shell.UndoCommand.Execute(null);
        Assert.False(h.Shell.CanPublish);
        Assert.False(h.Shell.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task Publish_starts_on_where_and_this_computer_on_name()
    {
        using var h = new Harness();
        h.AddTitle("Hello");
        await h.Shell.PublishCommand.ExecuteAsync(null);
        Assert.Equal(PublishPage.Where, Assert.IsType<PublishWizardViewModel>(h.Dialogs.Shown[^1]).Page);
        await h.Shell.PublishToComputerCommand.ExecuteAsync(null);
        Assert.Equal(PublishPage.Name, Assert.IsType<PublishWizardViewModel>(h.Dialogs.Shown[^1]).Page);
    }

    [Fact]
    public void Undo_and_redo_history()
    {
        using var h = new Harness();
        Assert.False(h.Shell.HasUndo);
        Assert.False(h.Shell.HasRedo);
        Assert.False(h.Shell.UndoCommand.CanExecute(null));
        Assert.Equal("_Undo", h.Shell.UndoText);
        Assert.Empty(h.Shell.UndoHistory);

        h.AddTitle("One");
        h.AddTitle("Two");
        h.AddTitle("Three");
        Assert.True(h.Shell.HasUndo);
        Assert.Equal("_Undo Add Title", h.Shell.UndoText);
        Assert.Equal([1, 2, 3], h.Shell.UndoHistory.Select(e => e.Count));
        Assert.All(h.Shell.UndoHistory, e => Assert.Equal(UndoNames.AddTitle, e.Name));
        Assert.Same(h.Shell.UndoManyCommand, h.Shell.UndoHistory[0].Command);

        h.Shell.UndoHistory[1].Command.Execute(h.Shell.UndoHistory[1].Count);
        Assert.Single(h.Session.Project.VideoTrack);
        Assert.True(h.Shell.HasRedo);
        Assert.Equal(2, h.Shell.RedoHistory.Count);
        Assert.Equal("_Redo Add Title", h.Shell.RedoText);
        Assert.Same(h.Shell.RedoManyCommand, h.Shell.RedoHistory[0].Command);

        h.Shell.RedoManyCommand.Execute(1);
        Assert.Equal(2, h.Session.Project.VideoTrack.Count);
        Assert.Single(h.Shell.RedoHistory);

        h.Shell.UndoManyCommand.Execute(10);
        Assert.Empty(h.Session.Project.VideoTrack);
        Assert.False(h.Shell.HasUndo);
        Assert.Equal(3, h.Shell.RedoHistory.Count);
        Assert.False(h.Shell.UndoCommand.CanExecute(null));
        Assert.True(h.Shell.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void Task_pane_edit_entries_switch_the_contents_view()
    {
        using var h = new Harness();
        TaskSection edit = h.Shell.Tasks.Sections[1];
        edit.Entries[1].Command.Execute(null);
        Assert.Equal(ContentsView.Effects, h.Shell.Contents.View);
        edit.Entries[2].Command.Execute(null);
        Assert.Equal(ContentsView.Transitions, h.Shell.Contents.View);
        edit.Entries[0].Command.Execute(null);
        Assert.Equal(ContentsView.ImportedMedia, h.Shell.Contents.View);
        edit.Entries[3].Command.Execute(null);
        Assert.Equal(UpperPane.Titles, h.Shell.Pane);
        Assert.True(h.Shell.IsTitlesPane);
    }

    [Fact]
    public async Task Start_imports_media_given_on_the_command_line()
    {
        Harness.RequireFFmpeg();
        string video = TestMedia.CounterCard(seconds: 1);
        string picture = TestMedia.Picture();
        using var h = new Harness();
        await h.Shell.StartAsync([video, picture]);

        Assert.Equal(2, h.Session.Project.Media.Count);
        Assert.Contains(h.Session.Project.Media, m => m.Kind == MediaKind.Video && m.Path == video);
        Assert.Contains(h.Session.Project.Media, m => m.Kind == MediaKind.Picture && m.Path == picture);
        Assert.Equal(ContentsView.ImportedMedia, h.Shell.Contents.View);
        Assert.Equal(UpperPane.Contents, h.Shell.Pane);
        Assert.True(h.Shell.CanAutoMovie);
        Assert.False(h.Shell.CanPublish);
        Assert.False(h.Shell.IsBusy);
        Assert.Empty(h.Messages.Shown);
    }

    [Fact]
    public async Task Import_warns_about_unsupported_files()
    {
        using var temp = new TempFolder();
        string bad = temp.File("notes.txt");
        await File.WriteAllTextAsync(bad, "x", TestContext.Current.CancellationToken);
        using var h = new Harness();
        IReadOnlyList<AvaMovieMaker.Timeline.Model.MediaItem> items = await h.Shell.ImportFilesAsync([bad]);
        Assert.Empty(items);
        Assert.Single(h.Messages.Shown);
        Assert.Equal($"The file {bad} is not a supported file type, and it cannot be imported into Drastic Movie Maker.", h.Messages.Shown[0].Text);
    }

    [Fact]
    public async Task Project_properties_apply_writes_through_the_editor()
    {
        using var h = new Harness();
        h.Dialogs.OnShow = vm =>
        {
            var p = (ProjectPropertiesViewModel)vm;
            p.Title = "Applied";
            p.Apply();
        };
        h.Dialogs.Result = false;
        await h.Shell.ProjectPropertiesCommand.ExecuteAsync(null);
        Assert.Equal("Applied", h.Session.Project.Properties.Title);
        Assert.True(h.Shell.HasUndo);
    }

    [Fact]
    public async Task Options_ok_applies_settings_and_saves()
    {
        using var h = new Harness();
        h.Dialogs.OnShow = vm =>
        {
            var o = (OptionsViewModel)vm;
            o.OmitMetadata = true;
            o.Aspect = AvaMovieMaker.Timeline.Model.AspectRatio.Widescreen16x9;
        };
        h.Dialogs.Result = true;
        int saves = h.Store.Saves;
        await h.Shell.OptionsCommand.ExecuteAsync(null);
        Assert.True(h.Store.Settings.OmitPublishedMetadata);
        Assert.Equal(AvaMovieMaker.Timeline.Model.AspectRatio.Widescreen16x9, h.Session.Project.Settings.Aspect);
        Assert.True(h.Store.Saves > saves);
    }

    [Fact]
    public async Task Options_are_per_user_not_undoable_and_survive_new()
    {
        using var h = new Harness();
        h.AddTitle("Before");
        h.Dialogs.OnShow = vm => ((OptionsViewModel)vm).PictureSeconds = 3;
        h.Dialogs.Result = true;
        await h.Shell.OptionsCommand.ExecuteAsync(null);
        Assert.Equal(3, h.Store.Settings.PictureDurationSeconds);
        Assert.Equal(AvaMovieMaker.Time.MediaTime.FromSeconds(3), h.Session.Project.Settings.PictureDuration);
        Assert.Equal(UndoNames.AddTitle, h.Session.Undo.UndoName);

        h.Shell.UndoCommand.Execute(null);
        Assert.Equal(AvaMovieMaker.Time.MediaTime.FromSeconds(3), h.Session.Project.Settings.PictureDuration);
        Assert.False(h.Session.Undo.IsDirty);

        await h.Shell.NewProjectCommand.ExecuteAsync(null);
        Assert.Equal(AvaMovieMaker.Time.MediaTime.FromSeconds(3), h.Session.Project.Settings.PictureDuration);
    }

    [Fact]
    public async Task Opened_project_takes_the_users_durations_and_keeps_its_aspect()
    {
        using var temp = new TempFolder();
        string path = temp.File("old.dmmproj");
        var old = new AvaMovieMaker.Timeline.Model.Project
        {
            Settings = new AvaMovieMaker.Timeline.Model.ProjectSettings
            {
                PictureDuration = AvaMovieMaker.Time.MediaTime.FromSeconds(9),
                Aspect = AvaMovieMaker.Timeline.Model.AspectRatio.Widescreen16x9,
            },
        };
        AvaMovieMaker.Timeline.Serialization.ProjectSerializer.Save(old, path);
        using var h = new Harness(new AppSettings { AutoRecoveryEnabled = false, PictureDurationSeconds = 4 });
        Assert.True(await h.Shell.OpenFileAsync(path));
        Assert.Equal(AvaMovieMaker.Time.MediaTime.FromSeconds(4), h.Session.Project.Settings.PictureDuration);
        Assert.Equal(AvaMovieMaker.Timeline.Model.AspectRatio.Widescreen16x9, h.Session.Project.Settings.Aspect);
        Assert.False(h.Session.Undo.IsDirty);
    }

    [Fact]
    public async Task Save_prompt_uses_movie_makers_wording()
    {
        using var h = new Harness();
        h.AddTitle("x");
        h.Messages.Answer = AvaMovieMaker.ViewModels.Services.MessageResult.Cancel;
        Assert.False(await h.Shell.ConfirmDiscardAsync());
        Assert.Equal("Do you want to save the changes you made to the project Untitled?", Assert.Single(h.Messages.Shown).Text);
    }

    [Fact]
    public void Undo_drop_down_lists_16_rows_with_a_footer()
    {
        using var h = new Harness();
        for (int i = 0; i < 20; i++)
        {
            h.AddTitle($"t{i}");
        }

        Assert.Equal(16, h.Shell.UndoHistory.Count);
        Assert.Equal("Undo 1 Action", h.Shell.UndoHistory[0].Footer);
        Assert.Equal("Undo 3 Actions", h.Shell.UndoHistory[2].Footer);
        h.Shell.UndoManyCommand.Execute(2);
        Assert.Equal("Redo 2 Actions", h.Shell.RedoHistory[1].Footer);
    }

    [Fact]
    public void Recent_list_holds_4_by_default_and_2_to_16()
    {
        var s = new AppSettings();
        for (int i = 0; i < 10; i++)
        {
            s.AddRecent($"/p/{i}.dmmproj");
        }

        Assert.Equal(4, s.RecentProjects.Count);
        Assert.Equal("/p/9.dmmproj", s.RecentProjects[0]);
        s.RecentProjectCount = 100;
        Assert.Equal(16, s.RecentLimit);
        s.RecentProjectCount = 0;
        Assert.Equal(2, s.RecentLimit);
        s.AddRecent("/p/x.dmmproj");
        Assert.Equal(2, s.RecentProjects.Count);
    }

    [Fact]
    public void Theme_command_applies_and_saves()
    {
        using var h = new Harness();
        h.Shell.SetThemeCommand.Execute("Dark");
        Assert.Equal("Dark", h.Store.Settings.Theme);
        Assert.Contains("theme:Dark", h.Dialogs.Shown);
    }
}
