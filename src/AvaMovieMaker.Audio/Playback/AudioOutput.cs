using AvaMovieMaker.Time;

namespace AvaMovieMaker.Audio.Playback;

public sealed class AudioOutput : IPlaybackClock, IDisposable
{
    private const int Chunk = 960;
    private const int AheadFrames = AudioSystem.SampleRate / 5;
    private const int StartFrames = Chunk * 3;

    private readonly IAudioDevice _device;
    private readonly FloatRing _ring = new(AheadFrames * AudioSystem.Channels * 2);
    private readonly Lock _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private Thread? _feeder;
    private volatile bool _running;
    private volatile bool _disposed;
    private bool _startPending;
    private int _generation;
    private IAudioSource? _source;
    private long _feedPosition;
    private long _startSample;
    private long _played;
    private long _end = long.MaxValue;

    private AudioOutput(IAudioDevice device)
    {
        _device = device;
    }

    public static AudioOutput? Open(string deviceName = "")
    {
        if (!AudioSystem.IsAvailable)
        {
            return null;
        }

        AudioOutput? output = null;
        IAudioDevice? dev = AudioSystem.Backend.OpenPlayback(deviceName, AudioSystem.SampleRate, AudioSystem.Channels,
            span => output?.Pull(span));
        if (dev is null)
        {
            return null;
        }

        output = new AudioOutput(dev);
        return output;
    }

    public bool IsRunning => _running;

    public float Volume { get; set; } = 1f;

    public int LatencyFrames => _device.LatencyFrames;

    public MediaTime Position
    {
        get
        {
            long played = Interlocked.Read(ref _played) - _device.LatencyFrames;
            long sample = _startSample + Math.Max(0, played);
            return MediaTime.FromTimeBase(Math.Min(sample, _end), new Rational(1, AudioSystem.SampleRate));
        }
    }

    public void Play(IAudioSource source, MediaTime from, MediaTime? end = null)
    {
        Stop();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _generation++;
            _source = source;
            _startSample = from.ToTimeBase(new Rational(1, AudioSystem.SampleRate));
            _feedPosition = _startSample;
            _end = end is { } e ? e.ToTimeBase(new Rational(1, AudioSystem.SampleRate)) : long.MaxValue;
            Interlocked.Exchange(ref _played, 0);
            _running = true;
            _startPending = true;
            _feeder ??= StartFeeder();
        }

        _wake.Set();
    }

    public void PlayGrain(IAudioSource source, MediaTime at, MediaTime length) => Play(source, at, at + length);

    public void Stop()
    {
        lock (_gate)
        {
            _generation++;
            _running = false;
            _startPending = false;
        }

        _device.Stop();
        lock (_gate)
        {
            _ring.Clear();
        }
    }

    private Thread StartFeeder()
    {
        var t = new Thread(Feed) { IsBackground = true, Name = "AvaMovieMaker audio feeder", Priority = ThreadPriority.AboveNormal };
        t.Start();
        return t;
    }

    private void Feed()
    {
        float[] buf = new float[Chunk * AudioSystem.Channels];
        while (!_disposed)
        {
            int generation;
            long position, end;
            IAudioSource? source;
            lock (_gate)
            {
                bool room = _ring.Free >= buf.Length && _ring.Count < AheadFrames * AudioSystem.Channels;
                generation = _running && room ? _generation : -1;
                position = _feedPosition;
                end = _end;
                source = _source;
            }

            if (generation < 0)
            {
                if (!_running)
                {
                    _wake.WaitOne();
                }
                else
                {
                    Thread.Sleep(5);
                }

                continue;
            }

            Array.Clear(buf);
            if (position < end)
            {
                source?.Read(position, buf);
                long over = position + Chunk - end;
                if (over > 0)
                {
                    buf.AsSpan((int)((Chunk - over) * AudioSystem.Channels)).Clear();
                }
            }

            bool start = false;
            lock (_gate)
            {
                if (generation != _generation)
                {
                    continue;
                }

                _ring.Write(buf);
                _feedPosition = position + Chunk;
                if (_startPending && (_ring.Count >= StartFrames * AudioSystem.Channels || _ring.Free < buf.Length))
                {
                    _startPending = false;
                    start = true;
                }
            }

            if (start)
            {
                _device.Start();
                bool stopped;
                lock (_gate)
                {
                    stopped = generation != _generation;
                }

                if (stopped)
                {
                    _device.Stop();
                }
            }
        }
    }

    private void Pull(Span<float> output)
    {
        int got = _ring.Read(output);
        if (got < output.Length)
        {
            output[got..].Clear();
        }

        float v = Volume;
        if (v != 1f)
        {
            for (int i = 0; i < got; i++)
            {
                output[i] *= v;
            }
        }

        if (_running)
        {
            Interlocked.Add(ref _played, got / AudioSystem.Channels);
        }
    }

    public void Dispose()
    {
        Stop();
        Thread? feeder;
        lock (_gate)
        {
            _disposed = true;
            feeder = _feeder;
        }

        _wake.Set();
        feeder?.Join();
        _wake.Dispose();
        _device.Dispose();
    }
}
