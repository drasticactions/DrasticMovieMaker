using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Decoding;

public sealed unsafe class AudioReader : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    private const long ForwardWindow = SampleRate * 2;

    private readonly InputFile _input;
    private readonly int _stream;
    private readonly Rational _timeBase;
    private readonly long _startPts;
    private AVCodecContext* _codec;
    private SwrContext* _swr;
    private AVPacket* _packet;
    private AVFrame* _frame;
    private float[] _buf = new float[SampleRate * Channels];
    private int _bufCount;
    private long _bufStart = -1;
    private bool _eof;
    private bool _demuxEof;

    public AudioReader(string path)
    {
        Path = path;
        _input = new InputFile(path);
        try
        {
            _stream = _input.FindBest(AVMediaType.AVMEDIA_TYPE_AUDIO);
            if (_stream < 0)
            {
                throw new FFmpegException($"{System.IO.Path.GetFileName(path)} has no audio stream.");
            }

            for (int i = 0; i < _input.StreamCount; i++)
            {
                if (i != _stream)
                {
                    _input.Stream(i)->discard = AVDiscard.AVDISCARD_ALL;
                }
            }

            AVStream* s = _input.Stream(_stream);
            _timeBase = FFmpegUtil.ToRational(s->time_base);
            _startPts = s->start_time == ffmpeg.AV_NOPTS_VALUE ? 0 : s->start_time;
            Duration = s->duration > 0 ? MediaTime.FromTimeBase(s->duration, _timeBase)
                : MediaTime.FromTimeBase(Math.Max(0, _input.Context->duration), new Rational(1, ffmpeg.AV_TIME_BASE));

            AVCodec* dec = ffmpeg.avcodec_find_decoder(s->codecpar->codec_id);
            if (dec == null)
            {
                throw new FFmpegException($"No decoder for {FFmpegUtil.CodecName(s->codecpar->codec_id)}.");
            }

            _codec = ffmpeg.avcodec_alloc_context3(dec);
            FFmpegRuntime.Throw(ffmpeg.avcodec_parameters_to_context(_codec, s->codecpar), "Audio parameters");
            _codec->pkt_timebase = s->time_base;
            FFmpegRuntime.Throw(ffmpeg.avcodec_open2(_codec, dec, null), "Open audio decoder");
            _packet = ffmpeg.av_packet_alloc();
            _frame = ffmpeg.av_frame_alloc();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string Path { get; }

    public MediaTime Duration { get; }

    public long DurationSamples => Duration.ToTimeBase(new Rational(1, SampleRate));

    public static long ToSample(MediaTime t) => t.ToTimeBase(new Rational(1, SampleRate));

    public static MediaTime FromSample(long sample) => MediaTime.FromTimeBase(sample, new Rational(1, SampleRate));

    public int Read(MediaTime time, Span<float> dest) => Read(ToSample(time), dest);

    public int Read(long position, Span<float> dest)
    {
        int frames = dest.Length / Channels;
        dest.Clear();
        if (position < 0)
        {
            int skip = (int)Math.Min(frames, -position);
            int n = Read(0, dest[(skip * Channels)..]);
            return skip + n;
        }

        if (_bufStart < 0 || position < _bufStart || position > _bufStart + _bufCount + ForwardWindow)
        {
            Seek(position);
        }

        long want = position + frames;
        while (!_eof && (_bufStart < 0 || _bufStart + _bufCount < want))
        {
            DecodeMore();
            if (_bufStart >= 0 && _bufStart > position)
            {
                break;
            }
        }

        if (_bufStart < 0)
        {
            return 0;
        }

        long from = Math.Max(position, _bufStart);
        long to = Math.Min(want, _bufStart + _bufCount);
        if (to > from)
        {
            int srcOff = (int)(from - _bufStart) * Channels;
            int dstOff = (int)(from - position) * Channels;
            int count = (int)(to - from) * Channels;
            _buf.AsSpan(srcOff, count).CopyTo(dest.Slice(dstOff, count));
        }

        long drop = Math.Clamp(to - _bufStart, 0, _bufCount);
        if (drop > 0)
        {
            int keep = _bufCount - (int)drop;
            Array.Copy(_buf, (int)drop * Channels, _buf, 0, keep * Channels);
            _bufCount = keep;
            _bufStart += drop;
        }

        return frames;
    }

    private void Seek(long position)
    {
        _bufCount = 0;
        _bufStart = -1;
        _eof = false;
        _demuxEof = false;
        MediaTime t = FromSample(position);
        long ts = t.ToTimeBase(_timeBase) + _startPts;
        if (ffmpeg.av_seek_frame(_input.Context, _stream, ts, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
        {
            ffmpeg.av_seek_frame(_input.Context, _stream, _startPts, ffmpeg.AVSEEK_FLAG_BACKWARD | ffmpeg.AVSEEK_FLAG_ANY);
        }

        ffmpeg.avcodec_flush_buffers(_codec);
        if (_swr != null)
        {
            SwrContext* s = _swr;
            ffmpeg.swr_free(&s);
            _swr = null;
        }
    }

    private void DecodeMore()
    {
        while (true)
        {
            int r = ffmpeg.avcodec_receive_frame(_codec, _frame);
            if (r == 0)
            {
                Append(_frame);
                ffmpeg.av_frame_unref(_frame);
                return;
            }

            if (r == ffmpeg.AVERROR_EOF || _demuxEof && r == ffmpeg.AVERROR(Errno.Again))
            {
                Drain();
                _eof = true;
                return;
            }

            if (r != ffmpeg.AVERROR(Errno.Again))
            {
                FFmpegRuntime.Throw(r, "Decode audio");
            }

            while (true)
            {
                if (ffmpeg.av_read_frame(_input.Context, _packet) < 0)
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

                ffmpeg.avcodec_send_packet(_codec, _packet);
                ffmpeg.av_packet_unref(_packet);
                break;
            }
        }
    }

    private void EnsureSwr(AVFrame* f)
    {
        if (_swr != null)
        {
            return;
        }

        AVChannelLayout outLayout;
        ffmpeg.av_channel_layout_default(&outLayout, Channels);
        AVChannelLayout inLayout = f->ch_layout;
        if (inLayout.nb_channels == 0 || inLayout.order == AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC)
        {
            ffmpeg.av_channel_layout_default(&inLayout, Math.Max(1, f->ch_layout.nb_channels));
        }

        SwrContext* swr = null;
        FFmpegRuntime.Throw(ffmpeg.swr_alloc_set_opts2(&swr, &outLayout, AVSampleFormat.AV_SAMPLE_FMT_FLT, SampleRate,
            &inLayout, (AVSampleFormat)f->format, f->sample_rate, 0, null), "Resampler options");
        if (inLayout.nb_channels == 1)
        {
            double* matrix = stackalloc double[2] { 1.0, 1.0 };
            FFmpegRuntime.Throw(ffmpeg.swr_set_matrix(swr, matrix, 1), "Mono matrix");
        }

        FFmpegRuntime.Throw(ffmpeg.swr_init(swr), "Resampler init");
        _swr = swr;
    }

    private void Append(AVFrame* f)
    {
        EnsureSwr(f);
        if (_bufStart < 0)
        {
            long pts = f->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE ? f->best_effort_timestamp : f->pts;
            if (pts == ffmpeg.AV_NOPTS_VALUE)
            {
                pts = _startPts;
            }

            _bufStart = Math.Max(0, ToSample(MediaTime.FromTimeBase(pts - _startPts, _timeBase)));
        }

        int maxOut = (int)ffmpeg.av_rescale_rnd(ffmpeg.swr_get_delay(_swr, f->sample_rate) + f->nb_samples, SampleRate, f->sample_rate, AVRounding.AV_ROUND_UP) + 32;
        Convert(f->extended_data, f->nb_samples, maxOut);
    }

    private void Drain()
    {
        if (_swr == null)
        {
            return;
        }

        int maxOut = (int)ffmpeg.swr_get_delay(_swr, SampleRate) + 32;
        if (maxOut > 32)
        {
            Convert(null, 0, maxOut);
        }
    }

    private void Convert(byte** input, int inSamples, int maxOut)
    {
        int needed = (_bufCount + maxOut) * Channels;
        if (_buf.Length < needed)
        {
            Array.Resize(ref _buf, Math.Max(needed, _buf.Length * 2));
        }

        fixed (float* p = &_buf[_bufCount * Channels])
        {
            byte* outPtr = (byte*)p;
            int got = ffmpeg.swr_convert(_swr, &outPtr, maxOut, input, inSamples);
            if (got > 0)
            {
                _bufCount += got;
            }
        }
    }

    public void Dispose()
    {
        if (_swr != null)
        {
            SwrContext* s = _swr;
            ffmpeg.swr_free(&s);
            _swr = null;
        }

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

        _input.Dispose();
    }
}
