using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.AutoMovie;

public static class MusicAnalyzer
{
    private const int Rate = 16000;
    private const int Decimate = AudioReader.SampleRate / Rate;
    private const int SilenceFrame = Rate / 40;
    private const float SilenceLevel = 200f / 32768f;
    private const int EnvelopeRate = 200;
    private const int EnvelopeBlock = Rate / EnvelopeRate;

    public static MusicAnalysis Analyze(string path, CancellationToken cancel = default)
    {
        using var reader = new AudioReader(path);
        long frames = reader.DurationSamples / Decimate;
        float[] mono = new float[frames];
        float[] buffer = new float[Decimate * AudioReader.Channels * 4800];
        for (long at = 0; at < frames; at += 4800)
        {
            cancel.ThrowIfCancellationRequested();
            reader.Read(at * Decimate, buffer);
            int n = (int)Math.Min(4800, frames - at);
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int k = 0; k < Decimate * AudioReader.Channels; k++)
                {
                    sum += buffer[i * Decimate * AudioReader.Channels + k];
                }

                mono[at + i] = sum / (Decimate * AudioReader.Channels);
            }
        }

        return Analyze(mono);
    }

    public static MusicAnalysis Analyze(float[] mono)
    {
        (int first, int last) = AudibleSpan(mono);
        MediaTime start = FromRate(first * SilenceFrame, Rate), end = FromRate((last + 1) * SilenceFrame, Rate);
        if (last < first)
        {
            return new MusicAnalysis(MediaTime.Zero, MediaTime.Zero, []);
        }

        double[] onset = Onsets(mono);
        var beats = new List<(int At, double Strength, double Raw)>();
        int window = 5 * EnvelopeRate, hop = 4 * EnvelopeRate, edge = EnvelopeRate / 2;
        for (int w0 = -edge; w0 < onset.Length; w0 += hop)
        {
            int from = Math.Max(0, w0), to = Math.Min(onset.Length, w0 + window);
            if (to - from < 3)
            {
                break;
            }

            double mean = 0;
            for (int i = from; i < to; i++)
            {
                mean += onset[i];
            }

            mean /= to - from;
            var peaks = new List<int>();
            double top = 0;
            for (int i = from; i < to; i++)
            {
                if (onset[i] <= mean || !IsPeak(onset, i, 5))
                {
                    continue;
                }

                peaks.Add(i);
                if (i >= w0 + edge && i < w0 + window - edge)
                {
                    top = Math.Max(top, onset[i]);
                }
            }

            if (top <= 0)
            {
                continue;
            }

            foreach (int i in peaks)
            {
                double s = onset[i] / top;
                if (s > 0.3 && i >= w0 + edge && i < w0 + window - edge)
                {
                    beats.Add((i, Math.Min(1, s), onset[i]));
                }
            }
        }

        var merged = new List<(int At, double Strength, double Raw)>();
        foreach (var b in beats.OrderBy(b => b.At))
        {
            if (merged.Count > 0 && b.At - merged[^1].At < EnvelopeRate * 15 / 100)
            {
                if (b.Raw > merged[^1].Raw)
                {
                    merged[^1] = b;
                }

                continue;
            }

            merged.Add(b);
        }

        double max = merged.Count == 0 ? 1 : merged.Max(b => b.Raw);
        var result = merged
            .Select(b => new Beat(FromRate(b.At, EnvelopeRate), b.Strength, b.Raw / max))
            .Where(b => b.Time >= start && b.Time < end)
            .ToList();
        return new MusicAnalysis(start, end, result);
    }

    private static (int First, int Last) AudibleSpan(float[] mono)
    {
        int count = mono.Length / SilenceFrame;
        bool[] silent = new bool[count];
        for (int f = 0; f < count; f++)
        {
            double sum = 0;
            for (int i = 0; i < SilenceFrame; i++)
            {
                sum += Math.Abs(mono[f * SilenceFrame + i]);
            }

            silent[f] = sum / SilenceFrame < SilenceLevel;
        }

        int SilentIn(int from, int step)
        {
            int n = 0;
            for (int k = 1; k <= 10; k++)
            {
                int j = from + k * step;
                if (j >= 0 && j < count && silent[j])
                {
                    n++;
                }
            }

            return n;
        }

        int first = 0;
        while (first < count && (silent[first] || SilentIn(first, 1) > 2))
        {
            first++;
        }

        int last = count - 1;
        while (last > first && (silent[last] || SilentIn(last, -1) > 8))
        {
            last--;
        }

        return (first, first < count ? last : -1);
    }

    private static double[] Onsets(float[] mono)
    {
        double[] low = Envelope(mono, 120, 0.8), high = Envelope(mono, 3500, 0.8);
        double[] onset = new double[low.Length];
        for (int i = 1; i < onset.Length; i++)
        {
            onset[i] = Math.Max(0, low[i] - low[i - 1]) + Math.Max(0, high[i] - high[i - 1]);
        }

        return onset;
    }

    private static double[] Envelope(float[] mono, double center, double q)
    {
        var stages = new[] { new BandPass(center, q), new BandPass(center, q), new BandPass(center, q) };
        int n = mono.Length / EnvelopeBlock;
        double[] env = new double[n];
        for (int b = 0; b < n; b++)
        {
            double sum = 0;
            for (int i = 0; i < EnvelopeBlock; i++)
            {
                double x = mono[b * EnvelopeBlock + i];
                foreach (BandPass s in stages)
                {
                    x = s.Next(x);
                }

                sum += Math.Abs(x);
            }

            env[b] = sum / EnvelopeBlock;
        }

        double[] smooth = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            int c = 0;
            for (int k = -2; k <= 2; k++)
            {
                if (i + k >= 0 && i + k < n)
                {
                    s += env[i + k];
                    c++;
                }
            }

            smooth[i] = Math.Log(1 + 1000 * s / c);
        }

        return smooth;
    }

    private static bool IsPeak(double[] x, int i, int radius)
    {
        for (int k = Math.Max(0, i - radius); k <= Math.Min(x.Length - 1, i + radius); k++)
        {
            if (x[k] > x[i] || (x[k] == x[i] && k < i))
            {
                return false;
            }
        }

        return true;
    }

    private static MediaTime FromRate(long n, int rate) => MediaTime.FromTimeBase(n, new Rational(1, rate));

    private sealed class BandPass
    {
        private readonly double _b0, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public BandPass(double center, double q)
        {
            double w = 2 * Math.PI * center / Rate, alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
            (_b0, _b2, _a1, _a2) = (alpha / a0, -alpha / a0, -2 * Math.Cos(w) / a0, (1 - alpha) / a0);
        }

        public double Next(double x)
        {
            double y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            (_x2, _x1, _y2, _y1) = (_x1, x, _y1, y);
            return y;
        }
    }
}
