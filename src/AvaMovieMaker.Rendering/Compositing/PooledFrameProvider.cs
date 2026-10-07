using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Rendering.Compositing;

public sealed class PooledFrameProvider(DecoderPool pool) : IFrameProvider
{
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private static readonly MediaTime PrerollSlack = MediaTime.FromMilliseconds(100);
    private readonly HashSet<string> _prerolling = new(StringComparer.Ordinal);

    public DecoderPool Pool { get; } = pool;

    public bool KeepHardwareFrames { get; set; }

    public DecodedFrame? GetFrame(string path, MediaTime time)
    {
        lock (_failed)
        {
            if (_failed.Contains(path))
            {
                return null;
            }
        }

        try
        {
            using DecoderPool.Lease lease = Pool.Acquire(path, time);
            lease.Decoder.KeepHardwareFrames = KeepHardwareFrames;
            return lease.Decoder.GetFrame(time);
        }
        catch (Exception e) when (e is IOException or Media.FFmpeg.FFmpegException)
        {
            Log.Warn("render", $"No frames from {path}: {e.Message}");
            lock (_failed)
            {
                _failed.Add(path);
            }

            return null;
        }
    }

    public void Preroll(string path, MediaTime time)
    {
        time = MediaTime.Max(MediaTime.Zero, time - PrerollSlack);
        lock (_failed)
        {
            if (_failed.Contains(path) || !_prerolling.Add(path))
            {
                return;
            }
        }

        if (Pool.HasDecoderNear(path, time))
        {
            Done();
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                GetFrame(path, time)?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                Done();
            }
        });

        void Done()
        {
            lock (_failed)
            {
                _prerolling.Remove(path);
            }
        }
    }

    public void Reset()
    {
        lock (_failed)
        {
            _failed.Clear();
        }
    }
}
