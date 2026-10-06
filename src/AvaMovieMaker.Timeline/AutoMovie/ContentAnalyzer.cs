using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.AutoMovie;

public static class ContentAnalyzer
{
    public const double BadFrame = 0.35;

    private const int Bins = 64;
    private const double CutDifference = 0.22;
    private const double CutRatio = 2.5;
    private const int MeanFrames = 120;
    private const int ThumbWidth = 64, ThumbHeight = 48;
    private const int MinSubshotFrames = 40;
    private const int MotionSpan = 4;

    public static double Quality(ReadOnlySpan<int> histogram)
    {
        long n = 0;
        foreach (int c in histogram)
        {
            n += c;
        }

        if (n == 0)
        {
            return 0;
        }

        double entropy = 0, mean = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            if (histogram[i] == 0)
            {
                continue;
            }

            double p = (double)histogram[i] / n;
            entropy -= p * Math.Log(p);
            mean += p * i;
        }

        double variance = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            variance += (double)histogram[i] / n * (i - mean) * (i - mean);
        }

        return 0.5 * entropy / Math.Log(Bins) + Math.Sqrt(variance) / 63;
    }

    public static double Difference(ReadOnlySpan<int> a, ReadOnlySpan<int> b)
    {
        long na = 0, nb = 0, common = 0;
        for (int i = 0; i < a.Length; i++)
        {
            na += a[i];
            nb += b[i];
        }

        if (na == 0 || nb == 0)
        {
            return 1;
        }

        for (int i = 0; i < a.Length; i++)
        {
            common += Math.Min(a[i] * nb, b[i] * na);
        }

        return 1 - (double)common / (na * nb);
    }

    public static SourceAnalysis AnalyzePicture(string path)
    {
        using var decoder = new VideoDecoder(path, allowHardware: false);
        using DecodedFrame? frame = decoder.GetFrame(MediaTime.Zero);
        if (frame is null)
        {
            return SourceAnalysis.Picture(0);
        }

        int[] histogram = new int[Bins];
        Histogram(frame, histogram);
        return SourceAnalysis.Picture(Quality(histogram));
    }

    public static SourceAnalysis AnalyzeVideo(string path, MediaTime start, MediaTime end, CancellationToken cancel = default)
    {
        using var decoder = new VideoDecoder(path, allowHardware: false) { SkipLoopFilter = true };
        decoder.SeekTo(start);
        var times = new List<MediaTime>();
        var qualities = new List<double>();
        var motions = new List<(int Kind, double Amount)>();
        var thumbs = new Queue<float[]>();
        var cuts = new List<int> { 0 };
        int[] previous = new int[Bins], current = new int[Bins];
        var differences = new Queue<double>();
        double differenceSum = 0;
        while (decoder.ReadNext() is { } frame)
        {
            using (frame)
            {
                cancel.ThrowIfCancellationRequested();
                if (frame.End <= start)
                {
                    continue;
                }

                if (frame.Pts >= end)
                {
                    break;
                }

                Array.Clear(current);
                Histogram(frame, current);
                float[] thumb = thumbs.Count > MotionSpan ? thumbs.Dequeue() : new float[ThumbWidth * ThumbHeight];
                Thumbnail(frame, thumb);
                (int kind, double amount) = thumbs.Count == 0 ? (0, 0) : CameraMotion(thumbs.Peek(), thumb);
                motions.Add((kind, amount / Math.Max(1, thumbs.Count)));
                thumbs.Enqueue(thumb);
                if (times.Count > 0)
                {
                    double d = Difference(previous, current);
                    double mean = differences.Count == 0 ? d : differenceSum / differences.Count;
                    if (d > CutDifference && d / (mean + 1e-7) >= CutRatio)
                    {
                        cuts.Add(times.Count);
                    }

                    differences.Enqueue(d);
                    differenceSum += d;
                    if (differences.Count > MeanFrames)
                    {
                        differenceSum -= differences.Dequeue();
                    }
                }

                times.Add(MediaTime.Max(frame.Pts, start));
                qualities.Add(Quality(current));
                (previous, current) = (current, previous);
            }
        }

        var subshots = new List<Subshot>();
        for (int s = 0; s < cuts.Count; s++)
        {
            int from = cuts[s], to = s + 1 < cuts.Count ? cuts[s + 1] : times.Count;
            List<int> bounds = MotionSplits(motions, from, to);
            for (int b = 0; b + 1 < bounds.Count; b++)
            {
                MediaTime subEnd = bounds[b + 1] < times.Count ? times[bounds[b + 1]] : end;
                double motion = 0;
                for (int i = bounds[b]; i < bounds[b + 1]; i++)
                {
                    motion += motions[i].Amount;
                }

                Subshot u = Score(s, times, qualities, bounds[b], bounds[b + 1], subEnd);
                subshots.Add(u with { Motion = motion / Math.Max(1, bounds[b + 1] - bounds[b]) });
            }
        }

        return SourceAnalysis.Video(subshots);
    }

    private static Subshot Score(int shot, List<MediaTime> times, List<double> q, int from, int to, MediaTime shotEnd)
    {
        int bad = 0;
        int bestStart = from, bestLength = 0, runStart = from;
        for (int i = from; i <= to; i++)
        {
            bool isBad = i == to || q[i] <= BadFrame;
            if (i < to && isBad)
            {
                bad++;
            }

            if (isBad)
            {
                if (i - runStart > bestLength)
                {
                    (bestStart, bestLength) = (runStart, i - runStart);
                }

                runStart = i + 1;
            }
        }

        MediaTime At(int i) => i < to ? times[i] : shotEnd;
        if (to == from || bad * 2 > to - from || bestLength == 0)
        {
            return new Subshot(shot, At(from), At(to) - At(from), 0);
        }

        double sum = 0;
        for (int i = bestStart; i < bestStart + bestLength; i++)
        {
            sum += q[i];
        }

        return new Subshot(shot, At(bestStart), At(bestStart + bestLength) - At(bestStart), sum / bestLength);
    }

    private static List<int> MotionSplits(List<(int Kind, double Amount)> motions, int from, int to)
    {
        var bounds = new List<int> { from };
        int start = from;
        int[] counts = new int[7];
        for (int i = from; i < to; i++)
        {
            counts[motions[i].Kind]++;
            int length = i - start + 1;
            if (length < 2 * MinSubshotFrames || to - i <= MinSubshotFrames)
            {
                continue;
            }

            int main = Array.IndexOf(counts, counts.Max());
            int recent = 0;
            for (int k = i - MinSubshotFrames + 1; k <= i; k++)
            {
                if (motions[k].Kind == main)
                {
                    recent++;
                }
            }

            double change = (double)recent / MinSubshotFrames - (double)counts[main] / length;
            if (change >= 0.35 || change <= -0.30)
            {
                int split = i - MinSubshotFrames + 1;
                if (change < 0)
                {
                    while (split < i - 5 && Enumerable.Range(split, 5).Any(k => motions[k].Kind == main))
                    {
                        split++;
                    }
                }

                bounds.Add(split);
                start = split;
                Array.Clear(counts);
                for (int k = split; k <= i; k++)
                {
                    counts[motions[k].Kind]++;
                }
            }
        }

        bounds.Add(to);
        return bounds;
    }

    private static (int Kind, double Amount) CameraMotion(float[] a, float[] b)
    {
        double still = Sad(a, b, 0, 0, 1);
        (int dx, int dy, double best) = (0, 0, still);
        for (int y = -3; y <= 3; y++)
        {
            for (int x = -3; x <= 3; x++)
            {
                if ((x != 0 || y != 0) && Sad(a, b, x, y, 1) is var d && d < best)
                {
                    (dx, dy, best) = (x, y, d);
                }
            }
        }

        double zoomIn = Sad(a, b, 0, 0, 1.06), zoomOut = Sad(a, b, 0, 0, 1 / 1.06);
        double clear = still * 0.85;
        if (Math.Min(zoomIn, zoomOut) < Math.Min(best, clear))
        {
            return zoomIn < zoomOut ? (5, 2) : (6, 2);
        }

        if (best >= clear || (dx == 0 && dy == 0))
        {
            return (0, 0);
        }

        double amount = Math.Sqrt(dx * dx + dy * dy);
        return Math.Abs(dx) >= Math.Abs(dy) ? (dx < 0 ? 1 : 2, amount) : (dy < 0 ? 3 : 4, amount);
    }

    private static double Sad(float[] a, float[] b, int dx, int dy, double scale)
    {
        double sum = 0;
        int n = 0;
        const double cx = ThumbWidth / 2.0, cy = ThumbHeight / 2.0;
        for (int y = 4; y < ThumbHeight - 4; y++)
        {
            for (int x = 4; x < ThumbWidth - 4; x++)
            {
                int sx = (int)Math.Round(cx + (x - cx) / scale) - dx, sy = (int)Math.Round(cy + (y - cy) / scale) - dy;
                if (sx < 0 || sy < 0 || sx >= ThumbWidth || sy >= ThumbHeight)
                {
                    continue;
                }

                sum += Math.Abs(b[y * ThumbWidth + x] - a[sy * ThumbWidth + sx]);
                n++;
            }
        }

        return n == 0 ? double.MaxValue : sum / n;
    }

    private static unsafe void Thumbnail(DecodedFrame frame, float[] thumb)
    {
        FramePlane p = frame.Plane(0);
        byte* data = (byte*)p.Data;
        for (int ty = 0; ty < ThumbHeight; ty++)
        {
            byte* row = data + (long)((ty * 2 + 1) * p.Height / (ThumbHeight * 2)) * p.Stride;
            for (int tx = 0; tx < ThumbWidth; tx++)
            {
                int x = (tx * 2 + 1) * p.Width / (ThumbWidth * 2);
                thumb[ty * ThumbWidth + tx] = frame.Format switch
                {
                    FramePixelFormat.Yuv420P or FramePixelFormat.Nv12 => row[x],
                    FramePixelFormat.Yuv420P10 => (((ushort*)row)[x] & 0x3FF) >> 2,
                    FramePixelFormat.P010 => ((ushort*)row)[x] >> 8,
                    FramePixelFormat.Bgra => (row[x * 4 + 2] * 77 + row[x * 4 + 1] * 150 + row[x * 4] * 29) >> 8,
                    _ => (row[x * 4] * 77 + row[x * 4 + 1] * 150 + row[x * 4 + 2] * 29) >> 8,
                };
            }
        }
    }

    private static unsafe void Histogram(DecodedFrame frame, int[] histogram)
    {
        const int step = 4;
        FramePlane p = frame.Plane(0);
        byte* data = (byte*)p.Data;
        for (int y = 0; y < p.Height; y += step)
        {
            byte* row = data + (long)y * p.Stride;
            for (int x = 0; x < p.Width; x += step)
            {
                int bin = frame.Format switch
                {
                    FramePixelFormat.Yuv420P or FramePixelFormat.Nv12 => row[x] >> 2,
                    FramePixelFormat.Yuv420P10 => (((ushort*)row)[x] & 0x3FF) >> 4,
                    FramePixelFormat.P010 => ((ushort*)row)[x] >> 10,
                    FramePixelFormat.Bgra => (row[x * 4 + 2] * 77 + row[x * 4 + 1] * 150 + row[x * 4] * 29) >> 10,
                    _ => (row[x * 4] * 77 + row[x * 4 + 1] * 150 + row[x * 4 + 2] * 29) >> 10,
                };
                histogram[bin]++;
            }
        }
    }
}
