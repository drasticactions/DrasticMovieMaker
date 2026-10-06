using System.Reflection;

namespace AvaMovieMaker.Assets;

public static class AssetFonts
{
    private const string Prefix = "AvaMovieMaker.Assets.Fonts.";

    public static Stream? Open(string fileName) => typeof(AssetFonts).Assembly.GetManifestResourceStream(Prefix + fileName);

    public static IEnumerable<Stream> OpenAll()
    {
        Assembly a = typeof(AssetFonts).Assembly;
        foreach (string name in a.GetManifestResourceNames())
        {
            if (name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                Stream? s = a.GetManifestResourceStream(name);
                if (s is not null)
                {
                    yield return s;
                }
            }
        }
    }
}
