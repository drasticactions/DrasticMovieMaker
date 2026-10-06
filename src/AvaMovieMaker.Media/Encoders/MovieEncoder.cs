using System.Runtime.InteropServices;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Encoders;

public sealed unsafe class MovieEncoder : IDisposable
{
    private readonly EncoderSettings _settings;
    private AVFormatContext* _fmt;
    private AVCodecContext* _video;
    private AVCodecContext* _audio;
    private AVStream* _videoStream;
    private AVStream* _audioStream;
    private AVFrame* _videoFrame;
    private AVFrame* _hwFrame;
    private AVFrame* _audioFrame;
    private AVPacket* _packet;
    private SwsContext* _sws;
    private MediaOutput? _output;
    private AVBufferRef* _hwFrames;
    private long _videoPts;
    private long _audioPts;
    private float[] _audioFifo = new float[48000 * 2];
    private int _audioFifoFrames;
    private bool _finished;
    private bool _headerWritten;

    public MovieEncoder(string path, EncoderSettings settings)
    {
        FFmpegRuntime.EnsureLoaded();
        _settings = settings;
        Path = path;
        try
        {
            Open();
        }
        catch
        {
            Abort();
            throw;
        }
    }

    public string Path { get; }

    public long FramesWritten => _videoPts;

    public static string? MissingEncoder(EncoderSettings s)
    {
        FFmpegRuntime.EnsureLoaded();
        if (!FFmpegRuntime.HasEncoder(s.VideoCodecName))
        {
            return s.VideoCodecName;
        }

        if (s.IncludeAudio && !FFmpegRuntime.HasEncoder(s.AudioCodecName))
        {
            return s.AudioCodecName;
        }

        return null;
    }

