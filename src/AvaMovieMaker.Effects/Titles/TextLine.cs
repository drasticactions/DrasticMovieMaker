using SkiaSharp;

namespace AvaMovieMaker.Effects.Titles;

public sealed class TextLine
{
    public required string Text { get; init; }

    public required SKFont Font { get; init; }

    public required ushort[] Glyphs { get; init; }

    public required SKPoint[] Positions { get; init; }

    public required int[] Clusters { get; init; }

    public required float Width { get; init; }

    public float Ascent => -Font.Metrics.Ascent;

    public float Descent => Font.Metrics.Descent;

    public float Height => Ascent + Descent;

    public float Pitch => NominalSize * TitleLayout.LinePitch;

    public float NominalSize { get; init; }

    public float BaselineInPitch
    {
        get
        {
            float cap = Font.Metrics.CapHeight > 0 ? Font.Metrics.CapHeight : Ascent * 0.7f;
            return (Pitch + cap) / 2;
        }
    }

    public bool Underline { get; init; }

    public SKTextBlob? Blob(int glyphCount = -1)
    {
        int n = glyphCount < 0 ? Glyphs.Length : Math.Min(glyphCount, Glyphs.Length);
        if (n == 0)
        {
            return null;
        }

        using var builder = new SKTextBlobBuilder();
        SKPositionedRunBuffer run = builder.AllocatePositionedRun(Font, n);
        Glyphs.AsSpan(0, n).CopyTo(run.Glyphs);
        Positions.AsSpan(0, n).CopyTo(run.Positions);
        return builder.Build();
    }
}
