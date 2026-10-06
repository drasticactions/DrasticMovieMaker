using System.Runtime.InteropServices;
using AvaMovieMaker.IO;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.FFmpeg;

internal sealed unsafe class MediaOutput : IDisposable
{
    private readonly MediaStreamSource _main;
    private readonly Dictionary<IntPtr, MediaStreamSource> _opened = [];
    private GCHandle _self;

    private MediaOutput(AVFormatContext* fmt, string reference)
    {
        _main = MediaStreamSource.ForWrite(FileStore.Current.Create(reference));
        _self = GCHandle.Alloc(this);
        fmt->pb = _main.Context;
        fmt->flags |= ffmpeg.AVFMT_FLAG_CUSTOM_IO;
        fmt->opaque = (void*)GCHandle.ToIntPtr(_self);
        fmt->io_open = new AVFormatContext_io_open_func { Pointer = (IntPtr)(delegate* unmanaged<AVFormatContext*, AVIOContext**, byte*, int, AVDictionary**, int>)&IoOpen };
        fmt->io_close2 = new AVFormatContext_io_close2_func { Pointer = (IntPtr)(delegate* unmanaged<AVFormatContext*, AVIOContext*, int>)&IoClose };
    }

    public static MediaOutput Attach(AVFormatContext* fmt, string reference) => new(fmt, reference);

    public void Dispose()
    {
        foreach (MediaStreamSource source in _opened.Values)
        {
            source.Dispose();
        }

        _opened.Clear();
        _main.Dispose();
        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }

    [UnmanagedCallersOnly]
    private static int IoOpen(AVFormatContext* s, AVIOContext** pb, byte* url, int flags, AVDictionary** options)
    {
        var output = (MediaOutput)GCHandle.FromIntPtr((IntPtr)s->opaque).Target!;
        try
        {
            string reference = Marshal.PtrToStringUTF8((IntPtr)url) ?? string.Empty;
            MediaStreamSource source = (flags & ffmpeg.AVIO_FLAG_WRITE) != 0
                ? MediaStreamSource.ForWrite(FileStore.Current.Create(reference))
                : MediaStreamSource.ForRead(FileStore.Current.OpenRead(reference));
            output._opened[(IntPtr)source.Context] = source;
            *pb = source.Context;
            return 0;
        }
        catch (Exception)
        {
            return ffmpeg.AVERROR(Errno.NoEntry);
        }
    }

    [UnmanagedCallersOnly]
    private static int IoClose(AVFormatContext* s, AVIOContext* pb)
    {
        var output = (MediaOutput)GCHandle.FromIntPtr((IntPtr)s->opaque).Target!;
        if (output._opened.Remove((IntPtr)pb, out MediaStreamSource? source))
        {
            source.Dispose();
        }

        return 0;
    }
}
