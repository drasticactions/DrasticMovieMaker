using AvaMovieMaker.IO;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Interop.Mswmm;

public static partial class MswmmImporter
{
    public static MswmmImportResult Import(string path, string? mediaSearchFolder = null)
    {
        byte[] data = FileStore.ReadAllBytes(path);
        var warnings = new List<string>();
        Project project = Parse(data, warnings);
        var missing = ResolveMedia(project, Path.GetDirectoryName(Path.GetFullPath(path))!, mediaSearchFolder);
        project.FilePath = null;
        return new MswmmImportResult(project, warnings, missing);
    }

    private static List<string> ResolveMedia(Project project, string projectDir, string? searchFolder)
    {
        var missing = new List<string>();
        var folders = new[] { searchFolder, projectDir }.Where(f => !string.IsNullOrEmpty(f) && FileStore.Current.DirectoryExists(f)).Cast<string>().ToList();
        var lookup = folders.SelectMany(FileStore.EnumerateFilesRecursive)
            .ToLookup(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < project.Media.Count; i++)
        {
            MediaItem m = project.Media[i];
            string name = WindowsFileName(m.Path);
            string? match = lookup[name].FirstOrDefault(f => m.FileSize == 0 || FileStore.Current.GetInfo(f)?.Length / 1024 == m.FileSize / 1024) ?? lookup[name].FirstOrDefault();
            if (match is not null)
            {
                project.Media[i] = m with { Path = Path.GetFullPath(match), Missing = false, FileSize = FileStore.Current.GetInfo(match)?.Length ?? 0 };
            }
            else
            {
                project.Media[i] = m with { Missing = true };
                missing.Add(m.Path);
            }
        }

        return missing;
    }

    public static string WindowsFileName(string path)
    {
        int i = path.LastIndexOfAny(['\\', '/']);
        return i >= 0 ? path[(i + 1)..] : path;
    }
}
