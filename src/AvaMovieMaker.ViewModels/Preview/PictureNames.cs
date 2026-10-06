using System.Globalization;
using System.Text;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.ViewModels.Preview;

public static class PictureNames
{
    public const string Fallback = "VideoTape";

    public static string Clean(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name.Trim())
        {
            sb.Append(c < 32 || "\\/:*?\"<>|".Contains(c, StringComparison.Ordinal) ? '_' : c);
        }

        return sb.Length == 0 ? Fallback : sb.ToString();
    }

    public static string? Suggest(string folder, string name)
    {
        string clean = Clean(name);
        for (int n = 1; n <= 9999; n++)
        {
            string file = string.Create(CultureInfo.InvariantCulture, $"{clean}_{n:0000}.jpg");
            if (!FileStore.Current.Exists(Path.Combine(folder, file)))
            {
                return file;
            }
        }

        return null;
    }

    public static bool IsJpeg(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpe", StringComparison.OrdinalIgnoreCase);
    }
}
