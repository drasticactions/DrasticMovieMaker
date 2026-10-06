using AvaMovieMaker.Diagnostics;
using MiniAudioEx.Native;

namespace AvaMovieMaker.Audio.Native;

internal static class MiniAudioContext
{
    internal static readonly Lock Gate = new();
    private static ma_context_ptr _context;
    private static bool? _ready;

    public static bool TryGet(out ma_context_ptr context)
    {
        lock (Gate)
        {
            if (_ready is null)
            {
                try
                {
                    var ctx = new ma_context_ptr(true);
                    ma_result r = MiniAudioNative.ma_context_init(null, ctx);
                    _ready = r == ma_result.success;
                    if (_ready == true)
                    {
                        _context = ctx;
                    }
                    else
                    {
                        Log.Warn("audio", $"miniaudio context failed: {r}");
                    }
                }
                catch (DllNotFoundException e)
                {
                    Log.Warn("audio", $"libminiaudioex not found: {e.Message}");
                    _ready = false;
                }
            }

            context = _context;
            return _ready.Value;
        }
    }

    public static (ma_device_info[] Playback, ma_device_info[] Capture) Devices()
    {
        if (!TryGet(out ma_context_ptr ctx))
        {
            return ([], []);
        }

        lock (Gate)
        {
            ma_result r = MiniAudioNative.ma_context_get_devices(ctx, out ma_device_info[] playback, out ma_device_info[] capture);
            return r == ma_result.success ? (playback ?? [], capture ?? []) : ([], []);
        }
    }

    public static IReadOnlyList<AudioDeviceInfo> List(bool capture)
    {
        (ma_device_info[] playback, ma_device_info[] captureList) = Devices();
        ma_device_info[] list = capture ? captureList : playback;
        var result = new List<AudioDeviceInfo>(list.Length);
        for (int i = 0; i < list.Length; i++)
        {
            result.Add(new AudioDeviceInfo(i, list[i].GetName(), list[i].isDefault != 0, capture));
        }

        return result;
    }
}
