using System.Security.Cryptography;
using System.Text;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.Media.Analysis;

public static class MediaCache
{
    public static string? RootOverride { get; set; }

    public static string Root => RootOverride ?? AppPaths.CacheDir;

    public static string FolderFor(string path)
    {
        StoredFileInfo? fi = FileStore.Current.GetInfo(path);
        string key = $"{Path.GetFullPath(path)}\n{fi?.Length ?? 0}\n{fi?.LastWriteTimeUtc.Ticks ?? 0}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        string dir = Path.Combine(Root, Convert.ToHexStringLower(hash.AsSpan(0, 12)));
        return dir;
    }

    public static string FileFor(string mediaPath, string name)
    {
        return Path.Combine(FolderFor(mediaPath), name);
    }
}
