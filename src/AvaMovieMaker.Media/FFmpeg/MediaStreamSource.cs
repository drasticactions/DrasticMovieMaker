using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.FFmpeg;

public sealed unsafe class MediaStreamSource : IDisposable
{
    private const int BufferSize = 256 * 1024;

    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly bool _write;
    private GCHandle _self;
    private AVIOContext* _context;

    private MediaStreamSource(Stream stream, bool write, bool ownsStream)
    {
        FFmpegRuntime.EnsureLoaded();
        _stream = stream;
        _write = write;
        _ownsStream = ownsStream;
        _self = GCHandle.Alloc(this);
        byte* buffer = (byte*)ffmpeg.av_malloc(BufferSize);
        if (buffer == null)
        {
            _self.Free();
            throw new OutOfMemoryException("av_malloc for the I/O buffer");
        }

        _context = ffmpeg.avio_alloc_context(
            buffer,
            BufferSize,
            write ? 1 : 0,
            (void*)GCHandle.ToIntPtr(_self),
            new avio_alloc_context_read_packet_func { Pointer = write ? IntPtr.Zero : (IntPtr)(delegate* unmanaged<void*, byte*, int, int>)&Read },
            new avio_alloc_context_write_packet_func { Pointer = write ? (IntPtr)(delegate* unmanaged<void*, byte*, int, int>)&Write : IntPtr.Zero },
            new avio_alloc_context_seek_func { Pointer = stream.CanSeek ? (IntPtr)(delegate* unmanaged<void*, long, int, long>)&Seek : IntPtr.Zero });
        if (_context == null)
        {
            ffmpeg.av_free(buffer);
            _self.Free();
            throw new OutOfMemoryException("avio_alloc_context");
        }

        if (!stream.CanSeek)
        {
            _context->seekable = 0;
        }
    }

    public AVIOContext* Context => _context;

    public Exception? LastError { get; private set; }

    public static MediaStreamSource ForRead(Stream stream, bool ownsStream = true) => new(stream, write: false, ownsStream);

    public static MediaStreamSource ForWrite(Stream stream, bool ownsStream = true) => new(stream, write: true, ownsStream);

    public void Dispose()
    {
        if (_context != null)
        {
            if (_write)
            {
                ffmpeg.avio_flush(_context);
            }

            ffmpeg.av_freep(&_context->buffer);
            AVIOContext* context = _context;
            ffmpeg.avio_context_free(&context);
            _context = null;
        }

        if (_self.IsAllocated)
        {
            _self.Free();
        }

        if (_ownsStream)
        {
            _stream.Dispose();
        }
    }

    private static MediaStreamSource From(void* opaque) => (MediaStreamSource)GCHandle.FromIntPtr((IntPtr)opaque).Target!;

    [UnmanagedCallersOnly]
    private static int Read(void* opaque, byte* buffer, int size)
    {
        MediaStreamSource source = From(opaque);
        try
        {
            int n = source._stream.Read(new Span<byte>(buffer, size));
            return n == 0 ? ffmpeg.AVERROR_EOF : n;
        }
        catch (Exception e)
        {
            source.LastError = e;
            return ffmpeg.AVERROR(Errno.IO);
        }
    }

    [UnmanagedCallersOnly]
    private static int Write(void* opaque, byte* buffer, int size)
    {
        MediaStreamSource source = From(opaque);
        try
        {
            source._stream.Write(new ReadOnlySpan<byte>(buffer, size));
            return size;
        }
        catch (Exception e)
        {
            source.LastError = e;
            return ffmpeg.AVERROR(Errno.IO);
        }
    }

    [UnmanagedCallersOnly]
    private static long Seek(void* opaque, long offset, int whence)
    {
        MediaStreamSource source = From(opaque);
        try
        {
            Stream s = source._stream;
            if ((whence & ffmpeg.AVSEEK_SIZE) != 0)
            {
                return s.Length;
            }

            return (whence & ~ffmpeg.AVSEEK_FORCE) switch
            {
                0 => s.Seek(offset, SeekOrigin.Begin),
                1 => s.Seek(offset, SeekOrigin.Current),
                2 => s.Seek(offset, SeekOrigin.End),
                _ => ffmpeg.AVERROR(Errno.Invalid),
            };
        }
        catch (Exception e)
        {
            source.LastError = e;
            return ffmpeg.AVERROR(Errno.IO);
        }
    }
}
