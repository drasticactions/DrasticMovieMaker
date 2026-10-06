using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Timeline.Editing;

namespace AvaMovieMaker.ViewModels.Tests;

public class ThumbnailSizeTests
{
    [Fact]
    public void Storyboard_thumbnails_follow_the_screen_scale()
    {
        using var h = new Harness();
        TimelineEditor editor = h.Shell.Session.Editor;
        editor.InsertVideoClips(0, [editor.NewTitleClip(new TitleContent { Lines = ["Hello"] })], UndoNames.AddTitle);

        Assert.Equal(153, Assert.Single(h.Shell.Storyboard.Cells).Thumbnail!.Width);

        h.Shell.Storyboard.SetThumbnailSize(254, 191);
        Assert.Equal((254, 191), (h.Shell.Storyboard.Cells[0].Thumbnail!.Width, h.Shell.Storyboard.Cells[0].Thumbnail!.Height));

        editor.InsertVideoClips(1, [editor.NewTitleClip(new TitleContent { Lines = ["Bye"] })], UndoNames.AddTitle);
        Assert.Equal(254, h.Shell.Storyboard.Cells[1].Thumbnail!.Width);
    }
}
