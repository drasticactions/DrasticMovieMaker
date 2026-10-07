using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Media.Probing;
using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Decoding;

public sealed unsafe class VideoDecoder : IDisposable
{
    private static readonly MediaTime ForwardWindow = MediaTime.FromSeconds(2.5);

    private static readonly MediaTime RoundingTolerance = MediaTime.FromMilliseconds(0.1);

    private static readonly MediaTime SkipMargin = MediaTime.FromSeconds(0.5);

    private readonly InputFile _input;
    private readonly int _stream;
    private readonly Rational _timeBase;
    private readonly long _startPts;
    private readonly List<long> _keyframes = [];
    private AVCodecContext* _codec;
    private AVPacket* _packet;
    private AVFrame* _frame;
    private AVFrame* _swFrame;
    private bool _hardware;
    private bool _demuxEof;
    private bool _decoderEof;
    private DecodedFrame? _last;
    private DecodedFrame? _pending;

    public VideoDecoder(string path, bool allowHardware = true)
    {
        Path = path;
        _input = new InputFile(path);
        try
        {
            _stream = _input.FindBest(AVMediaType.AVMEDIA_TYPE_VIDEO);
            if (_stream < 0)
            {
                throw new FFmpegException($"{System.IO.Path.GetFileName(path)} has no video stream.");
            }

            AVStream* s = _input.Stream(_stream);
            _timeBase = FFmpegUtil.ToRational(s->time_base);
            _startPts = s->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : s->start_time;
            FrameRate = FFmpegUtil.FrameRate(s);
            Rotation = FFmpegUtil.StreamRotation(s);
            AVRational sar = s->codecpar->sample_aspect_ratio;
            SampleAspect = sar.num > 0 && sar.den > 0 ? sar.num / (double)sar.den : 1.0;
            IsPicture = MediaFormats.IsPictureExtension(path);
            if (IsPicture)
            {
                (Rotation, FlipHorizontal) = ExifReader.Transform(ExifReader.Read(path).Orientation);
            }

            Duration = s->duration > 0 ? MediaTime.FromTimeBase(s->duration, _timeBase)
                : _input.Context->duration > 0 ? MediaTime.FromTimeBase(_input.Context->duration, new Rational(1, ffmpeg.AV_TIME_BASE))
                : MediaTime.Zero;

            for (int i = 0; i < _input.StreamCount; i++)
            {
                if (i != _stream)
                {
                    _input.Stream(i)->discard = AVDiscard.AVDISCARD_ALL;
                }
            }

            OpenCodec(allowHardware && !IsPicture);
            _packet = ffmpeg.av_packet_alloc();
            _frame = ffmpeg.av_frame_alloc();
            _swFrame = ffmpeg.av_frame_alloc();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string Path { get; }

    public Rational FrameRate { get; }

    public MediaTime Duration { get; }

    public int Rotation { get; }

    public bool FlipHorizontal { get; }

    public double SampleAspect { get; }

    public bool IsPicture { get; }

    public bool IsHardware => _hardware;

    public bool KeepHardwareFrames { get; set; }

    public bool SkipLoopFilter
    {
        set
        {
            if (_codec != null)
            {
                _codec->skip_loop_filter = value ? AVDiscard.AVDISCARD_NONREF : AVDiscard.AVDISCARD_DEFAULT;
            }
        }
    }

    public IReadOnlyList<MediaTime> KnownKeyframes => _keyframes.Select(ToMedia).ToList();

    private void OpenCodec(bool allowHardware)
    {
        AVStream* s = _input.Stream(_stream);
        AVCodec* dec = ffmpeg.avcodec_find_decoder(s->codecpar->codec_id);
        if (dec == null)
        {
            throw new FFmpegException($"No decoder for {FFmpegUtil.CodecName(s->codecpar->codec_id)}.");
        }

        if (allowHardware && HardwareDevice.Supports(dec) && TryOpen(dec, s, hardware: true))
        {
            _hardware = true;
            return;
        }

        if (!TryOpen(dec, s, hardware: false))
        {
            throw new FFmpegException($"Could not open the {FFmpegUtil.CodecName(s->codecpar->codec_id)} decoder.");
        }
    }

    private bool TryOpen(AVCodec* dec, AVStream* s, bool hardware)
    {
        AVCodecContext* c = ffmpeg.avcodec_alloc_context3(dec);
        if (ffmpeg.avcodec_parameters_to_context(c, s->codecpar) < 0)
        {
            ffmpeg.avcodec_free_context(&c);
            return false;
        }

        c->pkt_timebase = s->time_base;
        if (hardware)
        {
            AVBufferRef* dev = HardwareDevice.Ref();
            if (dev == null)
            {
                ffmpeg.avcodec_free_context(&c);
                return false;
            }

            c->hw_device_ctx = dev;
        }
        else
        {
            c->thread_count = 0;
            c->thread_type = ffmpeg.FF_THREAD_FRAME | ffmpeg.FF_THREAD_SLICE;
        }

        int r = ffmpeg.avcodec_open2(c, dec, null);
        if (r < 0)
        {
            ffmpeg.avcodec_free_context(&c);
            if (hardware)
            {
                Log.Info("media", $"{HardwareDevice.Name} open failed for {Path}: {FFmpegRuntime.ErrorText(r)}");
            }

            return false;
        }

        _codec = c;
        return true;
    }

    private MediaTime ToMedia(long pts) => MediaTime.FromTimeBase(pts - _startPts, _timeBase);

    private long ToStream(MediaTime t) => t.ToTimeBase(_timeBase) + _startPts;

    public bool CanReach(MediaTime time)
    {
        if (IsPicture)
        {
            return true;
        }

        time += RoundingTolerance;
        return _last is not null && time >= _last.Pts && time - _last.End < ForwardWindow;
    }

    public DecodedFrame? GetFrame(MediaTime time)
    {
        if (time < MediaTime.Zero)
        {
            time = MediaTime.Zero;
        }

        time += RoundingTolerance;
        if (_last is not null && (_last.Contains(time) || IsPicture))
        {
            return _last.Clone();
        }

        if (IsPicture)
        {
            DecodedFrame? still = TakeNext();
            if (still is null)
            {
                return null;
            }

            SetLast(still);
            return still.Clone();
        }

        bool canDecodeForward = _last is not null && time >= _last.Pts && time - _last.End < ForwardWindow;
        if (canDecodeForward && _keyframes.Count > 0)
        {
            long target = ToStream(time);
            long lastPts = ToStream(_last!.End);
            int k = _keyframes.BinarySearch(target);
            k = k >= 0 ? k : ~k - 1;
            if (k >= 0 && _keyframes[k] > lastPts && time - ToMedia(_keyframes[k]) < time - _last.End)
            {
                canDecodeForward = false;
            }
        }

        if (!canDecodeForward)
        {
            Seek(time);
        }

        MediaTime? skipBefore = time < Duration - SkipMargin ? time : null;
        while (true)
        {
            DecodedFrame? next = TakeNext(skipBefore);
            if (next is null)
            {
                return _last?.Clone();
            }

            if (next.Pts > time && _last is not null && _last.Pts <= time)
            {
                _pending = next;
                return _last.Clone();
            }

            SetLast(next);
            if (next.Contains(time) || next.Pts > time)
            {
                return next.Clone();
            }
        }
    }

    public DecodedFrame? ReadNext()
    {
        DecodedFrame? next = TakeNext();
        if (next is not null)
        {
            SetLast(next);
            return next.Clone();
        }

        return null;
    }

    public void SeekTo(MediaTime time)
    {
        Seek(time);
        while (true)
        {
            DecodedFrame? f = TakeNext();
            if (f is null)
            {
                return;
            }

            if (f.End > time)
            {
                _pending = f;
                return;
            }

            SetLast(f);
        }
    }

    private void SetLast(DecodedFrame f)
    {
        _last?.Dispose();
        _last = f;
    }

    private DecodedFrame? TakeNext(MediaTime? skipBefore = null)
    {
        if (_pending is not null)
        {
            DecodedFrame p = _pending;
            _pending = null;
            return p;
        }

        return DecodeOne(skipBefore);
    }

    private void Seek(MediaTime time)
    {
        _pending?.Dispose();
        _pending = null;
        _last?.Dispose();
        _last = null;

        MediaTime seekTo = time;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            long ts = ToStream(seekTo);
            int r = ffmpeg.av_seek_frame(_input.Context, _stream, ts, ffmpeg.AVSEEK_FLAG_BACKWARD);
            if (r < 0)
            {
                ffmpeg.av_seek_frame(_input.Context, _stream, _startPts, ffmpeg.AVSEEK_FLAG_BACKWARD | ffmpeg.AVSEEK_FLAG_ANY);
            }

            ffmpeg.avcodec_flush_buffers(_codec);
            _demuxEof = false;
            _decoderEof = false;
            DecodedFrame? first = DecodeOne();
            if (first is null)
            {
                return;
            }

            if (first.Pts <= time || seekTo <= MediaTime.Zero)
            {
                _pending = first;
                return;
            }

            first.Dispose();
            seekTo = MediaTime.Max(MediaTime.Zero, seekTo - MediaTime.FromSeconds(1 << attempt));
        }
    }

    private DecodedFrame? DecodeOne(MediaTime? skipBefore = null)
    {
        while (true)
        {
            if (_decoderEof)
            {
                return null;
            }

            int r = ffmpeg.avcodec_receive_frame(_codec, _frame);
            if (r == 0)
            {
                if (skipBefore is { } s && EndsBefore(_frame, s))
                {
                    ffmpeg.av_frame_unref(_frame);
                    continue;
                }

                return Wrap();
            }

            if (r == ffmpeg.AVERROR_EOF)
            {
                _decoderEof = true;
                return null;
            }

            if (r != ffmpeg.AVERROR(Errno.Again))
            {
                FFmpegRuntime.Throw(r, "Decode video");
            }

            if (_demuxEof)
            {
                _decoderEof = true;
                return null;
            }

            while (true)
            {
                int rr = ffmpeg.av_read_frame(_input.Context, _packet);
                if (rr < 0)
                {
                    _demuxEof = true;
                    ffmpeg.avcodec_send_packet(_codec, null);
                    break;
                }

                if (_packet->stream_index != _stream)
                {
                    ffmpeg.av_packet_unref(_packet);
                    continue;
                }

                if ((_packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0 && _packet->pts != ffmpeg.AV_NOPTS_VALUE)
                {
                    int i = _keyframes.BinarySearch(_packet->pts);
                    if (i < 0)
                    {
                        _keyframes.Insert(~i, _packet->pts);
                    }
                }

                int sr = ffmpeg.avcodec_send_packet(_codec, _packet);
                ffmpeg.av_packet_unref(_packet);
                if (sr < 0 && sr != ffmpeg.AVERROR(Errno.Again) && sr != ffmpeg.AVERROR_INVALIDDATA)
                {
                    FFmpegRuntime.Throw(sr, "Send packet");
                }

                break;
            }
        }
    }

    private bool EndsBefore(AVFrame* f, MediaTime time)
    {
        long pts = f->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? f->best_effort_timestamp : f->pts;
        if (pts == ffmpeg.AV_NOPTS_VALUE)
        {
            return false;
        }

        MediaTime dur = f->duration > 0 ? MediaTime.FromTimeBase(f->duration, _timeBase) : MediaTime.FrameDuration(FrameRate);
        return ToMedia(pts) + dur <= time;
    }

    private DecodedFrame Wrap()
    {
        AVFrame* src = _frame;
        AVFrame* owned;
        if (src->format == (int)AVPixelFormat.AV_PIX_FMT_VAAPI && KeepHardwareFrames && src->hw_frames_ctx != null
            && ((AVHWFramesContext*)src->hw_frames_ctx->data)->sw_format is var swf
            && swf is AVPixelFormat.AV_PIX_FMT_NV12 or AVPixelFormat.AV_PIX_FMT_P010LE)
        {
            AVFrame* hw = ffmpeg.av_frame_clone(src);
            ffmpeg.av_frame_unref(_frame);
            long hpts = hw->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? hw->best_effort_timestamp : hw->pts;
            if (hpts == ffmpeg.AV_NOPTS_VALUE)
            {
                hpts = _last is not null ? ToStream(_last.End) : _startPts;
            }

            MediaTime hstart = ToMedia(hpts);
            MediaTime hdur = hw->duration > 0 ? MediaTime.FromTimeBase(hw->duration, _timeBase) : MediaTime.FrameDuration(FrameRate);
            FrameColorSpace hcs = hw->colorspace switch
            {
                AVColorSpace.AVCOL_SPC_BT709 => FrameColorSpace.Bt709,
                AVColorSpace.AVCOL_SPC_BT2020_NCL or AVColorSpace.AVCOL_SPC_BT2020_CL => FrameColorSpace.Bt2020,
                AVColorSpace.AVCOL_SPC_BT470BG or AVColorSpace.AVCOL_SPC_SMPTE170M or AVColorSpace.AVCOL_SPC_FCC => FrameColorSpace.Bt601,
                _ => hw->height >= 720 ? FrameColorSpace.Bt709 : FrameColorSpace.Bt601,
            };
            return new DecodedFrame(hw, swf == AVPixelFormat.AV_PIX_FMT_NV12 ? FramePixelFormat.Nv12 : FramePixelFormat.P010,
                hstart, hdur, hcs, hw->color_range == AVColorRange.AVCOL_RANGE_JPEG, hardware: true)
            {
                Rotation = Rotation,
                FlipHorizontal = FlipHorizontal,
                SampleAspect = SampleAspect,
            };
        }

        if (src->hw_frames_ctx != null && src->format == (int)HardwareDevice.PixelFormat)
        {
            ffmpeg.av_frame_unref(_swFrame);
            FFmpegRuntime.Throw(ffmpeg.av_hwframe_transfer_data(_swFrame, src, 0), $"Transfer {HardwareDevice.Name} frame");
            ffmpeg.av_frame_copy_props(_swFrame, src);
            owned = ffmpeg.av_frame_clone(_swFrame);
            ffmpeg.av_frame_unref(_swFrame);
        }
        else
        {
            owned = ffmpeg.av_frame_clone(src);
        }

        ffmpeg.av_frame_unref(_frame);

        long pts = owned->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? owned->best_effort_timestamp : owned->pts;
        if (pts == ffmpeg.AV_NOPTS_VALUE)
        {
            pts = _last is not null ? ToStream(_last.End) : _startPts;
        }

        MediaTime start = ToMedia(pts);
        MediaTime dur = owned->duration > 0 ? MediaTime.FromTimeBase(owned->duration, _timeBase) : MediaTime.FrameDuration(FrameRate);

        AVPixelFormat pf = (AVPixelFormat)owned->format;
        bool full;
        FramePixelFormat format;
        if (IsPicture || FrameConverter.HasAlpha(pf) || FrameConverter.IsRgbFamily(pf))
        {
            if (pf != AVPixelFormat.AV_PIX_FMT_RGBA)
            {
                AVFrame* conv = FrameConverter.Convert(owned, AVPixelFormat.AV_PIX_FMT_RGBA);
                ffmpeg.av_frame_free(&owned);
                owned = conv;
            }

            format = FramePixelFormat.Rgba;
            full = true;
        }
        else if (!FrameConverter.TryMap(pf, out format, out full))
        {
            bool tenBit = ffmpeg.av_pix_fmt_desc_get(pf) is var d && d != null && d->comp[0].depth > 8;
            AVFrame* conv = FrameConverter.Convert(owned, tenBit ? AVPixelFormat.AV_PIX_FMT_YUV420P10LE : AVPixelFormat.AV_PIX_FMT_YUV420P);
            full = owned->color_range == AVColorRange.AVCOL_RANGE_JPEG || pf is AVPixelFormat.AV_PIX_FMT_YUVJ422P or AVPixelFormat.AV_PIX_FMT_YUVJ444P;
            ffmpeg.av_frame_free(&owned);
            owned = conv;
            format = tenBit ? FramePixelFormat.Yuv420P10 : FramePixelFormat.Yuv420P;
        }
        else if (owned->color_range == AVColorRange.AVCOL_RANGE_JPEG)
        {
            full = true;
        }

        FrameColorSpace cs = owned->colorspace switch
        {
            AVColorSpace.AVCOL_SPC_BT709 => FrameColorSpace.Bt709,
            AVColorSpace.AVCOL_SPC_BT2020_NCL or AVColorSpace.AVCOL_SPC_BT2020_CL => FrameColorSpace.Bt2020,
            AVColorSpace.AVCOL_SPC_BT470BG or AVColorSpace.AVCOL_SPC_SMPTE170M or AVColorSpace.AVCOL_SPC_FCC => FrameColorSpace.Bt601,
            _ => owned->height >= 720 ? FrameColorSpace.Bt709 : FrameColorSpace.Bt601,
        };

        return new DecodedFrame(owned, format, start, dur, cs, full)
        {
            Rotation = Rotation,
            FlipHorizontal = FlipHorizontal,
            SampleAspect = SampleAspect,
        };
    }

    public void Dispose()
    {
        _pending?.Dispose();
        _pending = null;
        _last?.Dispose();
        _last = null;
        if (_codec != null)
        {
            AVCodecContext* c = _codec;
            ffmpeg.avcodec_free_context(&c);
            _codec = null;
        }

        if (_packet != null)
        {
            AVPacket* p = _packet;
            ffmpeg.av_packet_free(&p);
            _packet = null;
        }

        if (_frame != null)
        {
            AVFrame* f = _frame;
            ffmpeg.av_frame_free(&f);
            _frame = null;
        }

        if (_swFrame != null)
        {
            AVFrame* f = _swFrame;
            ffmpeg.av_frame_free(&f);
            _swFrame = null;
        }

        _input.Dispose();
    }
}
