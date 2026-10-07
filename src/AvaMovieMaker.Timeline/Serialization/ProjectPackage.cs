using System.Globalization;
using System.IO.Compression;
using AvaMovieMaker.IO;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Serialization;

public static class ProjectPackage
{
    public const string Extension = ".dmmpkg";

    private const string ProjectEntry = "project" + ProjectSerializer.Extension;

    public static IReadOnlyList<string> Export(Project project, Stream output)
    {
        var missing = new List<string>();
        var media = new List<MediaItem>();
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        int n = 0;
        foreach (MediaItem m in project.Media)
        {
            if (m.Missing || !FileStore.Current.Exists(m.Path))
            {
                missing.Add(m.Path);
                media.Add(m with { RelativePath = null, Missing = true });
                continue;
            }

            string entry = string.Create(CultureInfo.InvariantCulture, $"media/{n++}/{Path.GetFileName(m.Path)}");
            using (Stream from = FileStore.Current.OpenRead(m.Path))
            using (Stream to = zip.CreateEntry(entry, CompressionLevel.NoCompression).Open())
            {
                from.CopyTo(to);
            }

            media.Add(m with { Path = entry, RelativePath = entry });
        }

        Project copy = ProjectSerializer.FromJson(ProjectSerializer.ToJson(project));
        copy.Format = Project.FileFormat;
        copy.Version = Math.Max(copy.Version, Project.FileVersion);
        copy.Media = media;
        copy.FilePath = null;
        using (var writer = new StreamWriter(zip.CreateEntry(ProjectEntry, CompressionLevel.Optimal).Open()))
        {
            writer.Write(ProjectSerializer.ToJson(copy));
        }

        return missing;
    }

    public static Project Import(Stream package, string mediaFolder)
    {
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry entry = zip.GetEntry(ProjectEntry) ?? throw new InvalidDataException(Strings.NotAProjectPackage);
        Project p;
        using (var reader = new StreamReader(entry.Open()))
        {
            p = ProjectSerializer.FromJson(reader.ReadToEnd());
        }

        if (p.Format != Project.FileFormat)
        {
            throw new InvalidDataException(Strings.NotAProjectPackage);
        }

        p.Media = p.Media.Select(m => Extract(zip, m, mediaFolder)).ToList();
        p.FilePath = null;
        return p;
    }

    private static MediaItem Extract(ZipArchive zip, MediaItem m, string mediaFolder)
    {
        string[] parts = (m.RelativePath ?? string.Empty).Split('/');
        if (parts.Length != 3 || parts[0] != "media" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int n)
            || zip.GetEntry(m.RelativePath!) is not { } file)
        {
            return m with { RelativePath = null, Missing = true };
        }

        string folder = Path.Combine(mediaFolder, n.ToString(CultureInfo.InvariantCulture));
        string target = Path.Combine(folder, Path.GetFileName(parts[2]));
        FileStore.Current.CreateDirectory(folder);
        using (Stream from = file.Open())
        using (Stream to = FileStore.Current.Create(target))
        {
            from.CopyTo(to);
        }

        return m with { Path = Path.GetFullPath(target), RelativePath = null, Missing = false };
    }
}
