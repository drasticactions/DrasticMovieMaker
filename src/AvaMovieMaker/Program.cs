using Avalonia;
using AvaWpf;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media.FFmpeg;

namespace AvaMovieMaker;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Log.OpenFile(AppPaths.LogFile);
        Log.EchoToConsole = args.Contains("--verbose");
        if (args.Contains("--smoke"))
        {
            return Smoke.Run(args);
        }

        App.StartupPaths = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).Select(Path.GetFullPath).ToList();
        App.FFmpegError = FFmpegRuntime.Check();
        if (App.FFmpegError is not null)
        {
            Log.Error("startup", App.FFmpegError);
        }

        if (OperatingSystem.IsWindows())
        {
            Win32DropTarget.Install();
        }

        int code = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return App.FFmpegError is null ? code : 1;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseWaylandWithFallback()
            .WithAvaWpfFonts()
            .WithBundledFonts()
            .With(new Win32PlatformOptions { CompositionMode = [Win32CompositionMode.RedirectionSurface] })
            .LogToTrace();
}
