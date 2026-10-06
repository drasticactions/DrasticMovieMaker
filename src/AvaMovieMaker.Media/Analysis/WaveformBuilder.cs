using AvaMovieMaker.IO;
using AvaMovieMaker.Media.Decoding;

namespace AvaMovieMaker.Media.Analysis;

public static class WaveformBuilder
{
    public static Task<Waveform> GetAsync(string path, CancellationToken cancel = default) => Task.Run(() => Get(path, cancel), cancel);

    public static Waveform Get(string path, CancellationToken cancel = default)
    {
        string cache = MediaCache.FileFor(path, "waveform.bin");
        if (FileStore.Current.Exists(cache) && Waveform.Load(cache) is { } cached)
        {
            return cached;
        }

        Waveform w = Build(path, cancel);
        try
        {
            w.Save(cache);
        }
        catch (IOException)
        {
        }

        return w;
    }

    public static Waveform Build(string path, CancellationToken cancel = default)
    {
        using var reader = new AudioReader(path);
        long total = reader.DurationSamples;
        int buckets = (int)Math.Max(1, (total + Waveform.SamplesPerBucket - 1) / Waveform.SamplesPerBucket);
        float[] min = new float[buckets];
        float[] max = new float[buckets];
        const int chunkBuckets = 100;
        float[] buf = new float[Waveform.SamplesPerBucket * chunkBuckets * AudioReader.Channels];
        for (int b = 0; b < buckets; b += chunkBuckets)
        {
            cancel.ThrowIfCancellationRequested();
            reader.Read((long)b * Waveform.SamplesPerBucket, buf);
            int n = Math.Min(chunkBuckets, buckets - b);
            for (int i = 0; i < n; i++)
            {
                float lo = 0;
                float hi = 0;
                int baseIndex = i * Waveform.SamplesPerBucket * 2;
                for (int s = 0; s < Waveform.SamplesPerBucket; s++)
                {
                    float m = (buf[baseIndex + s * 2] + buf[baseIndex + s * 2 + 1]) * 0.5f;
                    lo = Math.Min(lo, m);
                    hi = Math.Max(hi, m);
                }

                min[b + i] = lo;
                max[b + i] = hi;
            }
        }

        return new Waveform(min, max);
    }
}