    private void Open()
    {
        string format = _settings.Container switch
        {
            ContainerFormat.WebM => "webm",
            ContainerFormat.MkvLossless => "matroska",
            _ => "mp4",
        };
        AVFormatContext* fmt = null;
        FFmpegRuntime.Throw(ffmpeg.avformat_alloc_output_context2(&fmt, null, format, Path), "Create output");
        _fmt = fmt;

        foreach ((string key, string value) in _settings.Metadata)
        {
            if (!string.IsNullOrEmpty(value))
            {
                ffmpeg.av_dict_set(&_fmt->metadata, key, value, 0);
            }
        }

        OpenVideo();
        if (_settings.IncludeAudio)
        {
            OpenAudio();
        }

        if ((_fmt->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
        {
            _output = MediaOutput.Attach(_fmt, Path);
        }

        AVDictionary* opts = null;
        if (_settings.Container == ContainerFormat.Mp4)
        {
            ffmpeg.av_dict_set(&opts, "movflags", "+faststart", 0);
        }

        int r = ffmpeg.avformat_write_header(_fmt, &opts);
        ffmpeg.av_dict_free(&opts);
        FFmpegRuntime.Throw(r, "Write header");
        _headerWritten = true;
        _packet = ffmpeg.av_packet_alloc();
    }

    private void OpenVideo()
    {
        string name = _settings.VideoCodecName;
        AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(name);
        if (codec == null)
        {
            throw new FFmpegException($"The FFmpeg encoder {name} is not available.");
        }

        _video = ffmpeg.avcodec_alloc_context3(codec);
        _video->width = _settings.Width;
        _video->height = _settings.Height;
        _video->time_base = FFmpegUtil.ToAV(_settings.FrameRate.Invert());
        _video->framerate = FFmpegUtil.ToAV(_settings.FrameRate);
        _video->sample_aspect_ratio = FFmpegUtil.ToAV(_settings.SampleAspect);
        _video->gop_size = (int)Math.Round(_settings.FrameRate.ToDouble() * 2);
        _video->color_primaries = AVColorPrimaries.AVCOL_PRI_BT709;
        _video->color_trc = AVColorTransferCharacteristic.AVCOL_TRC_BT709;
        _video->colorspace = AVColorSpace.AVCOL_SPC_BT709;
        _video->color_range = AVColorRange.AVCOL_RANGE_MPEG;
        bool hw = name == "h264_vaapi";
        _video->pix_fmt = hw ? AVPixelFormat.AV_PIX_FMT_VAAPI
            : _settings.Container == ContainerFormat.MkvLossless ? AVPixelFormat.AV_PIX_FMT_YUV444P
            : AVPixelFormat.AV_PIX_FMT_YUV420P;
        if ((_fmt->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
        {
            _video->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
        }

        AVDictionary* opts = null;
        switch (name)
        {
            case "libx264":
                ffmpeg.av_dict_set(&opts, "preset", _settings.Preset, 0);
                if (_settings.VideoBitrate > 0)
                {
                    SetTargetBitrate();
                }
                else
                {
                    ffmpeg.av_dict_set(&opts, "crf", _settings.Crf.ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
                }

                _video->profile = 100;
                break;
            case "libvpx-vp9":
                if (_settings.VideoBitrate > 0)
                {
                    SetTargetBitrate();
                }
                else
                {
                    _video->bit_rate = 0;
                    ffmpeg.av_dict_set(&opts, "crf", _settings.Crf.ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
                }

                ffmpeg.av_dict_set(&opts, "row-mt", "1", 0);
                ffmpeg.av_dict_set(&opts, "cpu-used", _settings.CpuUsed.ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
                ffmpeg.av_dict_set(&opts, "deadline", "good", 0);
                break;
            case "h264_vaapi":
                if (_settings.VideoBitrate > 0)
                {
                    SetTargetBitrate();
                }
                else
                {
                    ffmpeg.av_dict_set(&opts, "qp", Math.Clamp(_settings.Crf + 2, 1, 51).ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
                }

                SetUpVaapiFrames();
                break;
            case "h264_videotoolbox" or "h264_mf":
                _video->bit_rate = _settings.VideoBitrate > 0 ? _settings.VideoBitrate
                    : (long)(_settings.Width * _settings.Height * _settings.FrameRate.ToDouble() * 0.15);
                break;
            case "ffv1":
                ffmpeg.av_dict_set(&opts, "level", "3", 0);
                break;
        }

        int r = ffmpeg.avcodec_open2(_video, codec, &opts);
        ffmpeg.av_dict_free(&opts);
        FFmpegRuntime.Throw(r, $"Open {name}");

        _videoStream = ffmpeg.avformat_new_stream(_fmt, null);
        _videoStream->time_base = _video->time_base;
        _videoStream->avg_frame_rate = _video->framerate;
        _videoStream->sample_aspect_ratio = _video->sample_aspect_ratio;
        FFmpegRuntime.Throw(ffmpeg.avcodec_parameters_from_context(_videoStream->codecpar, _video), "Video parameters");

        _videoFrame = ffmpeg.av_frame_alloc();
        _videoFrame->format = (int)(hw ? AVPixelFormat.AV_PIX_FMT_NV12 : _video->pix_fmt);
        _videoFrame->width = _settings.Width;
        _videoFrame->height = _settings.Height;
        FFmpegRuntime.Throw(ffmpeg.av_frame_get_buffer(_videoFrame, 32), "Video frame");
    }

    private void SetTargetBitrate()
    {
        _video->bit_rate = _settings.VideoBitrate;
        _video->rc_max_rate = _settings.VideoBitrate * 3 / 2;
        _video->rc_buffer_size = (int)Math.Min(int.MaxValue, _settings.VideoBitrate * 2);
    }

    private void SetUpVaapiFrames()
    {
        AVBufferRef* dev = Decoding.HardwareDevice.RefVaapi();
        if (dev == null)
        {
            throw new FFmpegException("Hardware encoding needs a VAAPI device.");
        }

        _hwFrames = ffmpeg.av_hwframe_ctx_alloc(dev);
        ffmpeg.av_buffer_unref(&dev);
        AVHWFramesContext* fc = (AVHWFramesContext*)_hwFrames->data;
        fc->format = AVPixelFormat.AV_PIX_FMT_VAAPI;
        fc->sw_format = AVPixelFormat.AV_PIX_FMT_NV12;
        fc->width = _settings.Width;
        fc->height = _settings.Height;
        fc->initial_pool_size = 20;
        FFmpegRuntime.Throw(ffmpeg.av_hwframe_ctx_init(_hwFrames), "VAAPI frames");
        _video->hw_frames_ctx = ffmpeg.av_buffer_ref(_hwFrames);
        _hwFrame = ffmpeg.av_frame_alloc();
    }

    private void OpenAudio()
    {
        string name = _settings.AudioCodecName;
        AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(name);
        if (codec == null)
        {
            throw new FFmpegException($"The FFmpeg encoder {name} is not available.");
        }

        _audio = ffmpeg.avcodec_alloc_context3(codec);
        _audio->sample_rate = 48000;
        AVChannelLayout layout;
        ffmpeg.av_channel_layout_default(&layout, 2);
        ffmpeg.av_channel_layout_copy(&_audio->ch_layout, &layout);
        _audio->sample_fmt = name switch
        {
            "aac" => AVSampleFormat.AV_SAMPLE_FMT_FLTP,
            "pcm_s16le" => AVSampleFormat.AV_SAMPLE_FMT_S16,
            _ => AVSampleFormat.AV_SAMPLE_FMT_FLT,
        };
        _audio->bit_rate = name == "pcm_s16le" ? 0 : _settings.AudioBitrate;
        _audio->time_base = new AVRational { num = 1, den = 48000 };
        if ((_fmt->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
        {
            _audio->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
        }

        FFmpegRuntime.Throw(ffmpeg.avcodec_open2(_audio, codec, null), $"Open {name}");
        _audioStream = ffmpeg.avformat_new_stream(_fmt, null);
        _audioStream->time_base = _audio->time_base;
        FFmpegRuntime.Throw(ffmpeg.avcodec_parameters_from_context(_audioStream->codecpar, _audio), "Audio parameters");

        _audioFrame = ffmpeg.av_frame_alloc();
        _audioFrame->format = (int)_audio->sample_fmt;
        _audioFrame->nb_samples = _audio->frame_size > 0 ? _audio->frame_size : 1024;
        _audioFrame->sample_rate = 48000;
        ffmpeg.av_channel_layout_copy(&_audioFrame->ch_layout, &_audio->ch_layout);
        FFmpegRuntime.Throw(ffmpeg.av_frame_get_buffer(_audioFrame, 0), "Audio frame");
    }

    public void WriteVideo(IntPtr pixels, int stride, bool bgra)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        AVPixelFormat src = bgra ? AVPixelFormat.AV_PIX_FMT_BGRA : AVPixelFormat.AV_PIX_FMT_RGBA;
        AVPixelFormat dst = (AVPixelFormat)_videoFrame->format;
        _sws = ffmpeg.sws_getCachedContext(_sws, _settings.Width, _settings.Height, src, _settings.Width, _settings.Height, dst,
            (int)SwsFlags.SWS_BICUBIC | (int)SwsFlags.SWS_ACCURATE_RND | (int)SwsFlags.SWS_FULL_CHR_H_INP, null, null, null);
        if (_sws == null)
        {
            throw new FFmpegException("No RGB to YUV conversion.");
        }

        int4 rgb = Coefficients(ffmpeg.SWS_CS_ITU709);
        int4 yuv = Coefficients(ffmpeg.SWS_CS_ITU709);
        ffmpeg.sws_setColorspaceDetails(_sws, rgb, 1, yuv, 0, 0, 1 << 16, 1 << 16);
        FFmpegRuntime.Throw(ffmpeg.av_frame_make_writable(_videoFrame), "Frame writable");
        byte_ptr4 srcData = default;
        int4 srcStride = default;
        srcData[0] = (byte*)pixels;
        srcStride[0] = stride;
        ffmpeg.sws_scale(_sws, srcData, srcStride, 0, _settings.Height, _videoFrame->data, _videoFrame->linesize);
        _videoFrame->pts = _videoPts++;
        _videoFrame->color_range = AVColorRange.AVCOL_RANGE_MPEG;
        _videoFrame->colorspace = AVColorSpace.AVCOL_SPC_BT709;
        _videoFrame->color_primaries = AVColorPrimaries.AVCOL_PRI_BT709;
        _videoFrame->color_trc = AVColorTransferCharacteristic.AVCOL_TRC_BT709;

        if (_hwFrames != null)
        {
            ffmpeg.av_frame_unref(_hwFrame);
            FFmpegRuntime.Throw(ffmpeg.av_hwframe_get_buffer(_hwFrames, _hwFrame, 0), "VAAPI surface");
            FFmpegRuntime.Throw(ffmpeg.av_hwframe_transfer_data(_hwFrame, _videoFrame, 0), "Upload to VAAPI");
            _hwFrame->pts = _videoFrame->pts;
            Send(_video, _videoStream, _hwFrame);
        }
        else
        {
            Send(_video, _videoStream, _videoFrame);
        }
    }

    public void WriteAudio(ReadOnlySpan<float> interleaved)
    {
        if (_audio == null)
        {
            return;
        }

        int frames = interleaved.Length / 2;
        int needed = (_audioFifoFrames + frames) * 2;
        if (_audioFifo.Length < needed)
        {
            Array.Resize(ref _audioFifo, Math.Max(needed, _audioFifo.Length * 2));
        }

        interleaved[..(frames * 2)].CopyTo(_audioFifo.AsSpan(_audioFifoFrames * 2));
        _audioFifoFrames += frames;
        int size = _audioFrame->nb_samples;
        int consumed = 0;
        while (_audioFifoFrames - consumed >= size)
        {
            EncodeAudio(_audioFifo.AsSpan(consumed * 2, size * 2), size);
            consumed += size;
        }

        if (consumed > 0)
        {
            Array.Copy(_audioFifo, consumed * 2, _audioFifo, 0, (_audioFifoFrames - consumed) * 2);
            _audioFifoFrames -= consumed;
        }
    }

    private void EncodeAudio(ReadOnlySpan<float> samples, int frames)
    {
        FFmpegRuntime.Throw(ffmpeg.av_frame_make_writable(_audioFrame), "Audio frame writable");
        _audioFrame->nb_samples = frames;
        switch (_audio->sample_fmt)
        {
            case AVSampleFormat.AV_SAMPLE_FMT_FLTP:
                float* l = (float*)_audioFrame->data[0];
                float* r = (float*)_audioFrame->data[1];
                for (int i = 0; i < frames; i++)
                {
                    l[i] = samples[i * 2];
                    r[i] = samples[i * 2 + 1];
                }

                break;
            case AVSampleFormat.AV_SAMPLE_FMT_S16:
                short* s = (short*)_audioFrame->data[0];
                for (int i = 0; i < frames * 2; i++)
                {
                    s[i] = (short)Math.Clamp(Math.Round(samples[i] * 32767f), short.MinValue, short.MaxValue);
                }

                break;
            default:
                samples[..(frames * 2)].CopyTo(new Span<float>(_audioFrame->data[0], frames * 2));
                break;
        }

        _audioFrame->pts = _audioPts;
        _audioPts += frames;
        Send(_audio, _audioStream, _audioFrame);
    }

    private void Send(AVCodecContext* c, AVStream* stream, AVFrame* frame)
    {
        FFmpegRuntime.Throw(ffmpeg.avcodec_send_frame(c, frame), "Encode");
        while (true)
        {
            int r = ffmpeg.avcodec_receive_packet(c, _packet);
            if (r == ffmpeg.AVERROR(Errno.Again) || r == ffmpeg.AVERROR_EOF)
            {
                return;
            }

            FFmpegRuntime.Throw(r, "Receive packet");
            ffmpeg.av_packet_rescale_ts(_packet, c->time_base, stream->time_base);
            _packet->stream_index = stream->index;
            FFmpegRuntime.Throw(ffmpeg.av_interleaved_write_frame(_fmt, _packet), "Write packet");
        }
    }

    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        if (_audio != null && _audioFifoFrames > 0)
        {
            int count = _audioFifoFrames;
            int size = _audio->frame_size > 0 ? _audio->frame_size : count;
            float[] last = new float[size * 2];
            _audioFifo.AsSpan(0, count * 2).CopyTo(last);
            _audioFifoFrames = 0;
            EncodeAudio(last, size);
        }

        Send(_video, _videoStream, null);
        if (_audio != null)
        {
            Send(_audio, _audioStream, null);
        }

        FFmpegRuntime.Throw(ffmpeg.av_write_trailer(_fmt), "Write trailer");
        _finished = true;
        Close();
    }

    public void Abort()
    {
        _finished = true;
        Close();
        try
        {
            if (FileStore.Current.Exists(Path))
            {
                FileStore.Current.Delete(Path);
            }
        }
        catch (IOException e)
        {
            Log.Warn("encode", $"Could not delete {Path}: {e.Message}");
        }
    }

    private void Close()
    {
        if (_fmt != null)
        {
            ffmpeg.avformat_free_context(_fmt);
            _fmt = null;
        }

        _output?.Dispose();
        _output = null;

        if (_video != null)
        {
            AVCodecContext* c = _video;
            ffmpeg.avcodec_free_context(&c);
            _video = null;
        }

        if (_audio != null)
        {
            AVCodecContext* c = _audio;
            ffmpeg.avcodec_free_context(&c);
            _audio = null;
        }

        FreeFrame(ref _videoFrame);
        FreeFrame(ref _hwFrame);
        FreeFrame(ref _audioFrame);
        if (_packet != null)
        {
            AVPacket* p = _packet;
            ffmpeg.av_packet_free(&p);
            _packet = null;
        }

        if (_sws != null)
        {
            ffmpeg.sws_freeContext(_sws);
            _sws = null;
        }

        if (_hwFrames != null)
        {
            AVBufferRef* b = _hwFrames;
            ffmpeg.av_buffer_unref(&b);
            _hwFrames = null;
        }
    }

    private static void FreeFrame(ref AVFrame* f)
    {
        if (f != null)
        {
            AVFrame* x = f;
            ffmpeg.av_frame_free(&x);
            f = null;
        }
    }

    private static int4 Coefficients(int colorspace)
    {
        int* c = ffmpeg.sws_getCoefficients(colorspace);
        int4 r = default;
        for (uint i = 0; i < 4; i++)
        {
            r[i] = c[i];
        }

        return r;
    }

    public void Dispose()
    {
        if (!_finished)
        {
            if (_headerWritten)
            {
                Abort();
            }
            else
            {
                Close();
            }
        }
    }
}
