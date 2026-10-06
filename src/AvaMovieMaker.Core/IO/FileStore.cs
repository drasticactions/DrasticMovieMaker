using System.Text;

namespace AvaMovieMaker.IO;

public interface IFileStore
{
    Stream OpenRead(string reference);

    Stream Create(string reference);

    bool Exists(string reference);

    StoredFileInfo? GetInfo(string reference);

    void Delete(string reference);

    void Move(string source, string destination);

    bool DirectoryExists(string reference);

    void CreateDirectory(string reference);

    void DeleteDirectory(string reference);

    IReadOnlyList<string> EnumerateFiles(string directory);

    IReadOnlyList<string> EnumerateDirectories(string directory);

    long? AvailableFreeSpace(string reference);
}

public readonly record struct StoredFileInfo(long Length, DateTime LastWriteTimeUtc);

public static class FileStore
{
    public static IFileStore Current { get; set; } = new LocalFileStore();

    public static IEnumerable<string> EnumerateFilesRecursive(string directory)
    {
        foreach (string file in Current.EnumerateFiles(directory))
        {
            yield return file;
        }

        foreach (string sub in Current.EnumerateDirectories(directory))
        {
            foreach (string file in EnumerateFilesRecursive(sub))
            {
                yield return file;
            }
        }
    }

    public static string ReadAllText(string reference)
    {
        using var reader = new StreamReader(Current.OpenRead(reference), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static byte[] ReadAllBytes(string reference)
    {
        using Stream s = Current.OpenRead(reference);
        var buffer = new MemoryStream();
        s.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static void WriteAllText(string reference, string text)
    {
        using Stream s = Current.Create(reference);
        s.Write(new UTF8Encoding(false).GetBytes(text));
    }

    public static void WriteAllBytes(string reference, ReadOnlySpan<byte> bytes)
    {
        using Stream s = Current.Create(reference);
        s.Write(bytes);
    }
}

public sealed class LocalFileStore : IFileStore
{
    public Stream OpenRead(string reference) =>
        new FileStream(reference, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.RandomAccess);

    public Stream Create(string reference)
    {
        if (Path.GetDirectoryName(reference) is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
        }

        return new FileStream(reference, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, bufferSize: 1);
    }

    public bool Exists(string reference) => File.Exists(reference);

    public StoredFileInfo? GetInfo(string reference)
    {
        var file = new FileInfo(reference);
        return file.Exists ? new StoredFileInfo(file.Length, file.LastWriteTimeUtc) : null;
    }

    public void Delete(string reference)
    {
        if (File.Exists(reference))
        {
            File.Delete(reference);
        }
    }

    public void Move(string source, string destination) => File.Move(source, destination, overwrite: true);

    public bool DirectoryExists(string reference) => Directory.Exists(reference);

    public void CreateDirectory(string reference) => Directory.CreateDirectory(reference);

    public void DeleteDirectory(string reference)
    {
        if (Directory.Exists(reference))
        {
            Directory.Delete(reference, recursive: true);
        }
    }

    public IReadOnlyList<string> EnumerateFiles(string directory) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory) : [];

    public IReadOnlyList<string> EnumerateDirectories(string directory) =>
        Directory.Exists(directory) ? Directory.GetDirectories(directory) : [];

    public long? AvailableFreeSpace(string reference)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(reference)) ?? "/").AvailableFreeSpace;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
