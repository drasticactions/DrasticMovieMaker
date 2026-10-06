using System.Diagnostics;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Effects;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Media.Decoding;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Export;
using AvaMovieMaker.Timeline.Import;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using AvaMovieMaker.Timeline.Serialization;
using AvaMovieMaker.Undo;
using SkiaSharp;

namespace AvaMovieMaker;

internal static class Smoke
{
    public static int Run(string[] args)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            Step("ffmpeg", () => FFmpegRuntime.Check() is { } e ? throw new InvalidOperationException(e) : FFmpegRuntime.VersionText);
            string? input = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
            Project project = Step("open", () => Open(input), p => $"{p.VideoTrack.Count} video clips, {p.Duration}");
            using RenderDevice device = Step("device", () => RenderDevice.Create(preferGpu: !args.Contains("--software")), d => d.Description);
            using var pool = new DecoderPool(allowHardware: false);
            var frames = new PooledFrameProvider(pool);
            RenderPlan plan = RenderPlanner.Build(project);
            Step("render", () =>
            {
                using var compositor = new Compositor(device, EffectLibrary.Instance, frames);
                byte[] pixels = new byte[320 * 240 * 4];
                long lit = 0;
                for (int i = 0; i < 10; i++)
                {
                    MediaTime t = MediaTime.FromSeconds(plan.Duration.Seconds * i / 10);
                    device.Thread.Invoke(() =>
                    {
                        using SKImage img = compositor.Render(plan.PlanAt(t), 320, 240);
                        Compositor.ReadPixels(img, pixels, bgra: false, device.Context);
                    });
                    for (int p = 0; p < pixels.Length; p += 4)
                    {
                        lit += pixels[p] + pixels[p + 1] + pixels[p + 2] > 48 ? 1 : 0;
                    }
                }

                return lit > 0 ? $"10 frames, {lit * 100 / (320 * 240 * 10)}% non-black" : throw new InvalidOperationException("Every frame is black.");
            });
            Step("shared", () =>
            {
                if (!OperatingSystem.IsLinux())
                {
                    return "not available on this OS: the preview composes on the interface's GPU";
                }

                if (device.DeviceUuid is not { } uuid)
                {
                    return "not available (no GPU UUID)";
                }

                using SharedFramePool? shared = SharedFramePool.TryCreate(device, uuid);
                if (shared is null)
                {
                    return "not available; the preview composes on the interface's GPU instead (see log)";
                }

                using var compositor = new Compositor(device, EffectLibrary.Instance, frames);
                SharedFrameBuffer? buffer = device.Thread.Invoke(() =>
                {
                    using SKImage img = compositor.Render(plan.PlanAt(MediaTime.Zero), 320, 240);
                    return shared.TryPresent(img);
                });
                buffer?.Release(consumed: false);
                return buffer is null ? throw new InvalidOperationException("No shared frame buffer.") : shared.Description;
            });
            string output = Path.Combine(Path.GetTempPath(), $"amm-smoke-{Environment.ProcessId}.mp4");
            Step("publish", () =>
            {
                var settings = new EncoderSettings { Container = ContainerFormat.Mp4, Width = 320, Height = 240, FrameRate = project.Settings.FrameRate, Crf = 28, Preset = "veryfast", AudioBitrate = 96_000 };
                MovieExporter.Export(device, EffectLibrary.Instance, frames, plan, settings, output, limit: MediaTime.FromSeconds(1));
                long size = new FileInfo(output).Length;
                return size > 1000 ? $"{size} bytes" : throw new InvalidOperationException($"Output is only {size} bytes.");
            });
            Step("decode", () =>
            {
                try
                {
                    return Decode(device, output, args.Contains("--software"));
                }
                finally
                {
                    File.Delete(output);
                }
            });
            Console.WriteLine($"smoke: OK in {watch.Elapsed.TotalSeconds:0.0} s");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"smoke: FAILED: {e.Message}");
            Log.Error("smoke", "Smoke run failed", e);
            return 1;
        }
    }

    private static string Decode(RenderDevice device, string path, bool software)
    {
        using var decoder = new VideoDecoder(path, allowHardware: !software) { KeepHardwareFrames = device.SupportsZeroCopy };
        using DecodedFrame frame = decoder.ReadNext() ?? throw new InvalidOperationException("No frame decoded.");
        if (!decoder.IsHardware)
        {
            return $"software, {frame.Format}";
        }

        if (!frame.IsHardware)
        {
            return $"hardware, frames copied to memory, {frame.Format}";
        }

        bool copied = device.Thread.Invoke(() =>
        {
            using YuvUploader.Planes planes = YuvUploader.Upload(device, frame);
            return frame.HasSoftwareCopy;
        });
        return copied ? "hardware, but the GPU import fell back to a copy through memory" : "hardware, zero-copy to the GPU";
    }

    private static Project Open(string? input)
    {
        if (input is not null && input.EndsWith(".ammproj", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectSerializer.Load(input);
        }

        var project = new Project();
        var editor = new TimelineEditor(project, new UndoStack());
        if (input is not null)
        {
            MediaItem m = MediaImporter.FromFile(input, false, 1);
            editor.ImportMedia([m]);
            editor.InsertVideoClips(0, [editor.NewVideoClip(m)]);
        }
        else
        {
            editor.AddTitle(new TitleContent { Lines = ["AvaMovieMaker", "smoke test"] }, null, MediaTime.Zero);
        }

        return project;
    }

    private static T Step<T>(string name, Func<T> run, Func<T, string>? describe = null)
    {
        var w = Stopwatch.StartNew();
        T result = run();
        string text = describe is null ? result?.ToString() ?? string.Empty : describe(result);
        Console.WriteLine($"smoke: {name,-8} {w.ElapsedMilliseconds,6} ms  {text}");
        return result;
    }
}
