using AvaMovieMaker.IO;
using AvaMovieMaker.Media;
using AvaMovieMaker.Settings;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Timeline.Import;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Serialization;
using AvaMovieMaker.ViewModels.Contents;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class ImportTests
{
    [Fact]
    public async Task Import_makes_one_item_per_file_without_splitting_videos()
    {
        Harness.RequireFFmpeg();
        string scenes = TestMedia.SceneCuts([2.0, 4.0], 6);
        using var h = new Harness(new AppSettings { AutoRecoveryEnabled = false, CreateClipsOnImport = true });
        IReadOnlyList<MediaItem> items = await h.Shell.ImportFilesAsync([scenes]);

        MediaItem item = Assert.Single(items);
        SourceClip clip = Assert.Single(item.Clips);
        Assert.Equal(Path.GetFileNameWithoutExtension(scenes), item.Name);
        Assert.Equal(item.Name, clip.Name);
        Assert.Single(h.Shell.Contents.Items);
        Assert.Equal("Import Media Items", h.Session.Undo.UndoName);
    }

    [Fact]
    public void Clips_are_not_created_on_import_by_default()
    {
        Assert.False(new AppSettings().CreateClipsOnImport);
    }

    [Fact]
    public async Task Create_clips_splits_the_selected_video_into_numbered_clips()
    {
        Harness.RequireFFmpeg();
        string scenes = TestMedia.SceneCuts([2.0, 4.0], 6);
        using var h = new Harness();
        await h.Shell.ImportFilesAsync([scenes]);
        h.Shell.Contents.Select(h.Shell.Contents.Items);
        await h.Shell.CreateClipsCommand.ExecuteAsync(null);

        string name = Path.GetFileNameWithoutExtension(scenes);
        Assert.Equal([$"{name} 001", $"{name} 002", $"{name} 003"], h.Session.Project.Media[0].Clips.Select(c => c.Name));
        Assert.Equal("Create Clips", h.Session.Undo.UndoName);
        Assert.Equal(3, h.Shell.Contents.Items.Count);
    }

    [Fact]
    public async Task Failures_are_listed_in_one_error_message_with_full_paths()
    {
        Harness.RequireFFmpeg();
        using var temp = new TempFolder();
        string missing = temp.File("gone.mp4");
        string folder = Directory.CreateDirectory(temp.File("holiday")).FullName;
        string empty = temp.File("empty.wav");
        await File.WriteAllBytesAsync(empty, [], TestContext.Current.CancellationToken);
        string text = temp.File("notes.txt");
        await File.WriteAllTextAsync(text, "x", TestContext.Current.CancellationToken);
        string broken = temp.File("broken.mp4");
        await File.WriteAllTextAsync(broken, "this is not a video", TestContext.Current.CancellationToken);
        string good = TestMedia.Tone(seconds: 1);

        using var h = new Harness();
        IReadOnlyList<MediaItem> items = await h.Shell.ImportFilesAsync([missing, folder, empty, text, broken, good]);

        Assert.Single(items);
        MessageRequest message = Assert.Single(h.Messages.Shown);
        Assert.Equal("AvaMovieMaker", message.Title);
        Assert.Equal(MessageIcon.Error, message.Icon);
        Assert.Equal(MessageButtons.Ok, message.Buttons);
        Assert.True(message.IsList);
        Assert.Equal(
            [
                $"The file \"{missing}\" could not be found. Verify that the file has not been deleted, renamed, or moved, and then try again.",
                "AvaMovieMaker cannot import folders.",
                $"The file {empty} is an empty file.",
                $"The file {text} is not a supported file type, and it cannot be imported into AvaMovieMaker.",
                $"The file {broken} is not a supported file type, and it cannot be imported into AvaMovieMaker.",
            ],
            message.Text.Split("\n\n"));
        Assert.DoesNotContain("Windows Movie Maker", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_text_uses_the_codec_and_general_messages()
    {
        string text = ShellViewModel.ImportErrorText(
        [
            new("/m/a.avi", ImportError.MissingCodec, "No decoder"),
            new("/m/b.avi", ImportError.Failed, "Disk error."),
            new("/m/c.avi", ImportError.Failed, string.Empty),
        ]);
        Assert.Equal(
            "The file /m/a.avi cannot be imported because the codec required to play the file is not installed on your computer. If you have already tried to download and install the codec, close and restart AvaMovieMaker, and then try to import the file again."
            + "\n\n/m/b.avi could not be imported. Disk error.\n\n/m/c.avi could not be imported.",
            text);
    }

    [Fact]
    public async Task The_last_imported_item_is_selected_and_shown_paused_in_the_monitor()
    {
        Harness.RequireFFmpeg();
        string video = TestMedia.CounterCard(seconds: 1);
        string picture = TestMedia.Picture();
        using var h = new Harness();
        await h.Shell.ImportFilesAsync([video, picture]);

        ContentItemViewModel selected = Assert.Single(h.Shell.Contents.SelectedItems);
        Assert.Equal(picture, selected.Media.Path);
        Assert.Equal(PreviewTarget.Item, h.Shell.Monitor.Target);
        Assert.Equal(Path.GetFileNameWithoutExtension(picture), h.Shell.Monitor.Caption);
        Assert.False(h.Shell.Monitor.IsPlaying);
        Assert.Equal(0, h.Shell.Monitor.Playback.Position.Ticks);
    }

    [Fact]
    public void The_dialog_has_four_filters_and_each_entry_point_selects_its_own()
    {
        Assert.Equal(["All Media Files", "Audio and Music Files", "Picture and Video Files", "All Files"], ImportFilters.All.Select(f => f.Name));
        Assert.Equal(0, ImportFilters.SelectedIndex(MediaFilter.All));
        Assert.Equal(1, ImportFilters.SelectedIndex(MediaFilter.Audio));
        Assert.Equal(2, ImportFilters.SelectedIndex(MediaFilter.Video));
        Assert.Equal(2, ImportFilters.SelectedIndex(MediaFilter.Pictures));
        IReadOnlyList<string> picturesAndVideo = ImportFilters.All[2].Extensions;
        Assert.Contains(".jpg", picturesAndVideo);
        Assert.Contains(".wmv", picturesAndVideo);
        Assert.DoesNotContain(".mp3", picturesAndVideo);
        Assert.True(picturesAndVideo.ToList().IndexOf(".jpg") < picturesAndVideo.ToList().IndexOf(".avi"));
        Assert.Contains(".mp3", ImportFilters.All[0].Extensions);
        Assert.Contains(".png", ImportFilters.All[0].Extensions);
        Assert.Equal([".*"], ImportFilters.All[3].Extensions);
    }

    [Fact]
    public async Task Each_entry_point_opens_in_its_default_folder_and_remembers_its_own()
    {
        Harness.RequireFFmpeg();
        using var h = new Harness();
        await h.Shell.ImportMediaCommand.ExecuteAsync(null);
        await h.Shell.ImportVideosCommand.ExecuteAsync(null);
        await h.Shell.ImportPicturesCommand.ExecuteAsync(null);
        await h.Shell.ImportAudioCommand.ExecuteAsync(null);
        Assert.Equal(
            [(MediaFilter.All, AppPaths.VideosDir), (MediaFilter.Video, AppPaths.VideosDir), (MediaFilter.Pictures, AppPaths.PicturesDir), (MediaFilter.Audio, AppPaths.MusicDir)],
            h.Files.ImportRequests);

        string tone = TestMedia.Tone(seconds: 1);
        h.Files.ImportResult = [tone];
        await h.Shell.ImportAudioCommand.ExecuteAsync(null);
        Assert.Equal(Path.GetDirectoryName(tone), h.Store.Settings.ImportFolderAudio);
        Assert.Equal(Path.GetDirectoryName(tone), h.Shell.ImportStartFolder(MediaFilter.Audio));
        Assert.Equal(AppPaths.VideosDir, h.Shell.ImportStartFolder(MediaFilter.All));
        Assert.Equal(AppPaths.PicturesDir, h.Shell.ImportStartFolder(MediaFilter.Pictures));

        h.Store.Settings.ImportFolderVideo = Path.Combine(Path.GetTempPath(), "AvaMovieMaker-no-such-folder-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(AppPaths.VideosDir, h.Shell.ImportStartFolder(MediaFilter.Video));
    }

    [Fact]
    public void Drops_from_the_file_manager_need_a_media_file_or_a_single_project()
    {
        using var h = new Harness();
        Assert.False(h.Shell.CanDropFiles([]));
        Assert.False(h.Shell.CanDropFiles(["/x/notes.txt"]));
        Assert.True(h.Shell.CanDropFiles(["/x/notes.txt", "/x/clip.MP4"]));
        Assert.True(h.Shell.CanDropFiles(["/x/My Movie.MSWMM"]));
        Assert.True(h.Shell.CanDropFiles(["/x/movie.ammproj"]));
        Assert.False(h.Shell.CanDropFiles(["/x/a.ammproj", "/x/b.ammproj"]));
    }

    [Fact]
    public async Task Dropping_a_single_project_opens_it()
    {
        using var temp = new TempFolder();
        string path = temp.File("dropped" + ProjectSerializer.Extension);
        ProjectSerializer.Save(new Project { Properties = new ProjectProperties { Title = "Dropped" } }, path);
        using var h = new Harness();
        await h.Shell.DropFilesAsync([path]);
        Assert.Equal(path, h.Session.Project.FilePath);
        Assert.Equal("Dropped", h.Session.Project.Properties.Title);
        Assert.Empty(h.Session.Project.Media);
    }

    [Fact]
    public async Task Dropped_media_is_imported_and_other_files_are_reported()
    {
        Harness.RequireFFmpeg();
        using var temp = new TempFolder();
        string text = temp.File("notes.txt");
        await File.WriteAllTextAsync(text, "x", TestContext.Current.CancellationToken);
        string tone = TestMedia.Tone(seconds: 1);
        using var h = new Harness();
        await h.Shell.DropFilesAsync([text, tone]);
        Assert.Equal(MediaKind.Audio, Assert.Single(h.Session.Project.Media).Kind);
        Assert.Contains(text, Assert.Single(h.Messages.Shown).Text, StringComparison.Ordinal);

        await h.Shell.DropFilesAsync([text]);
        Assert.Single(h.Session.Project.Media);
        Assert.Single(h.Messages.Shown);
    }
}
