using System.Globalization;
using System.Text.Json;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Serialization;

public static class ProjectSerializer
{
    public const string Extension = ".ammproj";

    public static void Save(Project project, string path)
    {
        string full = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(full)!;
        project.Format = Project.FileFormat;
        project.Version = Math.Max(project.Version, Project.FileVersion);
        project.Media = project.Media.Select(m => m with { RelativePath = Relative(dir, m.Path) }).ToList();
        AtomicFile.Write(full, s => JsonSerializer.Serialize(s, project, AmmJsonContext.Default.Project));
    }

    public static void SaveCopy(Project project, string path)
    {
        string? keep = project.FilePath;
        Save(project, path);
        project.FilePath = keep;
    }

    public static string ToJson(Project project) => JsonSerializer.Serialize(project, AmmJsonContext.Default.Project);

    public static Project FromJson(string json) =>
        JsonSerializer.Deserialize(json, AmmJsonContext.Default.Project) ?? throw new InvalidDataException(Strings.ProjectFileEmpty);

    public static Project Load(string path)
    {
        string full = Path.GetFullPath(path);
        Project p;
        using (Stream s = FileStore.Current.OpenRead(full))
        {
            p = JsonSerializer.Deserialize(s, AmmJsonContext.Default.Project) ?? throw new InvalidDataException(Strings.ProjectFileEmpty);
        }

        if (p.Format != Project.FileFormat)
        {
            throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, Strings.NotAProjectFile, Path.GetFileName(full)));
        }

        if (p.Version > Project.FileVersion)
        {
            Log.Warn("project", $"{full} was written by a newer version ({p.Version}); unknown parts are kept.");
        }

        string dir = Path.GetDirectoryName(full)!;
        p.FilePath = full;
        p.Media = p.Media.Select(m => Resolve(m, dir)).ToList();
        return p;
    }

    private static MediaItem Resolve(MediaItem m, string projectDir)
    {
        if (FileStore.Current.Exists(m.Path))
        {
            return m with { Missing = false };
        }

        foreach (string candidate in Candidates(m, projectDir))
        {
            if (FileStore.Current.Exists(candidate))
            {
                Log.Info("project", $"Found moved file {m.Path} at {candidate}");
                return m with { Path = Path.GetFullPath(candidate), Missing = false };
            }
        }

        return m with { Missing = true };
    }

    private static IEnumerable<string> Candidates(MediaItem m, string projectDir)
    {
        if (!string.IsNullOrEmpty(m.RelativePath))
        {
            yield return Path.Combine(projectDir, m.RelativePath);
        }

        string name = Path.GetFileName(m.Path.Replace('\\', '/'));
        yield return Path.Combine(projectDir, name);
        string? oldDir = Path.GetDirectoryName(m.Path);
        if (!string.IsNullOrEmpty(oldDir))
        {
            yield return Path.Combine(oldDir, name);
        }
    }

    public static int FindMissing(Project project, string folder)
    {
        int found = 0;
        var files = FileStore.EnumerateFilesRecursive(folder).ToLookup(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < project.Media.Count; i++)
        {
            MediaItem m = project.Media[i];
            if (!m.Missing)
            {
                continue;
            }

            string name = Path.GetFileName(m.Path.Replace('\\', '/'));
            string? match = files[name].FirstOrDefault(f => m.FileSize == 0 || FileStore.Current.GetInfo(f)?.Length == m.FileSize) ?? files[name].FirstOrDefault();
            if (match is not null)
            {
                project.Media[i] = m with { Path = match, Missing = false };
                found++;
            }
        }

        return found;
    }

    private static string? Relative(string dir, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string rel = Path.GetRelativePath(dir, path);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel;
    }
}
