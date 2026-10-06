using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AvaMovieMaker.Diagnostics;
using MiniAudioEx.Native;

namespace AvaMovieMaker.Audio.Native;

internal sealed unsafe class NativeDevice : IAudioDevice
{
    public delegate void PlaybackHandler(Span<float> output);

    public delegate void CaptureHandler(ReadOnlySpan<float> input);

    private static readonly ConcurrentDictionary<IntPtr, NativeDevice> Devices = new();

    private readonly ma_device_ptr _device;
    private readonly PlaybackHandler? _playback;
    private readonly CaptureHandler? _capture;
    private readonly int _channels;
    private IntPtr _deviceId;
    private long _frames;
    private bool _disposed;

    private NativeDevice(ma_device_ptr device, int channels, PlaybackHandler? playback, CaptureHandler? capture)
    {
        _device = device;
        _channels = channels;
        _playback = playback;
        _capture = capture;
    }

    public long Frames => Interlocked.Read(ref _frames);

    public int LatencyFrames { get; private set; }

    public static NativeDevice? OpenPlayback(int deviceIndex, int sampleRate, int channels, PlaybackHandler handler) =>
        Open(ma_device_type.playback, deviceIndex, sampleRate, channels, handler, null);

    public static NativeDevice? OpenCapture(int deviceIndex, int sampleRate, int channels, CaptureHandler handler) =>
        Open(ma_device_type.capture, deviceIndex, sampleRate, channels, null, handler);

    private static NativeDevice? Open(ma_device_type type, int index, int sampleRate, int channels, PlaybackHandler? playback, CaptureHandler? capture)
    {
        if (!MiniAudioContext.TryGet(out ma_context_ptr ctx))
        {
            return null;
        }

        ma_device_config cfg = MiniAudioNative.ma_device_config_init(type);
        cfg.sampleRate = (uint)sampleRate;
        cfg.periodSizeInMilliseconds = 10;
        cfg.performanceProfile = ma_performance_profile.low_latency;
        cfg.dataCallback = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, uint, void>)&OnData;

        IntPtr idCopy = IntPtr.Zero;
        if (index >= 0)
        {
            (ma_device_info[] play, ma_device_info[] cap) = MiniAudioContext.Devices();
            ma_device_info[] list = type == ma_device_type.playback ? play : cap;
            if (index < list.Length)
            {
                idCopy = Marshal.AllocHGlobal(sizeof(ma_device_id));
                *(ma_device_id*)idCopy = list[index].id;
            }
        }

        if (type == ma_device_type.playback)
        {
            cfg.playback.format = ma_format.f32;
            cfg.playback.channels = (uint)channels;
            cfg.playback.pDeviceID = new ma_device_id_ptr(idCopy);
        }
        else
        {
            cfg.capture.format = ma_format.f32;
            cfg.capture.channels = (uint)channels;
            cfg.capture.pDeviceID = new ma_device_id_ptr(idCopy);
        }

        var dev = new ma_device_ptr(true);
        var result = new NativeDevice(dev, channels, playback, capture) { _deviceId = idCopy };
        Devices[dev.pointer] = result;
        ma_result r;
        lock (MiniAudioContext.Gate)
        {
            r = MiniAudioNative.ma_device_init(ctx, ref cfg, dev);
        }
        if (r != ma_result.success)
        {
            Devices.TryRemove(dev.pointer, out _);
            dev.Free();
            if (idCopy != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(idCopy);
            }

            Log.Warn("audio", $"Could not open the {type} device: {r}");
            return null;
        }

        if (type == ma_device_type.playback)
        {
            ma_device_playback_ptr pb = MiniAudioNative.ma_device_get_playback(dev);
            if (pb.pointer != IntPtr.Zero)
            {
                ma_device_playback* p = (ma_device_playback*)pb.pointer;
                result.LatencyFrames = (int)(p->internalPeriodSizeInFrames * p->internalPeriods);
            }
        }

        return result;
    }

    public bool Start() => MiniAudioNative.ma_device_start(_device) == ma_result.success;

    public bool Stop() => MiniAudioNative.ma_device_stop(_device) == ma_result.success;

    [UnmanagedCallersOnly]
    private static void OnData(IntPtr device, IntPtr output, IntPtr input, uint frames)
    {
        if (!Devices.TryGetValue(device, out NativeDevice? d))
        {
            return;
        }

        try
        {
            int n = (int)frames * d._channels;
            if (output != IntPtr.Zero)
            {
                var span = new Span<float>((void*)output, n);
                if (d._playback is not null)
                {
                    d._playback(span);
                }
                else
                {
                    span.Clear();
                }
            }

            if (input != IntPtr.Zero && d._capture is not null)
            {
                d._capture(new ReadOnlySpan<float>((void*)input, n));
            }
        }
        catch (Exception e)
        {
            Log.Error("audio", "Audio callback failed", e);
        }

        Interlocked.Add(ref d._frames, frames);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (MiniAudioContext.Gate)
        {
            MiniAudioNative.ma_device_uninit(_device);
        }
        Devices.TryRemove(_device.pointer, out _);
        _device.Free();
        if (_deviceId != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_deviceId);
            _deviceId = IntPtr.Zero;
        }
    }
}
