using Avalonia;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Threading;
using AvaWpf;
using AvaWpf.Animations;
using AvaMovieMaker.Tests;
using AvaMovieMaker.Views;
using AvaMovieMaker.Settings;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace AvaMovieMaker.Tests;

internal sealed class MemorySettingsStore(AppSettings settings) : ISettingsStore
{
    public AppSettings Settings { get; } = settings;

    public void Save()
    {
    }
}

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        Environment.SetEnvironmentVariable("AMM_SOFTWARE_RENDER", "1");
        App.NoAudio = true;

        App.Platform = App.Platform with { MenuHost = new AvaMovieMaker.Menus.InWindowMenuHost() };

        WpfAnimations.IsEnabled = false;
        App.SettingsOverride = new MemorySettingsStore(NewSettings());
        Logger.Sink = LogCollector.Instance;
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .WithAvaWpfFonts()
            .WithBundledFonts()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    public static AppSettings NewSettings() => new() { Theme = "Light", AutoRecoveryEnabled = false, ShowTasksPane = true };

    public static App Current => (App)Application.Current!;

    public static MainWindow OpenMainWindow(AppSettings? settings = null)
    {
        App.SettingsOverride = new MemorySettingsStore(settings ?? NewSettings());
        MainWindow w = Current.CreateMainWindow();
        w.Show();
        Pump();
        return w;
    }

    public static void Close(MainWindow w)
    {
        Current.Shell?.Session.Undo.MarkSaved();
        w.Close();
        Pump();
        Assert.False(w.IsVisible, "the main window did not close");
    }

    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}

public sealed class LogCollector : ILogSink
{
    public static readonly LogCollector Instance = new();

    private readonly Lock _gate = new();
    private readonly List<string> _messages = [];

    public bool IsEnabled(LogEventLevel level, string area) =>
        level >= LogEventLevel.Error || (level >= LogEventLevel.Warning && area == LogArea.Binding);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Add(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Add(level, area, source, messageTemplate, propertyValues);

    private void Add(LogEventLevel level, string area, object? source, string template, object?[] values)
    {
        if (!IsEnabled(level, area))
        {
            return;
        }

        string text = $"[{level} {area}] {source?.GetType().Name}: {template} | {string.Join(", ", values.Select(v => v?.ToString()))}";
        lock (_gate)
        {
            _messages.Add(text);
        }
    }

    public IReadOnlyList<string> Take()
    {
        lock (_gate)
        {
            var copy = _messages.ToList();
            _messages.Clear();
            return copy;
        }
    }
}
