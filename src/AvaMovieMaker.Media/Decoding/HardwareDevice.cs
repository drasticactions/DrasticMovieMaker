using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.FFmpeg;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Decoding;

internal static unsafe class HardwareDevice
{
    private static readonly Lock Gate = new();
    private static AVBufferRef* _device;
    private static bool _tried;

    public static string RenderNode { get; set; } = "/dev/dri/renderD128";

    public static AVHWDeviceType Type { get; } =
        OperatingSystem.IsLinux() ? AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI
        : OperatingSystem.IsMacOS() ? AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX
        : OperatingSystem.IsWindows() ? AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA
        : AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;

    public static AVPixelFormat PixelFormat { get; } = Type switch
    {
        AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
        AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
        AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_D3D11,
        _ => AVPixelFormat.AV_PIX_FMT_NONE,
    };

    public static string Name => Type switch
    {
        AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => "VAAPI",
        AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => "VideoToolbox",
        AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => "D3D11VA",
        _ => "none",
    };

    public static AVBufferRef* Ref()
    {
        lock (Gate)
        {
            if (!_tried)
            {
                _tried = true;
                Create();
            }

            return _device == null ? null : ffmpeg.av_buffer_ref(_device);
        }
    }

    public static AVBufferRef* RefVaapi() => Type == AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI ? Ref() : null;

    private static void Create()
    {
        if (Type == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            return;
        }

        string? where = Type == AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI ? RenderNode : null;
        if (where is not null && !File.Exists(where))
        {
            Log.Info("media", $"No {Name} device: {where} does not exist");
            return;
        }

        AVBufferRef* dev = null;
        int r = ffmpeg.av_hwdevice_ctx_create(&dev, Type, where, null, 0);
        if (r >= 0)
        {
            _device = dev;
            Log.Info("media", where is null ? $"{Name} device" : $"{Name} device on {where}");
        }
        else
        {
            Log.Info("media", $"No {Name} device: {FFmpegRuntime.ErrorText(r)}");
        }
    }

    public static bool Supports(AVCodec* codec)
    {
        if (Type == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            return false;
        }

        for (int i = 0; ; i++)
        {
            AVCodecHWConfig* cfg = ffmpeg.avcodec_get_hw_config(codec, i);
            if (cfg == null)
            {
                return false;
            }

            if (cfg->device_type == Type && (cfg->methods & 0x01 ) != 0)
            {
                return true;
            }
        }
    }
}
