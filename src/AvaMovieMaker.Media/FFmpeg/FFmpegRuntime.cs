using System.Globalization;
using System.Runtime.InteropServices;
using AvaMovieMaker.Diagnostics;
using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;

namespace AvaMovieMaker.Media.FFmpeg;

public static unsafe class FFmpegRuntime
{
    private static readonly Lock Gate = new();
    private static bool _initialized;

    public static readonly (string Name, int Major)[] Libraries =
    [
        ("avutil", 61),
        ("swresample", 7),
        ("swscale", 10),
        ("avcodec", 63),
        ("avformat", 63),
    ];

    public static string Source { get; private set; } = "none";

    public static bool IsAvailable => _initialized;

    public static string VersionText
    {
        get
        {
            if (!_initialized)
            {
                return Strings.FFmpegNotLoaded;
            }

            uint v = ffmpeg.avformat_version();
            return $"FFmpeg {ffmpeg.av_version_info()} ({Source}), libavformat {v >> 16}.{(v >> 8) & 0xFF}.{v & 0xFF}";
        }
    }

    public static string FileName(string name, int major) =>
        OperatingSystem.IsWindows() ? $"{name}-{major}.dll"
        : OperatingSystem.IsMacOS() ? $"lib{name}.{major}.dylib"
        : $"lib{name}.so.{major}";

    public static IEnumerable<string> BundledFolders()
    {
        string app = AppContext.BaseDirectory;
        yield return app;
        if (OperatingSystem.IsMacOS())
        {
            yield return Path.GetFullPath(Path.Combine(app, "..", "Frameworks"));
        }
    }

    public static string? Check()
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return null;
            }

            if (LoadLibraries() is { } error)
            {
                return error;
            }

            (string, uint)[] versions =
            [
                ("avutil", ffmpeg.avutil_version()),
                ("swresample", ffmpeg.swresample_version()),
                ("swscale", ffmpeg.swscale_version()),
                ("avcodec", ffmpeg.avcodec_version()),
                ("avformat", ffmpeg.avformat_version()),
            ];
            foreach ((string name, uint version) in versions)
            {
                int expected = Libraries.First(l => l.Name == name).Major;
                int major = (int)(version >> 16);
                if (major != expected)
                {
                    return string.Format(CultureInfo.CurrentCulture, Strings.FFmpegWrongVersion, name, major, expected, InstallHint);
                }
            }

            InstallLogCallback();
            _initialized = true;
            Log.Info("ffmpeg", VersionText);
            return null;
        }
    }

    private static string InstallHint =>
        OperatingSystem.IsWindows() ? Strings.FFmpegInstallHintWindows
        : OperatingSystem.IsMacOS() ? Strings.FFmpegInstallHintMacOS
        : Strings.FFmpegInstallHintLinux;

    private static string? LoadLibraries()
    {
        string? bundled = BundledFolders().FirstOrDefault(folder =>
            Libraries.All(l => File.Exists(Path.Combine(folder, FileName(l.Name, l.Major)))));
        if (bundled is not null)
        {
            if (OperatingSystem.IsLinux())
            {
                LibvaLoader.Load(bundled);
            }

            if (TryLoadFrom(bundled, out string? error))
            {
                Source = "bundled";
                return null;
            }

            Log.Warn("ffmpeg", $"The bundled FFmpeg in {bundled} could not be loaded ({error}); trying the system's.");
        }

        string? firstError = null;
        foreach (string folder in SystemFolders())
        {
            if (TryLoadFrom(folder, out string? error))
            {
                Source = "system";
                return null;
            }

            firstError ??= error;
        }

        return $"{firstError?.TrimEnd('.')}. {InstallHint}";
    }

    private static IEnumerable<string> SystemFolders()
    {
        yield return string.Empty;
        if (OperatingSystem.IsMacOS())
        {
            yield return "/opt/homebrew/lib";
            yield return "/usr/local/lib";
        }
    }

    private static bool TryLoad(string folder, string name, int major)
    {
        string file = FileName(name, major);
        if (!NativeLibrary.TryLoad(folder.Length == 0 ? file : Path.Combine(folder, file), out IntPtr handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }

    private static bool TryLoadFrom(string folder, out string? error)
    {
        foreach ((string name, int major) in Libraries)
        {
            if (!TryLoad(folder, name, major))
            {
                error = string.Format(CultureInfo.CurrentCulture, Strings.FFmpegLibraryNotFound, FileName(name, major));
                return false;
            }
        }

        try
        {
            DynamicallyLoadedBindings.LibrariesPath = folder;
            DynamicallyLoadedBindings.ThrowErrorIfFunctionNotFound = false;
            DynamicallyLoadedBindings.Initialize();
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = string.Format(CultureInfo.CurrentCulture, Strings.FFmpegLibrariesNotLoaded, e.Message);
            return false;
        }
    }

    public static void EnsureLoaded()
    {
        if (!_initialized && Check() is { } error)
        {
            throw new FFmpegException(error);
        }
    }

    public static string ErrorText(int error)
    {
        const int size = 256;
        byte* buf = stackalloc byte[size];
        ffmpeg.av_strerror(error, buf, size);
        return Marshal.PtrToStringUTF8((IntPtr)buf) ?? $"error {error}";
    }

    public static int Throw(int result, string what)
    {
        if (result < 0)
        {
            throw new FFmpegException($"{what}: {ErrorText(result)}", result);
        }

        return result;
    }

    public static bool HasEncoder(string name)
    {
        EnsureLoaded();
        return ffmpeg.avcodec_find_encoder_by_name(name) != null;
    }

    public static bool HasDecoder(AVCodecID id)
    {
        EnsureLoaded();
        return ffmpeg.avcodec_find_decoder(id) != null;
    }

    private static void InstallLogCallback()
    {
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_WARNING);
        var func = new av_log_set_callback_callback_func
        {
            Pointer = (IntPtr)(delegate* unmanaged<void*, int, byte*, byte*, void>)&OnLog,
        };
        ffmpeg.av_log_set_callback(func);
    }

    [UnmanagedCallersOnly]
    private static void OnLog(void* avcl, int level, byte* fmt, byte* vl)
    {
        if (level > ffmpeg.AV_LOG_WARNING)
        {
            return;
        }

        try
        {
            const int size = 1024;
            byte* line = stackalloc byte[size];
            int printPrefix = 1;
            string? format = Marshal.PtrToStringUTF8((IntPtr)fmt);
            if (format is null)
            {
                return;
            }

            ffmpeg.av_log_format_line2(avcl, level, format, vl, line, size, &printPrefix);
            string text = (Marshal.PtrToStringUTF8((IntPtr)line) ?? string.Empty).TrimEnd();
            if (text.Length > 0)
            {
                Log.Write(level <= ffmpeg.AV_LOG_ERROR ? LogLevel.Error : LogLevel.Warning, "ffmpeg", text);
            }
        }
        catch
        {
        }
    }
}
