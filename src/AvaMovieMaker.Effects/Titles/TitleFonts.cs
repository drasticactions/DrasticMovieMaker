using SkiaSharp;

namespace AvaMovieMaker.Effects.Titles;

public static class TitleFonts
{
    public const string DefaultFamily = "Kalam";
    public const string FallbackFamily = "Liberation Sans";

    private static readonly Lock Gate = new();
    private static readonly Dictionary<(string Family, bool Bold, bool Italic), SKTypeface> Registered = new();
    private static readonly Dictionary<string, string> Substitutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = "Liberation Sans",
        ["Times New Roman"] = "Liberation Serif",
        ["Bodoni"] = "Libre Bodoni",
        ["Bodoni MT"] = "Libre Bodoni",
        ["Eurostile"] = "Michroma",
        ["Secret Service Typewriter"] = "Courier Prime",
        ["Segoe UI"] = "Selawik",
        ["Segoe Print"] = "Kalam",
    };

    public static string DisplayFamily(string family)
    {
        bool installed;
        lock (Gate)
        {
            installed = Registered.Keys.Any(k => string.Equals(k.Family, family, StringComparison.OrdinalIgnoreCase));
        }

        installed = installed || Installed.Families.Contains(family, StringComparer.OrdinalIgnoreCase);
        return installed ? family : Substitutes.GetValueOrDefault(family, FallbackFamily);
    }

    public static event Action<string>? MissingFamily;

    private static readonly HashSet<string> Reported = new(StringComparer.OrdinalIgnoreCase);

    public static bool PreferRegisteredOriginals { get; set; }

    public static ITitleFontSource Installed { get; } = new SystemTitleFonts();

    public static void Register(Stream stream)
    {
        SKTypeface? tf = SKTypeface.FromStream(stream);
        if (tf is null)
        {
            return;
        }

        lock (Gate)
        {
            bool bold = tf.FontWeight >= (int)SKFontStyleWeight.SemiBold;
            bool italic = tf.FontSlant != SKFontStyleSlant.Upright;
            Registered[(tf.FamilyName, bold, italic)] = tf;
        }
    }

    public static void Register(string path)
    {
        using FileStream s = File.OpenRead(path);
        Register(s);
    }

    public static IReadOnlyList<string> Families
    {
        get
        {
            var list = new List<string>();
            lock (Gate)
            {
                list.AddRange(Registered.Keys.Select(k => k.Family).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));
            }

            foreach (string f in Installed.Families.Order(StringComparer.OrdinalIgnoreCase))
            {
                if (!list.Contains(f, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(f);
                }
            }

            return list;
        }
    }

    public static SKTypeface Resolve(TitleFont font) => Resolve(font.Family, font.Bold, font.Italic);

    public static SKTypeface Resolve(string family, bool bold, bool italic)
    {
        if (PreferRegisteredOriginals && Find(family, bold, italic) is { } original)
        {
            return original;
        }

        string name = Substitutes.GetValueOrDefault(family, family);
        if (Find(name, bold, italic) is { } registered)
        {
            return registered;
        }

        if (Installed.Match(name, bold, italic) is { } sys)
        {
            return sys;
        }

        lock (Gate)
        {
            if (Reported.Add(family))
            {
                MissingFamily?.Invoke(family);
            }
        }

        return Find(FallbackFamily, bold, italic)
               ?? Installed.Match(FallbackFamily, bold, italic)
               ?? SKTypeface.Default;
    }

    private static SKTypeface? Find(string family, bool bold, bool italic)
    {
        lock (Gate)
        {
            if (Registered.TryGetValue((family, bold, italic), out SKTypeface? exact))
            {
                return exact;
            }

            foreach (((string f, bool b, bool i), SKTypeface tf) in Registered)
            {
                if (string.Equals(f, family, StringComparison.OrdinalIgnoreCase) && i == italic)
                {
                    return tf;
                }
            }

            foreach (((string f, _, _), SKTypeface tf) in Registered)
            {
                if (string.Equals(f, family, StringComparison.OrdinalIgnoreCase))
                {
                    return tf;
                }
            }
        }

        return null;
    }
}

public interface ITitleFontSource
{
    IReadOnlyList<string> Families { get; }

    SKTypeface? Match(string family, bool bold, bool italic);
}

public sealed class SystemTitleFonts : ITitleFontSource
{
    public IReadOnlyList<string> Families => [.. SKFontManager.Default.FontFamilies];

    public SKTypeface? Match(string family, bool bold, bool italic)
    {
        var style = new SKFontStyle(bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        SKTypeface? tf = SKFontManager.Default.MatchFamily(family, style);
        return tf is not null && string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase) ? tf : null;
    }
}
