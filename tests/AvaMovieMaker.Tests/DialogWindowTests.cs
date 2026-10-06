using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using AvaWpf;
using AvaMovieMaker.Dialogs;
using AvaMovieMaker.Views;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media;
using AvaMovieMaker.Settings;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Dialogs;
using AvaMovieMaker.ViewModels.Publish;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Tests;

public sealed class DialogWindowTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static (Control Host, IWindowHandle Handle) Show(MainWindow owner, Control dialog)
    {
        IWindowHandle handle = WindowHost.Show(dialog, owner);
        TestApp.Pump();
        return (WindowHost.HostOf(dialog)!, handle);
    }

    private static string? TitleOf(Control host) => (host as Window)?.Title;

    private static void ShowAndCheck(MainWindow owner, Control dialog)
    {
        LogCollector.Instance.Take();
        (Control host, IWindowHandle handle) = Show(owner, dialog);
        try
        {
            Assert.True(host.IsEffectivelyVisible);
            Assert.True(host.Bounds.Width > 0 && host.Bounds.Height > 0, $"{dialog.GetType().Name} has no size");
            Assert.False(string.IsNullOrEmpty(TitleOf(host)), $"{dialog.GetType().Name} has no title");
            Assert.True(dialog.Bounds.Width > 0 && dialog.Bounds.Height > 0, $"{dialog.GetType().Name} content has no size");
            Assert.Empty(LogCollector.Instance.Take());
        }
        finally
        {
            handle.Close();
            TestApp.Pump();
        }
    }

    private static void WithMainWindow(Action<MainWindow> test)
    {
        WindowHost.UseInPageWindows = false;
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            test(w);
        }
        finally
        {
            TestApp.Close(w);
            WindowHost.UseInPageWindows = null;
        }
    }

    [AvaloniaFact]
    public void Import_error_list_is_a_read_only_scrolling_box() => WithMainWindow(w =>
    {
        string text = string.Join("\n\n", Enumerable.Range(1, 12).Select(i => $"The file /media/clip{i}.avi is an empty file."));
        MessageBoxWindow box = MessageBoxWindow.Create("AvaMovieMaker", new MessageRequest(text, MessageButtons.Ok, MessageIcon.Error) { IsList = true });
        (Control host, IWindowHandle handle) = Show(w, box);
        try
        {
            TextBox list = Assert.Single(box.GetLogicalDescendants().OfType<TextBox>());
            Assert.True(list.IsReadOnly);
            Assert.Equal(text, list.Text);
            Assert.True(host.Bounds.Height < 400, "the list scrolls instead of growing the window");
        }
        finally
        {
            handle.Close();
            TestApp.Pump();
        }
    });

    [AvaloniaFact]
    public void Options_window() => WithMainWindow(w =>
        ShowAndCheck(w, new OptionsWindow { DataContext = new OptionsViewModel(new AppSettings(), new ProjectSettings()) }));

    [AvaloniaFact]
    public void Project_properties_window() => WithMainWindow(w =>
        ShowAndCheck(w, new ProjectPropertiesWindow { DataContext = new ProjectPropertiesViewModel(new ProjectProperties { Title = "T" }, MediaTime.FromSeconds(12), "Author") }));

    [AvaloniaFact]
    public void Effects_window() => WithMainWindow(w =>
        ShowAndCheck(w, new EffectsWindow { DataContext = new ClipEffectsViewModel([EffectCatalog.All[0].Id]) }));

    [AvaloniaFact]
    public void Clip_volume_window() => WithMainWindow(w =>
        ShowAndCheck(w, new ClipVolumeWindow { DataContext = new ClipVolumeViewModel(AudioSettings.Default) }));

    [AvaloniaFact]
    public void Audio_levels_window() => WithMainWindow(w =>
        ShowAndCheck(w, new AudioLevelsWindow { DataContext = Shell.AudioLevels }));

    [AvaloniaFact]
    public void Publish_wizard_window_every_page() => WithMainWindow(w =>
    {
        Shell.Session.Editor.AddTitle(new TitleContent { Lines = ["Hello"] }, null, MediaTime.Zero);
        foreach (PublishPage page in new[] { PublishPage.Where, PublishPage.Name, PublishPage.Settings })
        {
            var vm = new PublishWizardViewModel(Shell.Session, Shell.Engine, new NoFiles(), new NoDialogs(), new NoMessages(), new InlineDispatcher(), Shell.Settings) { Page = page };
            ShowAndCheck(w, new PublishWizardWindow { DataContext = vm });
        }
    });

    private static Avalonia.Rect In(Control c, Control content) =>
        new(c.TranslatePoint(default, content)!.Value, c.Bounds.Size);

    [AvaloniaFact]
    public void Finish_page_and_volume_dialog_keep_their_margins() => WithMainWindow(w =>
    {
        var vm = new PublishWizardViewModel(Shell.Session, Shell.Engine, new NoFiles(), new NoDialogs(), new NoMessages(), new InlineDispatcher(), Shell.Settings) { Page = PublishPage.Finish };
        var wizard = new PublishWizardWindow { DataContext = vm };
        (_, IWindowHandle wizardHandle) = Show(w, wizard);
        try
        {
            var content = (Control)wizard.Content!;
            TextBlock close = wizard.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == "To close Publish Movie, click Finish.");
            Border page = wizard.GetLogicalDescendants().OfType<Border>().First(b => b.Background is Avalonia.Media.ISolidColorBrush { Color: var c } && c == Avalonia.Media.Colors.White);
            Avalonia.Rect line = In(close, content), white = In(page, content);
            Assert.True(white.Contains(line), $"{line} in {white}");
            Assert.InRange(white.Bottom - line.Bottom, 30, 60);
        }
        finally
        {
            wizardHandle.Close();
            TestApp.Pump();
        }

        var volume = new ClipVolumeWindow { DataContext = new ClipVolumeViewModel(AudioSettings.Default) };
        (_, IWindowHandle volumeHandle) = Show(w, volume);
        try
        {
            var content = (Control)volume.Content!;
            foreach (Button b in volume.GetLogicalDescendants().OfType<Button>().Where(b => b.Content is "OK" or "Cancel" or "_Reset"))
            {
                Avalonia.Rect r = In(b, content);
                Assert.True(content.Bounds.Width - r.Right >= 8, $"{b.Content} right margin {content.Bounds.Width - r.Right}");
                Assert.True(content.Bounds.Height - r.Bottom >= 8, $"{b.Content} bottom margin {content.Bounds.Height - r.Bottom}");
            }
        }
        finally
        {
            volumeHandle.Close();
            TestApp.Pump();
        }
    });

    [AvaloniaFact]
    public void About_window() => WithMainWindow(w => ShowAndCheck(w, new AboutWindow("Software rendering")));

    [AvaloniaFact]
    public void About_window_opens_with_OK_focused() => WithMainWindow(w =>
    {
        var about = new AboutWindow("Software rendering");
        (Control host, IWindowHandle handle) = Show(w, about);
        try
        {
            TestApp.Pump();
            object? focused = TopLevel.GetTopLevel(host)?.FocusManager?.GetFocusedElement();
            Assert.Equal("OK", Assert.IsType<Button>(focused).Content);
        }
        finally
        {
            handle.Close();
            TestApp.Pump();
        }
    });

    [AvaloniaFact]
    public void Licenses_window_shows_the_notices() => WithMainWindow(w =>
    {
        var licenses = new LicensesWindow();
        ShowAndCheck(w, licenses);
        string text = licenses.GetVisualDescendants().OfType<TextBox>().Single().Text ?? string.Empty;
        Assert.Contains("GNU General Public License", text, StringComparison.Ordinal);
        Assert.Contains("x264", text, StringComparison.Ordinal);
    });

    [AvaloniaFact]
    public void Clip_properties_window() => WithMainWindow(w =>
    {
        var m = new MediaItem
        {
            Kind = MediaKind.Video,
            Path = "/media/clip.mp4",
            Name = "clip",
            Duration = MediaTime.FromSeconds(4),
            Video = new VideoProperties { Width = 640, Height = 480, FrameRateNum = 30000, FrameRateDen = 1001, Codec = "h264" },
            Audio = new AudioProperties { SampleRate = 48000, Channels = 2, Codec = "aac" },
            Clips = [new SourceClip { Name = "clip", Start = MediaTime.Zero, End = MediaTime.FromSeconds(4) }],
        };
        var window = new ClipPropertiesWindow(ClipPropertiesViewModel.For(m, m.Clips[0]));
        Assert.Equal("clip Properties", WindowSettings.GetTitle(window));
        ShowAndCheck(w, window);
    });

    [AvaloniaFact]
    public void Color_dialog() => WithMainWindow(w =>
    {
        var custom = new List<uint>();
        ShowAndCheck(w, new ColorDialog(0xFF416FA6, custom, _ => { }));
        Assert.Equal(16, custom.Count);
    });

    private sealed class InlineDispatcher : IDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();
    }

    private sealed class NoFiles : IFileDialogs
    {
        public Task<IReadOnlyList<string>> ImportMediaAsync(MediaFilter filter, string? startFolder) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> OpenProjectAsync(string? startFolder) => Task.FromResult<string?>(null);

        public Task<string?> OpenMswmmAsync(string? startFolder) => Task.FromResult<string?>(null);

        public Task<string?> SaveProjectAsync(string suggestedName, string? startFolder) => Task.FromResult<string?>(null);

        public Task<string?> SavePackageAsync(string suggestedName, string? startFolder) => Task.FromResult<string?>(null);

        public Task<string?> PackageMediaFolderAsync(string projectName) => Task.FromResult<string?>(null);

        public Task<string?> SavePictureAsync(string suggestedName, string? startFolder) => Task.FromResult<string?>(null);

        public Func<string, string?, string?> NarrationSave { get; set; } = (_, _) => null;

        public Task<string?> SaveNarrationAsync(string suggestedName, string? startFolder) => Task.FromResult(NarrationSave(suggestedName, startFolder));

        public Task<string?> PickFolderAsync(string title, string? startFolder) => Task.FromResult<string?>(null);

        public Task<string?> OpenFileAsync(string title, string? startFolder) => Task.FromResult<string?>(null);
    }

    private sealed class NoMessages : IMessageBoxes
    {
        public Task<MessageResult> ShowAsync(MessageRequest request) => Task.FromResult(MessageResult.Ok);
    }

    private sealed class NoDialogs : IDialogService
    {
        public Task<bool> ShowOptionsAsync(OptionsViewModel options) => Task.FromResult(false);

        public Task<bool> ShowProjectPropertiesAsync(ProjectPropertiesViewModel properties) => Task.FromResult(false);

        public Task ShowClipPropertiesAsync(ClipPropertiesViewModel properties) => Task.CompletedTask;

        public Task<bool> ShowClipVolumeAsync(ClipVolumeViewModel volume) => Task.FromResult(false);

        public Task<bool> ShowEffectsAsync(ClipEffectsViewModel effects) => Task.FromResult(false);

        public Task ShowPublishWizardAsync(PublishWizardViewModel wizard) => Task.CompletedTask;

        public Task ShowAboutAsync() => Task.CompletedTask;

        public List<ViewModels.Dialogs.ProgressViewModel> Progress { get; } = [];

        public Task ShowProgressAsync(ViewModels.Dialogs.ProgressViewModel progress)

        {

            Progress.Add(progress);

            return Task.CompletedTask;

        }

        public void ShowAudioLevels(AudioLevelsViewModel levels)
        {
        }

        public void ShowFullScreen(ViewModels.Preview.MonitorViewModel monitor)
        {
        }

        public Task<uint?> PickColorAsync(uint initial, Action<uint> changed) => Task.FromResult<uint?>(null);

        public void PlayMovie(string path)
        {
        }

        public void ApplyTheme(string theme)
        {
        }
    }
}
