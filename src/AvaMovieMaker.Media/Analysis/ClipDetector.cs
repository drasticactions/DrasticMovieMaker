using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Analysis;

public static unsafe class ClipDetector
{
    public const int Bins = 256;

    public const int MaxWidth = 100;

    public const int Window = 15;

    public const double MinDifference = 0.16;

    public const double MinRatio = 2.0;

    public const int MinFramesApart = 15;

    public static readonly TimeSpan MaxRecordingGap = TimeSpan.FromSeconds(1);

    public static Task<IReadOnlyList<(MediaTime Start, MediaTime End)>> DetectAsync(string path, IProgress<double>? progress = null, CancellationToken cancel = default) =>
        Task.Run(() => Detect(path, progress, cancel), cancel);

    public static IReadOnlyList<(MediaTime Start, MediaTime End)> Detect(string path, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        HashSet<int> dvBreaks = [.. DvRecordingBreaks(path, cancel)];
        using var dec = new VideoDecoder(path, allowHardware: false);
        MediaTime duration = dec.Duration;
        var cuts = new List<MediaTime> { MediaTime.Zero };
        int[]? prev = null;
        var recent = new Queue<double>();
        double sum = 0;
        int frame = 0;
        int lastCut = 0;
        MediaTime lastEnd = MediaTime.Zero;
        dec.SeekTo(MediaTime.Zero);
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            using DecodedFrame? f = dec.ReadNext();
            if (f is null)
            {
                break;
            }

            int[] h = Histogram(f, out int pixels);
            bool cut = frame > 0 && dvBreaks.Contains(frame);
            if (prev is not null)
            {
                double d = Difference(prev, h, pixels);
                cut |= IsCut(d, sum / Window) && frame - lastCut > MinFramesApart;
                recent.Enqueue(d);
                sum += d;
                if (recent.Count > Window)
                {
                    sum -= recent.Dequeue();
                }
            }

            if (cut)
            {
                cuts.Add(f.Pts);
                lastCut = frame;
            }

            prev = h;
            lastEnd = f.End;
            frame++;
            if (duration > MediaTime.Zero)
            {
                progress?.Report(Math.Clamp(f.Pts / duration, 0, 1));
            }
        }

        MediaTime end = MediaTime.Max(duration, lastEnd);
        var clips = new List<(MediaTime, MediaTime)>();
        for (int i = 0; i < cuts.Count; i++)
        {
            clips.Add((cuts[i], i + 1 < cuts.Count ? cuts[i + 1] : end));
        }

        return clips;
    }

    public static bool IsCut(double difference, double windowMean) =>
        difference > MinDifference && difference / (windowMean + 1e-7) > MinRatio;

    public static double Difference(int[] a, int[] b, int pixels)
    {
        long common = 0;
        for (int i = 0; i < a.Length; i++)
        {
            common += Math.Min(a[i], b[i]);
        }

        return pixels == 0 ? 0 : 1.0 - common / (double)pixels;
    }

    public static int ReductionFactor(int width)
    {
        int k = 1;
        while (width / k > MaxWidth)
        {
            k *= 2;
        }

        return k;
    }

    public static int[] Histogram(DecodedFrame f, out int pixels)
    {
        int k = ReductionFactor(f.Width);
        int w = Math.Max(1, f.Width / k);
        int h = Math.Max(1, f.Height / k);
        FramePlane p = f.Plane(0);
        byte* data = (byte*)p.Data;
        var hist = new int[Bins];
        for (int by = 0; by < h; by++)
        {
            for (int bx = 0; bx < w; bx++)
            {
                int total = 0;
                for (int y = by * k; y < by * k + k; y++)
                {
                    byte* row = data + (long)y * p.Stride;
                    for (int x = bx * k; x < bx * k + k; x++)
                    {
                        total += Luma(f.Format, row, x);
                    }
                }

                hist[total / (k * k)]++;
            }
        }

        pixels = w * h;
        return hist;
    }

    private static int Luma(FramePixelFormat format, byte* row, int x)
    {
        switch (format)
        {
            case FramePixelFormat.Yuv420P10:
                return Math.Min(255, ((ushort*)row)[x] >> 2);
            case FramePixelFormat.P010:
                return ((ushort*)row)[x] >> 8;
            case FramePixelFormat.Bgra:
            {
                byte* px = row + x * 4;
                return (px[2] * 299 + px[1] * 587 + px[0] * 114) / 1000;
            }

            case FramePixelFormat.Rgba:
            {
                byte* px = row + x * 4;
                return (px[0] * 299 + px[1] * 587 + px[2] * 114) / 1000;
            }

            default:
                return row[x];
        }
    }

    public static IReadOnlyList<int> DvRecordingBreaks(string path, CancellationToken cancel = default)
    {
        var breaks = new List<int>();
        using var input = new InputFile(path);
        int vi = input.FindBest(AVMediaType.AVMEDIA_TYPE_VIDEO);
        if (vi < 0 || input.Stream(vi)->codecpar->codec_id != AVCodecID.AV_CODEC_ID_DVVIDEO)
        {
            return breaks;
        }

        AVPacket* pkt = ffmpeg.av_packet_alloc();
        try
        {
            int frame = 0;
            TimeSpan? previous = null;
            while (ffmpeg.av_read_frame(input.Context, pkt) >= 0)
            {
                cancel.ThrowIfCancellationRequested();
                if (pkt->stream_index == vi)
                {
                    TimeSpan? time = DvRecordingTime(new ReadOnlySpan<byte>(pkt->data, pkt->size));
                    if (frame > 0 && IsRecordingBreak(previous, time))
                    {
                        breaks.Add(frame);
                    }

                    previous = time;
                    frame++;
                }

                ffmpeg.av_packet_unref(pkt);
            }
        }
        finally
        {
            ffmpeg.av_packet_free(&pkt);
        }

        return breaks;
    }

    public static bool IsRecordingBreak(TimeSpan? previous, TimeSpan? current)
    {
        if (previous is null || current is null)
        {
            return previous.HasValue != current.HasValue;
        }

        return (current.Value - previous.Value).Duration() > MaxRecordingGap;
    }

    public static TimeSpan? DvRecordingTime(ReadOnlySpan<byte> frame)
    {
        const int block = 80;
        for (int b = 3; b <= 5; b++)
        {
            for (int k = 0; k < 15; k++)
            {
                int o = b * block + 3 + k * 5;
                if (o + 5 > frame.Length)
                {
                    return null;
                }

                if (frame[o] != 0x63)
                {
                    continue;
                }

                if (Bcd(frame[o + 2] & 0x7F) is { } s && Bcd(frame[o + 3] & 0x7F) is { } m && Bcd(frame[o + 4] & 0x3F) is { } h && s < 60 && m < 60 && h < 24)
                {
                    return new TimeSpan(h, m, s);
                }
            }
        }

        return null;
    }

    private static int? Bcd(int v) => (v >> 4) <= 9 && (v & 0xF) <= 9 ? (v >> 4) * 10 + (v & 0xF) : null;
}
