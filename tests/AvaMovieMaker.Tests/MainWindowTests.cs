using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using AvaMovieMaker.Views;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Titles;

namespace AvaMovieMaker.Tests;

public sealed class MainWindowTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static Button ToolButton(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    [AvaloniaFact]
    public void Opens_titled_with_the_app_name()
    {
        LogCollector.Instance.Take();
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Assert.True(w.IsVisible);
            Assert.Equal("Drastic Movie Maker", w.Title);
            Assert.NotNull(w.View);
            Assert.Same(Shell, w.DataContext);
            Assert.True(w.View!.Bounds.Width > 0);
            Assert.Empty(LogCollector.Instance.Take());
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Toolbar_buttons_by_automation_name()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            foreach (string name in new[] { "Import Media", "Undo", "Redo", "AutoMovie", "Publish Movie" })
            {
                Assert.True(ToolButton(w, name).IsEffectivelyVisible, name);
            }

            Assert.True(ToolButton(w, "Import Media").IsEffectivelyEnabled);
            Assert.False(ToolButton(w, "Undo").IsEffectivelyEnabled);
            Assert.False(ToolButton(w, "Redo").IsEffectivelyEnabled);
            Assert.False(ToolButton(w, "AutoMovie").IsEffectivelyEnabled);
            Assert.False(ToolButton(w, "Publish Movie").IsEffectivelyEnabled);

            Shell.Session.Editor.AddTitle(new TitleContent { Lines = ["Hello"] }, null, MediaTime.Zero);
            TestApp.Pump();
            Assert.True(ToolButton(w, "Undo").IsEffectivelyEnabled);
            Assert.True(ToolButton(w, "Publish Movie").IsEffectivelyEnabled);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Switching_to_timeline_shows_the_timeline_view()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            TimelineView timeline = w.GetVisualDescendants().OfType<TimelineView>().Single();
            StoryboardView storyboard = w.GetVisualDescendants().OfType<StoryboardView>().Single();
            Assert.False(timeline.IsVisible);
            Assert.True(storyboard.IsVisible);

            Shell.IsTimeline = true;
            TestApp.Pump();
            Assert.True(timeline.IsEffectivelyVisible);
            Assert.False(storyboard.IsVisible);
            Assert.True(timeline.Bounds.Height > 0);

            Shell.IsTimeline = false;
            TestApp.Pump();
            Assert.False(timeline.IsVisible);
            Assert.True(storyboard.IsEffectivelyVisible);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Title_animation_headers_are_disabled_rows()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Shell.TitlesCommand.Execute(null);
            Shell.TitleEditor.ChooseCommand.Execute(TitlePlacement.AtBeginning);
            Shell.TitleEditor.ShowAnimationsCommand.Execute(null);
            TestApp.Pump();

            TitleEditorView editor = w.GetVisualDescendants().OfType<TitleEditorView>().Single();
            Assert.True(editor.IsEffectivelyVisible);
            ListBox list = editor.FindControl<ListBox>("AnimationList")!;
            Assert.True(list.IsEffectivelyVisible);
            list.ScrollIntoView(list.ItemCount - 1);
            TestApp.Pump();
            list.ScrollIntoView(0);
            TestApp.Pump();

            var containers = list.GetRealizedContainers().Select(c => (Control)c).ToList();
            Assert.NotEmpty(containers);
            int headers = 0;
            foreach (Control c in containers)
            {
                object? item = list.ItemFromContainer(c);
                if (item is AnimationGroupHeader)
                {
                    headers++;
                    Assert.False(c.IsEnabled);
                }
                else
                {
                    Assert.IsType<AnimationOption>(item);
                    Assert.True(c.IsEnabled);
                }
            }

            Assert.True(headers > 0);
            Assert.IsType<AnimationGroupHeader>(list.ItemFromContainer(containers[0]));
        }
        finally
        {
            Shell.TitleEditor.CancelCommand.Execute(null);
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Renders_a_non_blank_frame()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            using WriteableBitmap? frame = w.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.True(frame!.PixelSize.Width >= 640 && frame.PixelSize.Height >= 480, frame.PixelSize.ToString());

            using ILockedFramebuffer fb = frame.Lock();
            var colors = new HashSet<uint>();
            unsafe
            {
                for (int y = 0; y < fb.Size.Height; y += 4)
                {
                    uint* row = (uint*)(fb.Address + y * fb.RowBytes);
                    for (int x = 0; x < fb.Size.Width; x += 4)
                    {
                        colors.Add(row[x]);
                    }
                }
            }

            Assert.True(colors.Count > 20, $"only {colors.Count} colors");
        }
        finally
        {
            TestApp.Close(w);
        }
    }
}
