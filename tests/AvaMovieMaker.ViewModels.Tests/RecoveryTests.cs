using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Serialization;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.ViewModels.Tests;

[Collection("recovery")]
public class RecoveryTests
{
    private static void Clear()
    {
        foreach (string f in new[] { ShellViewModel.RecoveryPath, ShellViewModel.RecoveryOriginPath })
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
    }

    private static void Crash(Harness h)
    {
        File.Copy(ShellViewModel.RecoveryPath, ShellViewModel.RecoveryPath + ".bak", true);
        File.Copy(ShellViewModel.RecoveryOriginPath, ShellViewModel.RecoveryOriginPath + ".bak", true);
        h.Dispose();
        File.Move(ShellViewModel.RecoveryPath + ".bak", ShellViewModel.RecoveryPath, true);
        File.Move(ShellViewModel.RecoveryOriginPath + ".bak", ShellViewModel.RecoveryOriginPath, true);
    }

    [Fact]
    public void Deleting_a_file_in_a_missing_folder_does_nothing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"amm-missing-{Guid.NewGuid():N}", "AutoRecover.ammproj");
        new AvaMovieMaker.IO.LocalFileStore().Delete(missing);
        Assert.False(File.Exists(missing));
    }

    [Fact]
    public async Task Unsaved_project_is_recovered_after_a_crash()
    {
        Clear();
        var first = new Harness();
        first.Session.Editor.AddTitle(new TitleContent { Lines = ["kept"] }, null, MediaTime.Zero);
        first.Shell.WriteRecovery();
        Assert.True(File.Exists(ShellViewModel.RecoveryPath));
        Assert.StartsWith(IO.AppPaths.StateDir, ShellViewModel.RecoveryPath, StringComparison.Ordinal);
        Crash(first);

        using var second = new Harness();
        second.Messages.Answer = MessageResult.Yes;
        await second.Shell.StartAsync((string?)null);
        MessageRequest prompt = Assert.Single(second.Messages.Shown);
        Assert.Equal("AvaMovieMaker found a project file that was saved automatically. Would you like to recover this file?", prompt.Text);
        Assert.Equal(MessageButtons.YesNo, prompt.Buttons);
        Assert.Single(second.Session.Project.VideoTrack);
        Assert.Null(second.Session.Project.FilePath);
        Assert.True(second.Session.Undo.IsDirty);
        Assert.False(File.Exists(ShellViewModel.RecoveryPath));
        Assert.False(File.Exists(ShellViewModel.RecoveryOriginPath));
    }

    [Fact]
    public async Task Saved_project_is_recovered_from_the_state_folder_not_beside_the_file()
    {
        Clear();
        using var temp = new TempFolder();
        string path = temp.File("mine.ammproj");
        var first = new Harness();
        first.AddTitle("one");
        ProjectSerializer.Save(first.Session.Project, path);
        await first.Shell.OpenFileAsync(path);
        first.AddTitle("two");
        first.Shell.WriteRecovery();
        Assert.Equal(["mine.ammproj"], Directory.GetFiles(temp.Path).Select(Path.GetFileName));
        Crash(first);

        using var second = new Harness();
        second.Messages.Answer = MessageResult.Yes;
        await second.Shell.StartAsync((string?)null);
        Assert.Equal(2, second.Session.Project.VideoTrack.Count);
        Assert.Equal(Path.GetFullPath(path), second.Session.Project.FilePath);
        Assert.True(second.Session.Undo.IsDirty);
        Assert.False(File.Exists(ShellViewModel.RecoveryPath));
    }

    [Fact]
    public async Task No_discards_the_copy_and_starts_empty()
    {
        Clear();
        var first = new Harness();
        first.AddTitle("dropped");
        first.Shell.WriteRecovery();
        Crash(first);

        using var second = new Harness();
        second.Messages.Answer = MessageResult.No;
        await second.Shell.StartAsync((string?)null);
        Assert.Single(second.Messages.Shown);
        Assert.True(second.Session.Project.IsEmpty);
        Assert.False(File.Exists(ShellViewModel.RecoveryPath));
    }

    [Fact]
    public void Copy_is_written_only_when_the_project_changed()
    {
        Clear();
        using var h = new Harness();
        h.Shell.WriteRecovery();
        Assert.False(File.Exists(ShellViewModel.RecoveryPath));

        h.AddTitle("x");
        h.Shell.WriteRecovery();
        DateTime written = File.GetLastWriteTimeUtc(ShellViewModel.RecoveryPath);
        File.SetLastWriteTimeUtc(ShellViewModel.RecoveryPath, written.AddMinutes(-5));

        h.Shell.WriteRecovery();
        Assert.Equal(written.AddMinutes(-5), File.GetLastWriteTimeUtc(ShellViewModel.RecoveryPath));

        h.AddTitle("y");
        h.Shell.WriteRecovery();
        Assert.NotEqual(written.AddMinutes(-5), File.GetLastWriteTimeUtc(ShellViewModel.RecoveryPath));
    }

    [Fact]
    public async Task Clean_exit_leaves_nothing_to_recover()
    {
        Clear();
        using (var h = new Harness())
        {
            h.Session.Editor.AddTitle(new TitleContent { Lines = ["gone"] }, null, MediaTime.Zero);
            h.Shell.WriteRecovery();
        }

        Assert.False(File.Exists(ShellViewModel.RecoveryPath));
        using var next = new Harness();
        await next.Shell.StartAsync((string?)null);
        Assert.DoesNotContain(next.Messages.Shown, m => m.Text.Contains("saved automatically", StringComparison.Ordinal));
    }
}

[CollectionDefinition("recovery", DisableParallelization = true)]
public sealed class RecoveryCollection;
