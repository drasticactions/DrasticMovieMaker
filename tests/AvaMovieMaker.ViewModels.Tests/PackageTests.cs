using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Timeline.Import;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.ViewModels.Tests;

public class PackageTests
{
    [Fact]
    public async Task Exported_packages_open_as_new_projects_with_their_media_where_the_user_chose()
    {
        using var temp = new TempFolder();
        string package = temp.File("Trip.dmmpkg");
        using (var first = new Harness())
        {
            MediaItem pic = MediaImporter.FromFile(TestMedia.Picture(64, 48, hue: 10), false, 1);
            first.Session.Editor.ImportMedia([pic]);
            first.Session.Editor.InsertVideoClips(0, [first.Session.Editor.NewVideoClip(pic)]);
            first.Files.PackageSave = (_, _) => package;
            await first.Shell.ExportPackageCommand.ExecuteAsync(null);
            Assert.True(File.Exists(package));
        }

        using var second = new Harness();
        second.Messages.Answer = MessageResult.No;
        second.Files.PackageFolder = temp.File("media");
        await second.Shell.OpenRecentCommand.ExecuteAsync(package);

        MediaItem opened = Assert.Single(second.Session.Project.Media);
        Assert.False(opened.Missing);
        Assert.StartsWith(temp.File("media"), opened.Path, StringComparison.Ordinal);
        Assert.Single(second.Session.Project.VideoTrack);
        Assert.Null(second.Session.Project.FilePath);
        Assert.True(second.Session.Undo.IsDirty);
    }

    [Fact]
    public async Task Missing_media_are_named_when_exporting()
    {
        using var temp = new TempFolder();
        using var h = new Harness();
        MediaItem pic = MediaImporter.FromFile(TestMedia.Picture(64, 48, hue: 10), false, 1);
        h.Session.Editor.ImportMedia([pic with { Path = temp.File("gone.png") }]);
        h.Files.PackageSave = (_, _) => temp.File("out.dmmpkg");
        await h.Shell.ExportPackageCommand.ExecuteAsync(null);

        MessageRequest shown = Assert.Single(h.Messages.Shown);
        Assert.Contains(temp.File("gone.png"), shown.Text, StringComparison.Ordinal);
        Assert.True(File.Exists(temp.File("out.dmmpkg")));
    }
}
