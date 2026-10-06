using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using AvaMovieMaker.Assets;

namespace AvaMovieMaker;

public sealed class BundledFonts : FontCollectionBase
{
    public static readonly Uri CollectionKey = new("fonts:AvaMovieMaker", UriKind.Absolute);

    public static readonly FontFamily Serif = new("fonts:AvaMovieMaker#Liberation Serif");

    public BundledFonts()
    {
        foreach (string file in (string[])["LiberationSerif-Bold.ttf", "LiberationSerif-BoldItalic.ttf"])
        {
            using Stream? s = AssetFonts.Open(file);
            if (s is not null)
            {
                var copy = new MemoryStream();
                s.CopyTo(copy);
                copy.Position = 0;
                TryAddGlyphTypeface(copy, out _);
            }
        }
    }

    public override Uri Key => CollectionKey;
}

public static class BundledFontsAppBuilderExtensions
{
    public static AppBuilder WithBundledFonts(this AppBuilder builder) =>
        builder.ConfigureFonts(fontManager => fontManager.AddFontCollection(new BundledFonts()));
}
