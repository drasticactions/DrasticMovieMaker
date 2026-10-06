using AvaMovieMaker.Media.FFmpeg;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Encoders;

public sealed unsafe class AudioFileWriter : IDisposable
{
    private readonly int _channels;
    private AVFormatContext* _fmt;
    private AVCodecContext* _codec;
    private AVStream* _stream;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private readonly List<float> _fifo = [];
    private long _pts;
    private bool _finished;
    private MediaOutput? _output;

    public AudioFileWriter(string path, int channels = 1, int sampleRate = 48000)
    {
        FFmpegRuntime.EnsureLoaded();
        Path = path;
        _channels = channels;
        AVFormatContext* fmt = null;
        FFmpegRuntime.Throw(ffmpeg.avformat_alloc_output_context2(&fmt, null, "flac", path), "Create FLAC output");
        _fmt = fmt;
        AVCodec* codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_FLAC);
        if (codec == null)
        {
            throw new FFmpegException("The FLAC encoder is not available.");
        }

        _codec = ffmpeg.avcodec_alloc_context3(codec);
        _codec->sample_rate = sampleRate;
        _codec->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S32;
        _codec->bits_per_raw_sample = 24;
        AVChannelLayout layout;
        ffmpeg.av_channel_layout_default(&layout, channels);
        ffmpeg.av_channel_layout_copy(&_codec->ch_layout, &layout);
        _codec->time_base = new AVRational { num = 1, den = sampleRate };
        FFmpegRuntime.Throw(ffmpeg.avcodec_open2(_codec, codec, null), "Open FLAC encoder");
        _stream = ffmpeg.avformat_new_stream(_fmt, null);
        _stream->time_base = _codec->time_base;
        FFmpegRuntime.Throw(ffmpeg.avcodec_parameters_from_context(_stream->codecpar, _codec), "FLAC parameters");
        _output = MediaOutput.Attach(_fmt, path);
        FFmpegRuntime.Throw(ffmpeg.avformat_write_header(_fmt, null), "FLAC header");
        _frame = ffmpeg.av_frame_alloc();
        _frame->format = (int)AVSampleFormat.AV_SAMPLE_FMT_S32;
        _frame->nb_samples = _codec->frame_size > 0 ? _codec->frame_size : 4608;
        _frame->sample_rate = sampleRate;
        ffmpeg.av_channel_layout_copy(&_frame->ch_layout, &_codec->ch_layout);
        FFmpegRuntime.Throw(ffmpeg.av_frame_get_buffer(_frame, 0), "FLAC frame");
        _packet = ffmpeg.av_packet_alloc();
    }

    public string Path { get; }

    public long FramesWritten { get; private set; }

    public void Write(ReadOnlySpan<float> interleaved)
    {
        foreach (float v in interleaved)
        {
            _fifo.Add(v);
        }

        FramesWritten += interleaved.Length / _channels;
        int size = _frame->nb_samples;
        while (_fifo.Count >= size * _channels)
        {
            EncodeFrom(size);
        }
    }

    private void EncodeFrom(int frames)
    {
        FFmpegRuntime.Throw(ffmpeg.av_frame_make_writable(_frame), "FLAC frame writable");
        int* dst = (int*)_frame->data[0];
        for (int i = 0; i < frames * _channels; i++)
        {
            double v = Math.Clamp(_fifo[i], -1f, 1f) * 2147483392.0;
            dst[i] = (int)v;
        }

        _fifo.RemoveRange(0, frames * _channels);
        _frame->nb_samples = frames;
        _frame->pts = _pts;
        _pts += frames;
        Send(_frame);
    }

    private void Send(AVFrame* f)
    {
        FFmpegRuntime.Throw(ffmpeg.avcodec_send_frame(_codec, f), "FLAC encode");
        while (ffmpeg.avcodec_receive_packet(_codec, _packet) == 0)
        {
            ffmpeg.av_packet_rescale_ts(_packet, _codec->time_base, _stream->time_base);
            _packet->stream_index = _stream->index;
            ffmpeg.av_interleaved_write_frame(_fmt, _packet);
        }
    }

    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        if (_fifo.Count > 0)
        {
            EncodeFrom(_fifo.Count / _channels);
        }

        Send(null);
        ffmpeg.av_write_trailer(_fmt);
        _finished = true;
        Dispose();
    }

    public void Dispose()
    {
        if (_fmt != null)
        {
            if (!_finished)
            {
                ffmpeg.av_write_trailer(_fmt);
                _finished = true;
            }

            ffmpeg.avformat_free_context(_fmt);
            _fmt = null;
        }

        _output?.Dispose();
        _output = null;

        if (_codec != null)
        {
            AVCodecContext* c = _codec;
            ffmpeg.avcodec_free_context(&c);
            _codec = null;
        }

        if (_frame != null)
        {
            AVFrame* f = _frame;
            ffmpeg.av_frame_free(&f);
            _frame = null;
        }

        if (_packet != null)
        {
            AVPacket* p = _packet;
            ffmpeg.av_packet_free(&p);
            _packet = null;
        }
    }
}
