using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using AvaWpf;
using AvaMovieMaker.Dialogs;
using AvaMovieMaker.Services;
using AvaMovieMaker.Views;
using AvaMovieMaker.Assets;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Settings;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker;

public partial class App : Application
{
    public static IReadOnlyList<string> StartupPaths { get; set; } = [];

    public static ISettingsStore? SettingsOverride { get; set; }

    public static string? FFmpegError { get; set; }

    public static bool NoAudio { get; set; }

    public static AppPlatform Platform { get; set; } = AppPlatform.ForDesktop();

    public ShellViewModel? Shell { get; private set; }

    private Task? _started;

    private readonly List<string> _openBeforeStart = [];

    public MainWindow? MainWindow { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (Platform.MenuHost is Menus.NativeMenuHost menus)
        {
            menus.InstallApplicationMenu(this);
        }
    }

    public static void RegisterTitleFonts()
    {
        foreach (Stream s in AssetFonts.OpenAll())
        {
            using (s)
            {
                TitleFonts.Register(s);
            }
        }
    }

    public static ThemeVariant VariantFor(string theme) => theme switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public MainWindow CreateMainWindow()
    {
        var window = new MainWindow();
        Shell = CreateShell(window);
        window.Attach(Shell);
        MainWindow = window;
        return window;
    }

    private ShellViewModel CreateShell(Visual owner)
    {
        ISettingsStore store = SettingsOverride ?? new JsonSettingsStore();
        RequestedThemeVariant = VariantFor(store.Settings.Theme);
        RegisterTitleFonts();
        var dispatcher = new UiDispatcher();
        IFileDialogs files = Platform.FileDialogs(owner);
        var messages = new MessageBoxes(owner, store);
        var dialogs = new DialogService(owner, store, Platform.FullScreen, Platform.Shell);
        var engine = new EngineHost(preferGpu: true, hardwareDecode: store.Settings.HardwareDecode, audioDevice: store.Settings.PlaybackDevice, withAudio: !NoAudio);
        dialogs.Renderer = () => engine.Device.Description;
        return new ShellViewModel(engine, files, messages, dialogs, dispatcher, store, new ProjectClipboard());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && FFmpegError is { } error)
        {
            desktop.MainWindow = ErrorWindow(error);
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
        {
            MainWindow w = CreateMainWindow();
            app.MainWindow = w;
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            w.Opened += async (_, _) =>
            {
                _started = Shell!.StartAsync([.. StartupPaths, .. _openBeforeStart]);
                await _started;
            };
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            {
                activatable.Activated += OnActivated;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is not FileActivatedEventArgs files || Shell is not { } shell)
        {
            return;
        }

        List<string> paths = [.. files.Files.Select(f => f.TryGetLocalPath()).OfType<string>()];
        if (paths.Count == 0)
        {
            return;
        }

        if (_started is null)
        {
            _openBeforeStart.AddRange(paths);
            return;
        }

        MainWindow?.Activate();
        await _started;
        await shell.DropFilesAsync(paths);
    }

    private static ThemeWindow ErrorWindow(string error)
    {
        MessageBoxWindow box = MessageBoxWindow.Create(ViewModels.Strings.FFmpegMissingTitle, new MessageRequest(error, MessageButtons.Ok, MessageIcon.Error));
        return new ThemeWindow
        {
            Title = WindowSettings.GetTitle(box),
            FrameKind = WindowFrameKind.Dialog,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            MinWidth = WindowSettings.GetMinWidth(box),
            MaxWidth = WindowSettings.GetMaxWidth(box),
            Content = box,
        };
    }
}
