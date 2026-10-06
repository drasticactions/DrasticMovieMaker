using System.Runtime.InteropServices;
using AvaMovieMaker.IO;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.FFmpeg;

internal sealed unsafe class InputFile : IDisposable
{
    private AVFormatContext* _ctx;
    private MediaStreamSource? _source;

    public InputFile(string path)
    {
        FFmpegRuntime.EnsureLoaded();
        Path = path;
        _source = MediaStreamSource.ForRead(FileStore.Current.OpenRead(path));
        AVFormatContext* ctx = ffmpeg.avformat_alloc_context();
        ctx->pb = _source.Context;
        ctx->flags |= ffmpeg.AVFMT_FLAG_CUSTOM_IO;
        int r = ffmpeg.avformat_open_input(&ctx, path, null, null);
        if (r < 0)
        {
            _source.Dispose();
            _source = null;
            FFmpegRuntime.Throw(r, $"Open {path}");
        }

        _ctx = ctx;
        r = ffmpeg.avformat_find_stream_info(_ctx, null);
        if (r < 0)
        {
            Dispose();
            FFmpegRuntime.Throw(r, $"Read stream info of {path}");
        }
    }

    public string Path { get; }

    public AVFormatContext* Context => _ctx;

    public string FormatName => _ctx->iformat == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)_ctx->iformat->name) ?? string.Empty;

    public int StreamCount => (int)_ctx->nb_streams;

    public AVStream* Stream(int index) => _ctx->streams[index];

    public int FindBest(AVMediaType type)
    {
        int r = ffmpeg.av_find_best_stream(_ctx, type, -1, -1, null, 0);
        if (r < 0)
        {
            return -1;
        }

        if (type == AVMediaType.AVMEDIA_TYPE_VIDEO && (Stream(r)->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != 0)
        {
            for (int i = 0; i < StreamCount; i++)
            {
                AVStream* s = Stream(i);
                if (s->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO && (s->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        return r;
    }

    public string? Tag(string key)
    {
        AVDictionaryEntry* e = ffmpeg.av_dict_get(_ctx->metadata, key, null, 0);
        return e == null ? null : Marshal.PtrToStringUTF8((IntPtr)e->value);
    }

    public void Dispose()
    {
        if (_ctx != null)
        {
            AVFormatContext* ctx = _ctx;
            ffmpeg.avformat_close_input(&ctx);
            _ctx = null;
        }

        _source?.Dispose();
        _source = null;
    }
}
