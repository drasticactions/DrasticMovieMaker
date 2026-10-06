using AvaMovieMaker.IO;
namespace AvaMovieMaker.Media.Analysis;

public sealed class Waveform
{
    public const int SamplesPerBucket = 480;
    public const double BucketSeconds = 0.01;

    private readonly List<(float[] Min, float[] Max)> _levels = [];

    public Waveform(float[] min, float[] max)
    {
        if (min.Length != max.Length)
        {
            throw new ArgumentException("Min and max lengths differ.");
        }

        _levels.Add((min, max));
    }

    public int BucketCount => _levels[0].Min.Length;

    public ReadOnlySpan<float> Min => _levels[0].Min;

    public ReadOnlySpan<float> Max => _levels[0].Max;

    public (float[] Min, float[] Max, double BucketSeconds) LevelFor(double secondsPerPixel)
    {
        int level = 0;
        double bucket = BucketSeconds;
        while (bucket * 2 <= secondsPerPixel && Level(level + 1) is not null)
        {
            level++;
            bucket *= 2;
        }

        (float[] min, float[] max) = Level(level)!.Value;
        return (min, max, bucket);
    }

    private (float[] Min, float[] Max)? Level(int index)
    {
        while (_levels.Count <= index)
        {
            (float[] min, float[] max) = _levels[^1];
            if (min.Length < 2)
            {
                return null;
            }

            int n = (min.Length + 1) / 2;
            float[] nmin = new float[n];
            float[] nmax = new float[n];
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                int b = Math.Min(a + 1, min.Length - 1);
                nmin[i] = Math.Min(min[a], min[b]);
                nmax[i] = Math.Max(max[a], max[b]);
            }

            _levels.Add((nmin, nmax));
        }

        return _levels[index];
    }

    public void Save(string path)
    {
        using var w = new BinaryWriter(FileStore.Current.Create(path));
        w.Write(0x4D4D5746);
        w.Write(BucketCount);
        foreach (float v in _levels[0].Min)
        {
            w.Write(v);
        }

        foreach (float v in _levels[0].Max)
        {
            w.Write(v);
        }
    }

    public static Waveform? Load(string path)
    {
        try
        {
            using var r = new BinaryReader(FileStore.Current.OpenRead(path));
            if (r.ReadInt32() != 0x4D4D5746)
            {
                return null;
            }

            int n = r.ReadInt32();
            if (n < 0 || n > 100_000_000)
            {
                return null;
            }

            float[] min = new float[n];
            float[] max = new float[n];
            for (int i = 0; i < n; i++)
            {
                min[i] = r.ReadSingle();
            }

            for (int i = 0; i < n; i++)
            {
                max[i] = r.ReadSingle();
            }

            return new Waveform(min, max);
        }
        catch (Exception e) when (e is IOException or EndOfStreamException)
        {
            return null;
        }
    }
}
