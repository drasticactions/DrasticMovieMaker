using System.Runtime.InteropServices;
using AvaMovieMaker.Diagnostics;

namespace AvaMovieMaker.Media.FFmpeg;

internal static class LibvaLoader
{
    private static readonly string[] Sonames = ["libva.so.2", "libva-drm.so.2"];

    public static bool HasSystemLibva { get; private set; }

    public static void Load(string ffmpegFolder)
    {
        bool system = true;
        foreach (string soname in Sonames)
        {
            if (NativeLibrary.TryLoad(soname, out _))
            {
                continue;
            }

            system = false;
            string stub = Path.Combine(ffmpegFolder, "fallback", soname);
            if (!NativeLibrary.TryLoad(stub, out _))
            {
                Log.Warn("ffmpeg", $"Neither the system's {soname} nor {stub} could be loaded.");
            }
        }

        HasSystemLibva = system;
        if (!system)
        {
            Log.Info("ffmpeg", "VA-API not available: no libva on this system");
        }
    }
}
