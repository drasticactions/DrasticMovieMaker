using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Titles;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class MonitorTests
{
    [Fact]
    public void Caption_names_the_board_in_view()
    {
        using var h = new Harness();
        Guid id = h.AddTitle("Opening");
        h.Shell.Monitor.ShowProject(seekStart: true);
        Assert.Equal(PreviewTarget.Project, h.Shell.Monitor.Target);

        Assert.Equal(string.Empty, h.Shell.Monitor.Caption);
        h.Session.Select([id]);
        Assert.Equal("Storyboard: Opening", h.Shell.Monitor.Caption);

        h.Shell.IsTimeline = true;
        Assert.Equal("Timeline: Opening", h.Shell.Monitor.Caption);

        h.Shell.IsTimeline = false;
        Assert.Equal("Storyboard: Opening", h.Shell.Monitor.Caption);
    }

    [Fact]
    public void Caption_is_empty_for_an_empty_project()
    {
        using var h = new Harness();
        h.Shell.Monitor.ShowProject(seekStart: true);
        Assert.Equal(string.Empty, h.Shell.Monitor.Caption);
        Assert.Equal("Software rendering", h.Shell.Monitor.RenderingDescription);
    }
}

public sealed class TitleEditorTests
{
    private static readonly string[] PlacementTexts =
        ["Title at the beginning", "Title before the selected clip", "Title on the selected clip", "Credits at the end"];

    [Fact]
    public void Start_offers_four_placements_with_clip_ones_disabled_without_a_selection()
    {
        using var h = new Harness();
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.Start();
        Assert.Equal(TitlePage.Placement, t.Page);
        Assert.True(t.IsPlacementPage);
        Assert.Equal("Where do you want to add a title?", t.Heading);
        Assert.Equal(PlacementTexts, t.Placements.Select(p => p.Text));
        Assert.Equal([true, false, false, true], t.Placements.Select(p => p.IsEnabled));
        Assert.Equal("Add Title", t.AddText);
        Assert.False(t.IsEditing);
    }

    [Fact]
    public void Start_with_a_selected_clip_enables_every_placement()
    {
        using var h = new Harness();
        Guid id = h.AddTitle("Clip");
        h.Session.Select([id]);
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.Start();
        Assert.All(t.Placements, p => Assert.True(p.IsEnabled, p.Text));
    }

    [Fact]
    public void Headings_follow_the_page()
    {
        using var h = new Harness();
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.Start();
        t.ChooseCommand.Execute(TitlePlacement.AtBeginning);
        Assert.Equal(TitlePage.Text, t.Page);
        Assert.True(t.IsTextPage);
        Assert.False(t.IsCredits);
        Assert.Equal("Enter text for title", t.Heading);

        t.ShowAnimationsCommand.Execute(null);
        Assert.True(t.IsAnimationPage);
        Assert.Equal("Choose the title animation", t.Heading);

        t.ShowFontsCommand.Execute(null);
        Assert.True(t.IsFontPage);
        Assert.Equal("Select title font and color", t.Heading);

        t.ShowTextCommand.Execute(null);
        Assert.Equal("Enter text for title", t.Heading);

        t.Start();
        t.ChooseCommand.Execute(TitlePlacement.CreditsAtEnd);
        Assert.True(t.IsCredits);
        Assert.Equal("Enter text for title", t.Heading);
        Assert.Equal(4, t.Credits.Count);
    }

    [Fact]
    public void Title_on_a_clip_switches_to_the_timeline()
    {
        using var h = new Harness();
        Guid id = h.AddTitle("Clip");
        h.Session.Select([id]);
        h.Shell.TitlesCommand.Execute(null);
        Assert.Equal(UpperPane.Titles, h.Shell.Pane);
        h.Shell.TitleEditor.ChooseCommand.Execute(TitlePlacement.OnClip);
        Assert.False(h.Shell.TitleEditor.IsFullFrame);

        Assert.False(h.Shell.IsTimeline);
        Assert.False(h.Shell.TitleEditor.AddCommand.CanExecute(null));
        h.Shell.TitleEditor.Line1 = "Overlay";
        h.Shell.TitleEditor.AddCommand.Execute(null);
        Assert.True(h.Shell.IsTimeline);
        Assert.Contains(h.Messages.Shown, m => m.Text.StartsWith("Overlay titles can only be added in the timeline view", StringComparison.Ordinal));
        Assert.Single(h.Session.Project.TitleOverlayTrack);
    }

