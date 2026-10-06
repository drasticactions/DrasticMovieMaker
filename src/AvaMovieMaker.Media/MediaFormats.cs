namespace AvaMovieMaker.Media;

public static class MediaFormats
{
    public static readonly string[] Video =
    [
        ".asf", ".avi", ".dvr-ms", ".m1v", ".mp2", ".mp2v", ".mpe", ".mpeg", ".mpg", ".mpv2", ".wm", ".wmv",
        ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".ogv", ".ts", ".mts", ".m2ts", ".3gp", ".flv",
    ];

    public static readonly string[] Pictures =
    [
        ".bmp", ".dib", ".emf", ".gif", ".jfif", ".jpe", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".wmf",
        ".webp", ".heic", ".heif", ".avif",
    ];

    public static readonly string[] Audio =
    [
        ".aif", ".aifc", ".aiff", ".asf", ".au", ".mp2", ".mp3", ".mpa", ".snd", ".wav", ".wma",
        ".m4a", ".aac", ".flac", ".ogg", ".oga", ".opus",
    ];

    public static IEnumerable<string> All => Video.Concat(Pictures).Concat(Audio).Distinct(StringComparer.Ordinal);

    public static bool IsPictureExtension(string path) => Pictures.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static bool IsSupported(string path) => All.Contains(Path.GetExtension(path).ToLowerInvariant());
}
