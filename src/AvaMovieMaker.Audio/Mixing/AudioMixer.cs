using AvaMovieMaker.Audio.Playback;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.Decoding;

namespace AvaMovieMaker.Audio.Mixing;

public sealed class AudioMixer : IAudioSource, IDisposable
{
    private readonly Dictionary<string, AudioReader> _readers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private float[] _tmp = new float[4096];
    private float[] _src = new float[4096];
    private AudioPlan _plan;

    public AudioMixer(AudioPlan plan)
    {
        _plan = plan;
    }

    public AudioPlan Plan
    {
        get => Volatile.Read(ref _plan);
        set
        {
            Volatile.Write(ref _plan, value);
            lock (_gate)
            {
                var used = new HashSet<string>(value.Inputs.Select(i => i.Path), StringComparer.Ordinal);
                foreach (string path in _readers.Keys.Where(p => !used.Contains(p)).ToList())
                {
                    _readers.Remove(path, out AudioReader? r);
                    r?.Dispose();
                }
            }
        }
    }

    public IReadOnlyList<string> OpenFiles
    {
        get
        {
            lock (_gate)
            {
                return [.. _readers.Keys];
            }
        }
    }

    public void Read(long position, Span<float> dest)
    {
        lock (_gate)
        {
            ReadLocked(position, dest);
        }
    }

    private void ReadLocked(long position, Span<float> dest)
    {
        dest.Clear();
        AudioPlan plan = Plan;
        int frames = dest.Length / 2;
        long end = position + frames;
        (float videoGain, float musicGain) = LevelsLaw.Gains(plan.Levels);
        foreach (AudioInput input in plan.Inputs)
        {
            if (input.TimelineEnd <= position || input.TimelineStart >= end || input.Mute)
            {
                continue;
            }

            float groupGain = input.Group == AudioGroup.Video ? videoGain : musicGain;
            if (groupGain <= 0)
            {
                continue;
            }

            long from = Math.Max(position, input.TimelineStart);
            long to = Math.Min(end, input.TimelineEnd);
            int n = (int)(to - from);
            if (!ReadInput(input, from, n))
            {
                continue;
            }

            int o = (int)(from - position) * 2;
            for (int i = 0; i < n; i++)
            {
                float g = input.GainAt(from + i) * groupGain;
                dest[o + i * 2] += _tmp[i * 2] * g;
                dest[o + i * 2 + 1] += _tmp[i * 2 + 1] * g;
            }
        }

        SoftLimiter.Apply(dest);
    }

    private bool ReadInput(AudioInput input, long from, int n)
    {
        AudioReader? reader = Reader(input.Path);
        if (reader is null)
        {
            return false;
        }

        if (_tmp.Length < n * 2)
        {
            _tmp = new float[n * 2];
        }

        double srcPos = input.SourceStart + (from - input.TimelineStart) * input.Speed;
        if (Math.Abs(input.Speed - 1.0) < 1e-9)
        {
            reader.Read((long)Math.Round(srcPos), _tmp.AsSpan(0, n * 2));
            return true;
        }

        long s0 = (long)Math.Floor(srcPos);
        int span = (int)Math.Ceiling(n * input.Speed) + 2;
        if (_src.Length < span * 2)
        {
            _src = new float[span * 2];
        }

        reader.Read(s0, _src.AsSpan(0, span * 2));
        for (int i = 0; i < n; i++)
        {
            double p = srcPos + i * input.Speed - s0;
            int k = (int)p;
            float f = (float)(p - k);
            int k1 = Math.Min(k + 1, span - 1);
            _tmp[i * 2] = _src[k * 2] * (1 - f) + _src[k1 * 2] * f;
            _tmp[i * 2 + 1] = _src[k * 2 + 1] * (1 - f) + _src[k1 * 2 + 1] * f;
        }

        return true;
    }

    private AudioReader? Reader(string path)
    {
        if (_readers.TryGetValue(path, out AudioReader? r))
        {
            return r;
        }

        if (_failed.Contains(path))
        {
            return null;
        }

        try
        {
            r = new AudioReader(path);
            _readers[path] = r;
            return r;
        }
        catch (Exception e)
        {
            Log.Warn("audio", $"No audio from {path}: {e.Message}");
            _failed.Add(path);
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (AudioReader r in _readers.Values)
            {
                r.Dispose();
            }

            _readers.Clear();
        }
    }
}
