using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media;
using AvaMovieMaker.Media.Analysis;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Media.Probing;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Import;

public enum ImportError
{
    Failed,
    NotFound,
    Folder,
    Empty,
    NotSupported,
    MissingCodec,
}

public static class MediaImporter
{
    public sealed record Failure(string Path, ImportError Error, string Reason);

    public sealed record Result(IReadOnlyList<MediaItem> Items, IReadOnlyList<Failure> Failures);

    public static async Task<Result> ImportAsync(IReadOnlyList<string> paths, bool createClips, int batch,
        IProgress<(int Done, int Total, string Name)>? progress = null, CancellationToken cancel = default)
    {
        var items = new List<MediaItem>();
        var failures = new List<Failure>();
        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            progress?.Report((i, paths.Count, Path.GetFileName(path)));
            if (cancel.IsCancellationRequested)
            {
                break;
            }

            if (!await Task.Run(() => ImportOne(path, createClips, batch, items, failures, cancel), CancellationToken.None).ConfigureAwait(false))
            {
                break;
            }
        }

        progress?.Report((paths.Count, paths.Count, string.Empty));
        return new Result(items, failures);
    }

    public static Result Import(IReadOnlyList<string> paths, bool createClips, int batch,
        IProgress<(int Done, int Total, string Name)>? progress = null, CancellationToken cancel = default)
    {
        var items = new List<MediaItem>();
        var failures = new List<Failure>();
        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            progress?.Report((i, paths.Count, Path.GetFileName(path)));

            if (cancel.IsCancellationRequested || !ImportOne(path, createClips, batch, items, failures, cancel))
            {
                break;
            }
        }

        progress?.Report((paths.Count, paths.Count, string.Empty));
        return new Result(items, failures);
    }

    private static bool ImportOne(string path, bool createClips, int batch, List<MediaItem> items, List<Failure> failures, CancellationToken cancel)
    {
        if (Check(path) is { } error)
        {
            Log.Warn("import", $"{path}: {error}");
            failures.Add(new Failure(path, error, string.Empty));
            return true;
        }

        try
        {
            items.Add(FromFile(path, createClips, batch, cancel));
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            Log.Warn("import", $"{path}: {e.Message}");
            failures.Add(new Failure(path, Classify(e), e.Message));
        }

        return true;
    }

    public static ImportError? Check(string path)
    {
        if (FileStore.Current.DirectoryExists(path))
        {
            return ImportError.Folder;
        }

        if (FileStore.Current.GetInfo(path) is not { } file)
        {
            return ImportError.NotFound;
        }

        if (!MediaFormats.IsSupported(path))
        {
            return ImportError.NotSupported;
        }

        return file.Length == 0 ? ImportError.Empty : null;
    }

    private static ImportError Classify(Exception e) => e switch
    {
        MissingCodecException => ImportError.MissingCodec,
        FileNotFoundException or DirectoryNotFoundException => ImportError.NotFound,
        FFmpegException => ImportError.NotSupported,
        _ => ImportError.Failed,
    };

    public static string ClipName(string name, int index) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name} {index + 1:000}");

    public static List<SourceClip> ClipsFor(string name, IReadOnlyList<(MediaTime Start, MediaTime End)> ranges) =>
        ranges.Count == 1
            ? [new SourceClip { Name = name, Start = ranges[0].Start, End = ranges[0].End }]
            : ranges.Select((r, i) => new SourceClip { Name = ClipName(name, i), Start = r.Start, End = r.End }).ToList();

    public static MediaItem FromFile(string path, bool createClips, int batch, CancellationToken cancel = default)
    {
        MediaInfo info = MediaProbe.Probe(path);
        string name = Path.GetFileNameWithoutExtension(path);
        var item = new MediaItem
        {
            Kind = info.Kind,
            Path = info.Path,
            Name = name,
            Duration = info.Duration,
            FileSize = info.FileSize,
            LastWriteTimeUtc = info.LastWriteTimeUtc,
            DateTaken = info.DateTaken,
            ImportBatch = batch,
            Video = info.Video is { } v ? new VideoProperties
            {
                Width = v.Width,
                Height = v.Height,
                SampleAspectNum = v.SampleAspect.Num,
                SampleAspectDen = v.SampleAspect.Den,
                FrameRateNum = v.FrameRate.Num,
                FrameRateDen = v.FrameRate.Den,
                Rotation = v.Rotation,
                FlipHorizontal = v.FlipHorizontal,
                Codec = v.Codec,
            } : null,
            Audio = info.Audio is { } a ? new AudioProperties { SampleRate = a.SampleRate, Channels = a.Channels, Codec = a.Codec } : null,
        };

        IReadOnlyList<(MediaTime Start, MediaTime End)> ranges = [(MediaTime.Zero, info.Duration)];
        if (createClips && info.Kind == MediaKind.Video && info.Duration > MediaTime.FromSeconds(2))
        {
            ranges = ClipDetector.Detect(path, null, cancel);
        }

        return item with { Clips = ClipsFor(name, ranges) };
    }
}
