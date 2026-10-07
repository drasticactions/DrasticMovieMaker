using AvaMovieMaker.Time;

namespace AvaMovieMaker.Media.Decoding;

public sealed class DecoderPool : IDisposable
{
    private const int MaxPerPath = 2;
    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = [];
    private long _clock;
    private bool _disposed;

    public DecoderPool(int capacity = 8, bool allowHardware = true)
    {
        Capacity = capacity;
        AllowHardware = allowHardware;
    }

    public int Capacity { get; }

    public bool AllowHardware { get; set; }

    public int OpenCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public Lease Acquire(string path, MediaTime? time = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Entry? idle = null;
            int open = 0;
            foreach (Entry e in _entries)
            {
                if (e.Evicted || !string.Equals(e.Decoder.Path, path, StringComparison.Ordinal))
                {
                    continue;
                }

                open++;
                if (e.InUse)
                {
                    continue;
                }

                if (time is not { } t || e.Decoder.CanReach(t))
                {
                    return Take(e);
                }

                if (idle is null || e.LastUse < idle.LastUse)
                {
                    idle = e;
                }
            }

            if (idle is not null && open >= MaxPerPath)
            {
                return Take(idle);
            }
        }

        var decoder = new VideoDecoder(path, AllowHardware);
        lock (_gate)
        {
            var entry = new Entry(decoder) { InUse = true, LastUse = ++_clock };
            _entries.Add(entry);
            Trim();
            return new Lease(this, entry);
        }
    }

    public bool HasDecoderNear(string path, MediaTime time)
    {
        lock (_gate)
        {
            foreach (Entry e in _entries)
            {
                if (!e.InUse && !e.Evicted && string.Equals(e.Decoder.Path, path, StringComparison.Ordinal) && e.Decoder.CanReach(time))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private Lease Take(Entry e)
    {
        e.InUse = true;
        e.LastUse = ++_clock;
        return new Lease(this, e);
    }

    private void Release(Entry e)
    {
        lock (_gate)
        {
            e.InUse = false;
            e.LastUse = ++_clock;
            if (_disposed || e.Evicted)
            {
                _entries.Remove(e);
                e.Decoder.Dispose();
                return;
            }

            Trim();
        }
    }

    private void Trim()
    {
        while (_entries.Count > Capacity)
        {
            Entry? oldest = null;
            foreach (Entry e in _entries)
            {
                if (!e.InUse && (oldest is null || e.LastUse < oldest.LastUse))
                {
                    oldest = e;
                }
            }

            if (oldest is null)
            {
                return;
            }

            _entries.Remove(oldest);
            oldest.Decoder.Dispose();
        }
    }

    public void Evict(string path)
    {
        lock (_gate)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                if (!string.Equals(e.Decoder.Path, path, StringComparison.Ordinal))
                {
                    continue;
                }

                if (e.InUse)
                {
                    e.Evicted = true;
                }
                else
                {
                    _entries.RemoveAt(i);
                    e.Decoder.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_entries[i].InUse)
                {
                    _entries[i].Decoder.Dispose();
                    _entries.RemoveAt(i);
                }
            }
        }
    }

    private sealed class Entry(VideoDecoder decoder)
    {
        public VideoDecoder Decoder { get; } = decoder;

        public bool InUse { get; set; }

        public bool Evicted { get; set; }

        public long LastUse { get; set; }
    }

    public readonly struct Lease : IDisposable
    {
        private readonly DecoderPool _pool;
        private readonly Entry _entry;

        internal Lease(DecoderPool pool, object entry)
        {
            _pool = pool;
            _entry = (Entry)entry;
        }

        public VideoDecoder Decoder => _entry.Decoder;

        public void Dispose() => _pool?.Release(_entry);
    }
}
