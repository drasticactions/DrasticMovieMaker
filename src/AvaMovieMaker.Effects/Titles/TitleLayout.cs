using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace AvaMovieMaker.Effects.Titles;

public static class TitleLayout
{
    public const float BaseSize = 0.17f;

    public const float SecondLineScale = 0.58f;

    public const float LinePitch = 1.47f;

    public const float TwoLineCenter = 0.42f;

    public const float WrapWidth = 0.73f;

    public const float StepFactor = 1.15f;

    public static float FontSize(TitleFont font, int shortSide, float relative = 1f) =>
        shortSide * BaseSize * relative * MathF.Pow(StepFactor, Math.Clamp(font.SizeStep, -6, 10));

    public static TextLine Shape(string text, TitleFont font, float size)
    {
        SKTypeface tf = TitleFonts.Resolve(font);
        var skFont = new SKFont(tf, size)
        {
            Subpixel = true,
            Edging = SKFontEdging.Antialias,
            Embolden = font.Bold && !tf.IsBold,
            SkewX = font.Italic && !tf.IsItalic ? -0.2f : 0f,
        };
        using var shaper = new SKShaper(tf);
        SKShaper.Result r = shaper.Shape(text, skFont);
        int n = r.Codepoints.Length;
        var glyphs = new ushort[n];
        var pos = new SKPoint[n];
        var clusters = new int[n];
        for (int i = 0; i < n; i++)
        {
            glyphs[i] = (ushort)r.Codepoints[i];
            pos[i] = r.Points[i];
            clusters[i] = (int)r.Clusters[i];
        }

        return new TextLine
        {
            Text = text,
            Font = skFont,
            Glyphs = glyphs,
            Positions = pos,
            Clusters = clusters,
            Width = r.Width,
            Underline = font.Underline,
            NominalSize = size,
        };
    }

    public static List<TextLine> Wrap(string text, TitleFont font, float size, float maxWidth)
    {
        var lines = new List<TextLine>();
        foreach (string paragraph in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var words = new Queue<string>(paragraph.Split(' '));
            string current = string.Empty;
            while (words.Count > 0)
            {
                string w = words.Dequeue();
                string candidate = current.Length == 0 ? w : current + " " + w;
                if (Shape(candidate, font, size).Width <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(Shape(current, font, size));
                    current = string.Empty;
                    words = new Queue<string>(words.Prepend(w));
                    continue;
                }

                int fit = 1;
                while (fit < w.Length && Shape(w[..(fit + 1)], font, size).Width <= maxWidth)
                {
                    fit++;
                }

                lines.Add(Shape(w[..fit], font, size));
                if (fit < w.Length)
                {
                    words = new Queue<string>(words.Prepend(w[fit..]));
                }
            }

            if (current.Length > 0 || lines.Count == 0)
            {
                lines.Add(Shape(current, font, size));
            }
        }

        return lines;
    }
}