    [Theory]
    [InlineData(TitlePlacement.AtBeginning)]
    [InlineData(TitlePlacement.CreditsAtEnd)]
    public void Animation_rows_put_a_header_before_each_group(TitlePlacement placement)
    {
        using var h = new Harness();
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.Start();
        t.ChooseCommand.Execute(placement);
        IReadOnlyList<object> rows = t.AnimationRows;
        Assert.NotEmpty(rows);
        Assert.IsType<AnimationGroupHeader>(rows[0]);
        Assert.Equal(t.Animations.Count + t.Animations.Select(a => a.Group).Distinct().Count(), rows.Count);

        string? group = null;
        var seen = new HashSet<string>();
        foreach (object row in rows)
        {
            if (row is AnimationGroupHeader header)
            {
                Assert.True(seen.Add(header.Name), $"group {header.Name} appears twice");
                group = header.Name;
            }
            else
            {
                AnimationOption a = Assert.IsType<AnimationOption>(row);
                Assert.Equal(group, a.GroupName);
            }
        }

        Assert.NotNull(t.Animation);
        Assert.Contains(t.Animation, t.Animations);
        if (placement == TitlePlacement.CreditsAtEnd)
        {
            Assert.Equal(["Credits"], seen);
        }
        else
        {
            Assert.DoesNotContain("Credits", seen);
        }
    }

    [Fact]
    public void ToContent_round_trips_through_edit()
    {
        using var h = new Harness();
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.Start();
        t.ChooseCommand.Execute(TitlePlacement.AtBeginning);
        AnimationOption two = t.Animations.First(a => a.Group == Effects.Catalog.TitleGroup.TwoLines);
        t.Animation = two;
        t.Line1 = "Main";
        t.Line2 = "Sub";
        t.Bold = true;
        t.Italic = true;
        t.IncreaseSizeCommand.Execute(null);
        t.IncreaseSizeCommand.Execute(null);
        t.TextColor = 0xFF102030;
        t.BackgroundColor = 0xFF405060;
        t.Transparency = 25;
        t.AlignLeft = true;

        TitleContent c = t.ToContent();
        Assert.Equal(TitlePlacement.AtBeginning, c.Placement);
        Assert.Equal(two.Id, c.AnimationId);
        Assert.Equal(["Main", "Sub"], c.Lines);
        Assert.Empty(c.Credits);
        Assert.Equal(TitleFonts.DefaultFamily, c.Font.Family);
        Assert.True(c.Font.Bold);
        Assert.True(c.Font.Italic);
        Assert.False(c.Font.Underline);
        Assert.Equal(2, c.Font.SizeStep);
        Assert.Equal(0xFF102030u, c.TextColor);
        Assert.Equal(0xFF405060u, c.BackgroundColor);
        Assert.Equal(25, c.Transparency);
        Assert.Equal(TitleAlignment.Left, c.Alignment);

        t.AddCommand.Execute(null);
        Guid id = Assert.Single(h.Session.Project.VideoTrack).Id;
        t.Edit(id);
        Assert.True(t.IsEditing);
        Assert.Equal("Done", t.AddText);
        Assert.Equal(TitlePage.Text, t.Page);
        TitleContent again = t.ToContent();
        Assert.Equal(c.Lines, again.Lines);
        Assert.Equal(c with { Lines = again.Lines, Credits = again.Credits }, again);
    }

    [Fact]
    public void Credits_keep_only_filled_rows()
    {
        using var h = new Harness();
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.Start();
        t.ChooseCommand.Execute(TitlePlacement.CreditsAtEnd);
        t.Credits[0].Heading = "Director";
        t.Credits[0].Names = "Someone";
        t.Credits[2].Names = "Crew";
        TitleContent c = t.ToContent();
        Assert.Equal(TitlePlacement.CreditsAtEnd, c.Placement);
        Assert.Equal([new CreditRow("Director", "Someone"), new CreditRow(string.Empty, "Crew")], c.Credits);
    }

    [Fact]
    public void Add_inserts_an_undoable_title_clip_and_closes()
    {
        using var h = new Harness();
        h.Shell.TitlesCommand.Execute(null);
        TitleEditorViewModel t = h.Shell.TitleEditor;
        t.ChooseCommand.Execute(TitlePlacement.AtBeginning);
        t.Line1 = "Welcome";
        t.AddCommand.Execute(null);

        VideoClip clip = Assert.Single(h.Session.Project.VideoTrack);
        Assert.Equal(VideoClipKind.Title, clip.Kind);
        Assert.Equal("Welcome", clip.Title!.Summary);
        Assert.Equal(UndoNames.AddTitle, h.Session.Undo.UndoName);
        Assert.Equal(UpperPane.Contents, h.Shell.Pane);
        Assert.True(h.Shell.CanPublish);

        h.Shell.UndoCommand.Execute(null);
        Assert.Empty(h.Session.Project.VideoTrack);
    }

    [Fact]
    public void Cancel_adds_nothing()
    {
        using var h = new Harness();
        h.Shell.TitlesCommand.Execute(null);
        h.Shell.TitleEditor.ChooseCommand.Execute(TitlePlacement.AtBeginning);
        h.Shell.TitleEditor.Line1 = "Discarded";
        h.Shell.TitleEditor.CancelCommand.Execute(null);
        Assert.Empty(h.Session.Project.VideoTrack);
        Assert.Equal(UpperPane.Contents, h.Shell.Pane);
        Assert.False(h.Shell.HasUndo);
    }
}
