using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Decoding;

public sealed unsafe class DecodedFrame : IDisposable
{
    private AVFrame* _frame;

    private AVFrame* _sw;
    private readonly Lock _gate = new();

    internal DecodedFrame(AVFrame* frame, FramePixelFormat format, MediaTime pts, MediaTime duration, FrameColorSpace colorSpace, bool fullRange, bool hardware = false)
    {
        _frame = frame;
        IsHardware = hardware;
        Format = format;
        Pts = pts;
        Duration = duration;
        ColorSpace = colorSpace;
        FullRange = fullRange;
        Width = frame->width;
        Height = frame->height;
    }

    public int Width { get; }

    public int Height { get; }

    public FramePixelFormat Format { get; }

    public FrameColorSpace ColorSpace { get; }

    public bool FullRange { get; }

    public MediaTime Pts { get; }

    public MediaTime Duration { get; }

    public int Rotation { get; init; }

    public bool FlipHorizontal { get; init; }

    public double SampleAspect { get; init; } = 1.0;

    public bool IsDisposed => _frame == null;

    public bool IsHardware { get; }

    public bool HasSoftwareCopy
    {
        get
        {
            lock (_gate)
            {
                return _sw != null;
            }
        }
    }

    private AVFrame* Software()
    {
        ObjectDisposedException.ThrowIf(_frame == null, this);
        if (!IsHardware)
        {
            return _frame;
        }

        lock (_gate)
        {
            if (_sw == null)
            {
                AVFrame* sw = ffmpeg.av_frame_alloc();
                int r = ffmpeg.av_hwframe_transfer_data(sw, _frame, 0);
                if (r < 0)
                {
                    ffmpeg.av_frame_free(&sw);
                    FFmpeg.FFmpegRuntime.Throw(r, $"Transfer {HardwareDevice.Name} frame");
                }

                ffmpeg.av_frame_copy_props(sw, _frame);
                _sw = sw;
            }

            return _sw;
        }
    }

    public DmaBufFrame? TryExportDmaBuf()
    {
        ObjectDisposedException.ThrowIf(_frame == null, this);
        if (!IsHardware)
        {
            return null;
        }

        AVFrame* drm = ffmpeg.av_frame_alloc();
        drm->format = (int)AVPixelFormat.AV_PIX_FMT_DRM_PRIME;
        int r = ffmpeg.av_hwframe_map(drm, _frame, (int)(AvHwframeMap.AV_HWFRAME_MAP_READ | AvHwframeMap.AV_HWFRAME_MAP_DIRECT));
        if (r < 0)
        {
            ffmpeg.av_frame_free(&drm);
            return null;
        }

        return new DmaBufFrame(drm, Width, Height);
    }

    public MediaTime End => Pts + Duration;

    public bool Contains(MediaTime t) => t >= Pts && t < End;

    public int PlaneCount => Format switch
    {
        FramePixelFormat.Yuv420P or FramePixelFormat.Yuv420P10 => 3,
        FramePixelFormat.Nv12 or FramePixelFormat.P010 => 2,
        _ => 1,
    };

    public int BytesPerSample(int plane) => Format switch
    {
        FramePixelFormat.Yuv420P => 1,
        FramePixelFormat.Yuv420P10 => 2,
        FramePixelFormat.Nv12 => plane == 0 ? 1 : 2,
        FramePixelFormat.P010 => plane == 0 ? 2 : 4,
        _ => 4,
    };

    public FramePlane Plane(int index)
    {
        ObjectDisposedException.ThrowIf(_frame == null, this);
        bool chroma = index > 0 && Format is not (FramePixelFormat.Bgra or FramePixelFormat.Rgba);
        int w = chroma ? (Width + 1) / 2 : Width;
        int h = chroma ? (Height + 1) / 2 : Height;
        AVFrame* f = Software();
        return new FramePlane((IntPtr)f->data[(uint)index], f->linesize[(uint)index], w, h);
    }

    public DecodedFrame Clone()
    {
        ObjectDisposedException.ThrowIf(_frame == null, this);
        AVFrame* c = ffmpeg.av_frame_clone(_frame);
        return new DecodedFrame(c, Format, Pts, Duration, ColorSpace, FullRange, IsHardware)
        {
            SoftwareCopy = CloneSoftware(),
            Rotation = Rotation,
            FlipHorizontal = FlipHorizontal,
            SampleAspect = SampleAspect,
        };
    }

    public DecodedFrame WithTime(MediaTime pts, MediaTime duration)
    {
        ObjectDisposedException.ThrowIf(_frame == null, this);
        AVFrame* c = ffmpeg.av_frame_clone(_frame);
        return new DecodedFrame(c, Format, pts, duration, ColorSpace, FullRange, IsHardware)
        {
            SoftwareCopy = CloneSoftware(),
            Rotation = Rotation,
            FlipHorizontal = FlipHorizontal,
            SampleAspect = SampleAspect,
        };
    }

    public byte[] ToBgra(out int width, out int height)
    {
        ObjectDisposedException.ThrowIf(_frame == null, this);
        width = Width;
        height = Height;
        return FrameConverter.ToBgra(Software(), FullRange, ColorSpace);
    }

    private AVFrame* CloneSoftware()
    {
        lock (_gate)
        {
            return _sw == null ? null : ffmpeg.av_frame_clone(_sw);
        }
    }

    private AVFrame* SoftwareCopy
    {
        init => _sw = value;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_sw != null)
            {
                AVFrame* s = _sw;
                ffmpeg.av_frame_free(&s);
                _sw = null;
            }
        }

        if (_frame != null)
        {
            AVFrame* f = _frame;
            ffmpeg.av_frame_free(&f);
            _frame = null;
        }
    }
}
