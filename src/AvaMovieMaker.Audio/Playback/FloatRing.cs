namespace AvaMovieMaker.Audio.Playback;

internal sealed class FloatRing(int capacity)
{
    private readonly float[] _data = new float[capacity];
    private long _write;
    private long _read;

    public int Capacity => _data.Length;

    public int Count => (int)(Volatile.Read(ref _write) - Volatile.Read(ref _read));

    public int Free => Capacity - Count;

    public int Write(ReadOnlySpan<float> src)
    {
        int n = Math.Min(src.Length, Free);
        long w = Volatile.Read(ref _write);
        for (int i = 0; i < n; i++)
        {
            _data[(w + i) % _data.Length] = src[i];
        }

        Volatile.Write(ref _write, w + n);
        return n;
    }

    public int Read(Span<float> dst)
    {
        int n = Math.Min(dst.Length, Count);
        long r = Volatile.Read(ref _read);
        for (int i = 0; i < n; i++)
        {
            dst[i] = _data[(r + i) % _data.Length];
        }

        Volatile.Write(ref _read, r + n);
        return n;
    }

    public void Clear()
    {
        Volatile.Write(ref _read, Volatile.Read(ref _write));
    }
}
