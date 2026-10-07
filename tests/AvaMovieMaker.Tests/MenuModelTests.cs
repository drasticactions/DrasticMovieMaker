using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using AvaMovieMaker.Menus;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Views;
using AvaMovieMaker.ViewModels;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Tests;

public sealed class MenuModelTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static IEnumerable<MenuItemEntry> All(IEnumerable<MenuEntry> entries) =>
        entries.OfType<MenuItemEntry>().SelectMany(e => All(e.Items).Prepend(e));

    [AvaloniaFact]
    public void Every_menu_shortcut_runs_the_command_of_its_item()
    {
        MainWindow w = TestApp.OpenMainWindow();
        TestApp.Close(w);
        string[] wrong =
        [
            .. All(ShellMenus.Build(Shell))
                .Where(e => e is { Shortcut: not null, Command: not null })
                .Where(e => e.Shortcut!.Value != Shortcut.Parse("Ctrl+T"))
                .Where(e => !ReferenceEquals(e.Command, Shell.Unavailable))
                .Where(e => !ReferenceEquals(ShellView.CommandFor(Shell, e.Shortcut!.Value.Key, e.Shortcut.Value.Modifiers, typing: false), e.Command))
                .Select(e => $"{e.Header} {e.Shortcut}"),
        ];
        Assert.Empty(wrong);
        Assert.Same(Shell.ToggleStoryboardTimelineCommand, ShellView.CommandFor(Shell, Key.T, KeyModifiers.Control, typing: false));
    }

    [AvaloniaFact]
    public void The_window_menu_bar_shows_the_model_with_ctrl_shortcuts()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Menu bar = w.View!.FindControl<Menu>("MainMenu")!;
            MenuItem[] items = [.. bar.GetLogicalDescendants().OfType<MenuItem>()];
            MenuItem open = items.Single(m => (m.Header as string) == "_Open Project...");
            Assert.Equal(new KeyGesture(Key.O, KeyModifiers.Control), open.InputGesture);
            Assert.Same(Shell.OpenProjectCommand, open.Command);
            MenuItem nudge = items.Single(m => (m.Header as string) == "Nudge _Left");
            Assert.Equal(new KeyGesture(Key.B, KeyModifiers.Control | KeyModifiers.Shift), nudge.InputGesture);

            MenuItem undo = items.Single(m => ReferenceEquals(m.Command, Shell.UndoCommand));
            Assert.Equal(Shell.UndoText, undo.Header);
            MenuItem timeline = items.Single(m => ReferenceEquals(m.Command, Shell.ShowTimelineViewCommand));
            bool was = Shell.IsTimeline;
            Shell.ToggleStoryboardTimelineCommand.Execute(null);
            TestApp.Pump();
            Assert.Equal(!was, timeline.IsChecked);
            Shell.ToggleStoryboardTimelineCommand.Execute(null);
            TestApp.Pump();
            Assert.Equal(was, timeline.IsChecked);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Aspect_ratio_and_fit_submenus_track_the_project_in_both_menu_bars()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Menu bar = w.View!.FindControl<Menu>("MainMenu")!;
            MenuItem[] items = [.. bar.GetLogicalDescendants().OfType<MenuItem>()];
            MenuItem[] aspects = [.. items.Where(m => ReferenceEquals(m.Command, Shell.SetAspectRatioCommand))];
            Assert.Equal(["4:3 (Standard)", "16:9 (Widescreen)", "9:16 (Vertical)", "1:1 (Square)", "4:5 (Portrait)"], aspects.Select(m => m.Header as string));
            Assert.All(aspects, m => Assert.Equal(MenuItemToggleType.Radio, m.ToggleType));
            MenuItem aspectMenu = items.Single(m => (m.Header as string) == Strings.AspectRatioMenu);
            MenuItem monitorSize = items.Single(m => (m.Header as string) == Strings.PreviewMonitorSize);
            var viewItems = ((MenuItem)monitorSize.Parent!).Items;
            Assert.Equal(viewItems.IndexOf(monitorSize) + 1, viewItems.IndexOf(aspectMenu));

            AspectRatio was = Shell.Aspect;
            Shell.SetAspectRatioCommand.Execute(AspectRatio.Square1x1);
            TestApp.Pump();
            Assert.Equal([false, false, false, true, false], aspects.Select(m => m.IsChecked));
            Shell.UndoCommand.Execute(null);
            TestApp.Pump();
            Assert.Equal(was, Shell.Aspect);
            Assert.True(aspects[AspectRatios.All.ToList().IndexOf(was)].IsChecked);

            MenuItem fit = items.Single(m => (m.Header as string) == Strings.FitMenu);
            Assert.Equal(3, fit.Items.Count);
            Assert.Equal(Shell.CanFit, fit.IsEnabled);
            Assert.Contains(Strings.PromptFitBlur, fit.Items.OfType<MenuItem>().Select(InWindowMenuHost.GetPrompt));

            NativeMenuItem[] native = [.. NativeItems(NativeMenuHost.Build(ShellMenus.Build(Shell)))];
            Assert.Equal(5, native.Count(i => ReferenceEquals(i.Command, Shell.SetAspectRatioCommand)));
            Assert.Equal(3, native.Count(i => ReferenceEquals(i.Command, Shell.SetFitCommand)));
            Assert.Equal(Shell.CanFit, native.Single(i => i.Header == NativeMenuHost.WithoutAccessKey(Strings.FitMenu)).IsEnabled);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void Native_menu_items_are_enabled_when_their_command_can_run()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            NativeMenuItem[] items = [.. NativeItems(NativeMenuHost.Build(ShellMenus.Build(Shell)))];
            string[] wrong =
            [
                .. items.Where(i => i.Command is not null && i.Command.CanExecute(i.CommandParameter) != i.IsEnabled)
                    .Select(i => $"{i.Header} (can run: {i.Command!.CanExecute(i.CommandParameter)}, enabled: {i.IsEnabled})"),
            ];
            Assert.True(wrong.Length == 0, string.Join("; ", wrong));
            Assert.True(items.First(i => ReferenceEquals(i.Command, Shell.SetAspectRatioCommand)).IsEnabled);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [AvaloniaFact]
    public void The_status_bar_shows_the_prompt_of_the_highlighted_item()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            Menu bar = w.View!.FindControl<Menu>("MainMenu")!;
            MenuItem[] items = [.. bar.GetLogicalDescendants().OfType<MenuItem>()];
            MenuItem Of(System.Windows.Input.ICommand command)
            {
                MenuItem item = items.Single(m => ReferenceEquals(m.Command, command));
                foreach (MenuItem parent in item.GetLogicalAncestors().OfType<MenuItem>().Reverse())
                {
                    parent.IsSubMenuOpen = true;
                    TestApp.Pump();
                }

                return item;
            }

            Of(Shell.UndoCommand).IsSelected = true;
            Assert.Equal(Strings.PromptUndo, Shell.StatusText);
            Of(Shell.VideoFadeInCommand).IsSelected = true;
            Assert.Equal(Strings.PromptVideoFadeIn, Shell.StatusText);
            Of(Shell.AudioFadeInCommand).IsSelected = true;
            Assert.Equal(Strings.PromptAudioFadeIn, Shell.StatusText);
            Shell.MenuPrompt = null;
            Assert.Equal(Strings.Ready, Shell.StatusText);
        }
        finally
        {
            TestApp.Close(w);
        }
    }

    [Fact]
    public void The_command_modifier_maps_both_ways()
    {
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, CommandKeys.Swap(KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Control));

        Assert.Equal(KeyModifiers.Meta | KeyModifiers.Shift, CommandKeys.Swap(KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Meta));
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Alt, CommandKeys.Swap(KeyModifiers.Meta | KeyModifiers.Alt, KeyModifiers.Meta));
        Assert.Equal(KeyModifiers.Meta, CommandKeys.Swap(KeyModifiers.Control, KeyModifiers.Meta));
        Assert.Equal(KeyModifiers.Alt, CommandKeys.Swap(KeyModifiers.Alt, KeyModifiers.Meta));
    }

    [AvaloniaFact]
    public void The_mac_menu_bar_moves_app_items_and_shows_cmd_shortcuts()
    {
        MainWindow w = TestApp.OpenMainWindow();
        KeyModifiers was = CommandKeys.Command;
        try
        {
            CommandKeys.Command = KeyModifiers.Meta;
            NativeMenu bar = NativeMenuHost.Build(ShellMenus.Build(Shell));
            Assert.Equal(["File", "Edit", "View", "Tools", "Clip", "Play", "Help"], bar.Items.OfType<NativeMenuItem>().Select(m => m.Header));
            NativeMenuItem[] items = [.. NativeItems(bar)];

            Assert.DoesNotContain(items, i => ReferenceEquals(i.Command, Shell.AboutCommand) || ReferenceEquals(i.Command, Shell.OptionsCommand) || ReferenceEquals(i.Command, Shell.ExitCommand));
            Assert.All(bar.Items.OfType<NativeMenuItem>(), m => Assert.IsNotType<NativeMenuItemSeparator>(m.Menu!.Items[^1]));

            NativeMenuItem open = items.Single(i => ReferenceEquals(i.Command, Shell.OpenProjectCommand));
            Assert.Equal("Open Project...", open.Header);
            Assert.Equal(new KeyGesture(Key.O, KeyModifiers.Meta), open.Gesture);
            Assert.Equal(new KeyGesture(Key.B, KeyModifiers.Meta | KeyModifiers.Shift), items.Single(i => i.Header == "Nudge Left").Gesture);

            Assert.Null(items.Single(i => ReferenceEquals(i.Command, Shell.ShowTimelineViewCommand)).Gesture);
            Assert.Null(items.Single(i => ReferenceEquals(i.Command, Shell.Monitor.RewindCommand)).Gesture);

            Assert.Equal(NativeMenuHost.WithoutAccessKey(Shell.UndoText), items.Single(i => ReferenceEquals(i.Command, Shell.UndoCommand)).Header);

            NativeMenu file = bar.Items.OfType<NativeMenuItem>().First().Menu!;
            Assert.Contains(file.Items.OfType<NativeMenuItem>(), i => i.Header == "(Empty)");
            Shell.RecentProjects.Insert(0, Path.Combine(Path.GetTempPath(), "Holiday.ammproj"));
            NativeMenuItem recent = file.Items.OfType<NativeMenuItem>().Single(i => ReferenceEquals(i.Command, Shell.OpenRecentCommand));
            Assert.StartsWith("1 ", recent.Header, StringComparison.Ordinal);
            Assert.DoesNotContain(file.Items.OfType<NativeMenuItem>(), i => i.Header == "(Empty)");
        }
        finally
        {
            CommandKeys.Command = was;
            Shell.RecentProjects.Clear();
            TestApp.Close(w);
        }
    }

    [Fact]
    public void Mac_headers_drop_access_keys()
    {
        Assert.Equal("Save Project As...", NativeMenuHost.WithoutAccessKey("Save Project _As..."));
        Assert.Equal("a_b", NativeMenuHost.WithoutAccessKey("a__b"));
    }

    private static IEnumerable<NativeMenuItem> NativeItems(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(i => (i.Menu is null ? [] : NativeItems(i.Menu)).Prepend(i));
}
