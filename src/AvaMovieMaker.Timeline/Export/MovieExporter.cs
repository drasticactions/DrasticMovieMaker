using System.Diagnostics;
using AvaMovieMaker.Audio.Mixing;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Planning;
using SkiaSharp;

namespace AvaMovieMaker.Timeline.Export;

public static class MovieExporter
{
    public static Task ExportAsync(RenderDevice device, IEffectLibrary effects, IFrameProvider frames, RenderPlan plan,
        EncoderSettings settings, string path, IProgress<ExportProgress>? progress = null, CancellationToken cancel = default) =>
        Task.Run(() => Export(device, effects, frames, plan, settings, path, progress, cancel), cancel);

    public static void Export(RenderDevice device, IEffectLibrary effects, IFrameProvider frames, RenderPlan plan,
        EncoderSettings settings, string path, IProgress<ExportProgress>? progress = null, CancellationToken cancel = default,
        MediaTime? limit = null)
    {
        try
        {
            ExportOnce(device, effects, frames, plan, settings, path, progress, cancel, limit);
        }
        catch (FFmpegException e) when (settings.HardwareEncode && !cancel.IsCancellationRequested)
        {
            // Drivers can refuse a frame size or format the software encoder handles, so retry without the hardware.
            Log.Warn("encode", $"Hardware encoding failed ({e.Message}); encoding in software instead.");
            ExportOnce(device, effects, frames, plan, settings with { HardwareEncode = false }, path, progress, cancel, limit);
        }
    }

    private static void ExportOnce(RenderDevice device, IEffectLibrary effects, IFrameProvider frames, RenderPlan plan,
        EncoderSettings settings, string path, IProgress<ExportProgress>? progress, CancellationToken cancel, MediaTime? limit)
    {
        Rational rate = settings.FrameRate;
        MediaTime duration = limit is { } l ? MediaTime.Min(l, plan.Duration) : plan.Duration;
        long total = Math.Max(1, (long)Math.Ceiling(duration.Seconds * rate.ToDouble() - 1e-9));
        int w = settings.Width, h = settings.Height;
        double pixelAspect = settings.SampleAspect.ToDouble();
        byte[] pixels = new byte[w * h * 4];
        var watch = Stopwatch.StartNew();
        using var compositor = new Compositor(device, effects, frames);
        using var mixer = new AudioMixer(plan.Audio);
        using var encoder = new MovieEncoder(path, settings);
        try
        {
            long samplesDone = 0;
            float[] audio = [];
            for (long n = 0; n < total; n++)
            {
                cancel.ThrowIfCancellationRequested();
                MediaTime t = MediaTime.FromFrame(n, rate);
                device.Thread.Invoke(() =>
                {
                    using SKImage img = compositor.Render(plan.PlanAt(t), w, h, pixelAspect);
                    Compositor.ReadPixels(img, pixels, bgra: false);
                });
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        encoder.WriteVideo((IntPtr)p, w * 4, bgra: false);
                    }
                }

                if (settings.IncludeAudio)
                {
                    long until = Media.Decoding.AudioReader.ToSample(MediaTime.FromFrame(n + 1, rate));
                    int count = (int)(until - samplesDone);
                    if (audio.Length < count * 2)
                    {
                        audio = new float[count * 2];
                    }

                    Span<float> span = audio.AsSpan(0, count * 2);
                    mixer.Read(samplesDone, span);
                    encoder.WriteAudio(span);
                    samplesDone = until;
                }

                if (progress is not null && (n % 5 == 0 || n == total - 1))
                {
                    double f = (n + 1) / (double)total;
                    TimeSpan remaining = TimeSpan.FromSeconds(watch.Elapsed.TotalSeconds / f * (1 - f));
                    progress.Report(new ExportProgress(f, remaining, n + 1, total));
                }
            }

            encoder.Finish();
        }
        catch
        {
            encoder.Abort();
            throw;
        }
    }
}
