using AvaMovieMaker.Effects.Catalog;
using SkiaSharp;

namespace AvaMovieMaker.Effects.Titles;

public static class TitleAnimator
{
    private static readonly SKSamplingOptions Linear = new(SKFilterMode.Linear, SKMipmapMode.None);

    public static void Draw(SKCanvas canvas, int width, int height, TitleContent content, double t, double duration, SKImage? video, bool fullFrame)
    {
        TitleAnimationInfo info = TitleAnimationCatalog.Get(content.AnimationId);
        var s = new Scene(canvas, width, height, content, info, t, Math.Max(duration, 0.04), video);
        s.DrawBackground(fullFrame);
        if (info.IsCredits)
        {
            Credits(s);
        }
        else
        {
            Title(s);
        }
    }

    private static void Title(Scene s)
    {
        float w = s.W, h = s.H;
        float ein = s.In, eout = s.Out;
        switch (s.Info.Kind)
        {
            case "static":
                s.Block(s.Main, w / 2, h / 2, 1);
                break;
            case "fade":
                s.Block(s.Main, w / 2, s.MainY, Math.Min(ein, eout));
                s.Block(s.Second, w / 2, s.SecondY, Math.Min(Delay(s, 0.25), eout));
                break;
            case "fly-in-fade":
                s.Block(s.Main, w / 2 - (1 - EaseOut(ein)) * w, s.MainY, eout);
                s.Block(s.Second, w / 2 - (1 - EaseOut(Delay(s, 0.15))) * w, s.SecondY, eout);
                break;
            case "fly-out":
                s.Block(s.Main, w / 2 + EaseIn(1 - eout) * w, s.MainY, ein);
                s.Block(s.Second, w / 2 + EaseIn(1 - eout) * w * 1.2f, s.SecondY, ein);
                break;
            case "fly-in-out":
                s.Block(s.Main, w / 2 - (1 - EaseOut(ein)) * w + EaseIn(1 - eout) * w, s.MainY, 1);
                s.Block(s.Second, w / 2 - (1 - EaseOut(ein)) * w * 1.2f + EaseIn(1 - eout) * w * 1.2f, s.SecondY, 1);
                break;
            case "fly-left-right":
                s.Block(s.Main, w / 2 - (1 - EaseOut(ein)) * w, s.MainY, eout);
                s.Block(s.Second, w / 2 + (1 - EaseOut(ein)) * w, s.SecondY, eout);
                break;
            case "layered":
            {
                float p = s.Progress;
                s.Block(s.Main, w * (0.3f + 0.4f * p), h * 0.42f, Math.Min(ein, eout), 1.2f);
                s.Block(s.Second, w * (0.7f - 0.4f * p), h * 0.6f, Math.Min(ein, eout) * 0.8f);
                break;
            }

            case "fly-top-left":
            {
                float k = 1 - EaseOut(ein);
                s.Block(s.Main, w / 2 - k * w * 0.8f, h / 2 - k * h * 0.8f, eout);
                break;
            }

            case "typewriter":
            {
                float reveal = Math.Clamp((float)(s.T / (s.D * 0.5)), 0, 1);
                s.Block(s.Main, w / 2, h / 2, eout, reveal: reveal);
                break;
            }

            case "ticker":
            {
                float band = s.S * 0.12f;
                s.Band(h - band * 1.5f, band, 0xC0000000);
                float total = w + s.MainWidth;
                s.BlockLeft(s.Main, w - s.Progress * total, h - band, 1);
                break;
            }

            case "news-banner":
            {
                float band = s.S * 0.14f;
                float drop = EaseOut(ein);
                s.Band(h - band * 1.6f, band * drop, 0xE0102A6A);
                float total = w + s.MainWidth;
                s.BlockLeft(s.Main, w - s.Progress * total * 0.9f, h - band * 1.1f, drop);
                break;
            }

            case "perspective":
                s.Perspective(s.Main, s.Progress);
                break;
            case "flashing":
                s.Block(s.Main, w / 2, h / 2, ((int)(s.T / 0.25) % 2) == 0 ? 1 : 0);
                break;
            case "zoom-out":
                s.Block(s.Main, w / 2, h / 2, Math.Min(ein, eout), 1 + 2 * (1 - EaseOut(ein)));
                break;
            case "zoom-in":
                s.Block(s.Main, w / 2, h / 2, eout, EaseOut(ein));
                break;
            case "spin-in":
                s.Block(s.Main, w / 2, h / 2, eout, EaseOut(ein), rotation: (1 - EaseOut(ein)) * 360);
                break;
            case "spin-out":
                s.Block(s.Main, w / 2, h / 2, ein, EaseOut(eout), rotation: (1 - eout) * 360);
                break;
            case "news-inset":
                s.BlockRight(s.Main, w * 0.92f, h * 0.82f, Math.Min(ein, eout), 0.8f);
                break;
            case "slow-zoom":
                s.Block(s.Main, w / 2, h / 2, Math.Min(ein, eout), 1 + 0.2f * s.Progress);
                break;
            case "zoom-up":
            {
                float k = EaseOut(ein);
                s.Block(s.Main, w / 2, h / 2 + (1 - k) * h * 0.4f, eout, 0.2f + 0.8f * k);
                break;
            }

            case "stretch":
                s.Block(s.Main, w / 2, h / 2, eout, 1, scaleY: EaseOut(ein));
                break;
            case "subtitle":
                s.Block(s.Main, w / 2, h * 0.86f, Math.Min(ein, eout), 0.6f);
                break;
            case "video-in-text":
                s.VideoInText(Math.Min(ein, eout));
                break;
            case "wow":
            {
                float k = ein;
                float scale = k < 0.6f ? EaseOut(k / 0.6f) * 1.25f : 1.25f - 0.25f * EaseOut((k - 0.6f) / 0.4f);
                s.Block(s.Main, w / 2, h / 2, eout, scale);
                break;
            }

            case "fade-wipe":
                s.Wipe(s.Main, ein, eout, bounce: false);
                break;
            case "bounce-wipe":
                s.Wipe(s.Main, ein, eout, bounce: true);
                break;
            case "ellipse-wipe":
                s.EllipseWipe(s.Main, ein, eout);
                break;
            case "mirror":
            {
                float k = 1 - EaseOut(ein);
                s.Block(s.Main, w / 2 - k * w * 0.6f, s.MainY, eout);
                s.Reflection(s.Main, w / 2 + k * w * 0.6f, s.MainY, eout);
                break;
            }

            case "scroll-banner":
            {
                float total = w + s.MainWidth;
                s.BlockLeft(s.Main, w - s.Progress * total, h / 2, Math.Min(ein, eout));
                break;
            }

            case "scroll-inverted":
            {
                float total = w + s.MainWidth;
                s.BlockLeft(s.Main, -s.MainWidth + s.Progress * total, h / 2, Math.Min(ein, eout));
                break;
            }

            case "explode":
                s.Explode(s.Main, w / 2, s.MainY, ein, eout);
                s.Block(s.Second, w / 2, s.SecondY, Math.Min(Delay(s, 0.5), eout));
                break;
            case "paint-drip":
                s.PaintDrip(s.Main, s.Progress, eout);
                break;
            case "scoreboard":
            {
                float k = EaseOut(ein) * eout;
                float band = s.S * 0.11f;
                float y = -band + k * band * 1.4f;
                s.Band(y, band, s.Content.BannerColor ?? 0xE0202020);
                s.BlockLeft(s.Main, w * 0.05f, y + band * 0.5f, k, 0.6f);
                s.BlockRight(s.Second, w * 0.95f, y + band * 0.5f, k, 0.9f);
                break;
            }

            case "newspaper":
                s.Newspaper(ein, eout);
                break;
            default:
                s.Block(s.Main, w / 2, h / 2, Math.Min(ein, eout));
                break;
        }
    }

    private static void Credits(Scene s)
    {
        IReadOnlyList<CreditRow> rows = s.Content.Credits.Count > 0
            ? s.Content.Credits
            : s.Content.Lines.Select(l => new CreditRow(l, string.Empty)).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        float w = s.W, h = s.H;
        float[]? rect = s.Info.VideoRect;
        float x0 = 0, y0 = 0, x1 = w, y1 = h;
        if (rect is not null)
        {
            if (rect[2] < 1)
            {
                x0 = w * (rect[0] + rect[2]);
            }
            else
            {
                y0 = h * (rect[1] + rect[3]);
            }
        }

        float cx = (x0 + x1) / 2;
        switch (s.Info.Kind)
        {
            case "credits-scroll":
            case "credits-scroll-side":
            {
                bool side = s.Info.Kind == "credits-scroll-side";
                float lineH = s.S * 0.09f;
                float block = rows.Count * lineH * (side ? 1.2f : 2.2f);
                float areaH = y1 - y0;
                float top = y0 + areaH * 1.05f - s.Progress * (areaH * 1.1f + block);
                canvasClip(s, x0, y0, x1, y1, () =>
                {
                    float y = top;
                    for (int ri = 0; ri < rows.Count; ri++)
                    {
                        CreditRow r = rows[ri];
                        if (side)
                        {
                            s.BlockRight(s.HeadingOf(rows, ri), cx - w * 0.02f, y, 1, 0.75f);
                            s.BlockLeft(s.Names(r.Names), cx + w * 0.02f, y, 1, 0.75f);
                            y += lineH * 1.2f;
                        }
                        else
                        {
                            s.Block(s.HeadingOf(rows, ri), cx, y, 1, 0.85f);
                            s.Block(s.Names(r.Names), cx, y + lineH, 1, 0.75f);
                            y += lineH * 2.2f;
                        }
                    }
                });
                break;
            }

            default:
            {
                double slot = s.D / rows.Count;
                int i = Math.Clamp((int)(s.T / slot), 0, rows.Count - 1);
                double lt = s.T - i * slot;
                double e = Math.Min(s.Info.Entrance, slot / 3), x = Math.Min(s.Info.Exit, slot / 3);
                float ein = e > 0 ? (float)Math.Clamp(lt / e, 0, 1) : 1;
                float eout = x > 0 ? (float)Math.Clamp((slot - lt) / x, 0, 1) : 1;
                CreditRow r = rows[i];
                TextLine[] heading = s.HeadingOf(rows, i);
                TextLine[] names = s.Names(r.Names);
                switch (s.Info.Kind)
                {
                    case "credits-zoom":
                        s.Block(heading, cx, h * 0.42f, Math.Min(ein, eout), 1 + 9 * (1 - EaseOut(ein)));
                        s.Block(names, cx, h * 0.58f, Math.Min(ein, eout), (1 + 9 * (1 - EaseOut(ein))) * 0.85f);
                        break;
                    case "credits-fly":
                        s.Block(heading, cx - (1 - EaseOut(ein)) * w, h * 0.42f, eout);
                        s.Block(names, cx + (1 - EaseOut(ein)) * w, h * 0.58f, eout, 0.85f);
                        break;
                    case "credits-mirror":
                    {
                        float k = (1 - EaseOut(ein)) - (1 - eout);
                        s.Block(heading, cx - k * w, h * 0.4f, Math.Min(ein, eout));
                        s.Block(names, cx - k * w, h * 0.52f, Math.Min(ein, eout), 0.85f);
                        s.Reflection(names, cx - k * w, h * 0.52f, Math.Min(ein, eout) * 0.8f);
                        break;
                    }

                    case "credits-explode":
                        s.Explode(heading, cx, h * 0.42f, ein, eout);
                        s.Block(names, cx, h * 0.58f, Math.Min(ein, eout), 0.85f);
                        break;
                    default:
                        s.Block(heading, cx, h * 0.42f, Math.Min(ein, eout));
                        s.Block(names, cx, h * 0.58f, Math.Min(ein, eout), 0.85f);
                        break;
                }

                break;
            }
        }

        static void canvasClip(Scene s, float x0, float y0, float x1, float y1, Action draw)
        {
            s.Canvas.Save();
            s.Canvas.ClipRect(new SKRect(x0, y0, x1, y1));
            draw();
            s.Canvas.Restore();
        }
    }

    private static float Delay(Scene s, double delay)
    {
        double e = s.Info.Entrance;
        return e <= 0 ? 1 : (float)Math.Clamp((s.T - delay) / e, 0, 1);
    }

    internal static float EaseOut(float t) => 1 - (1 - t) * (1 - t);

    internal static float EaseIn(float t) => t * t;

    private sealed class Scene
    {
        public Scene(SKCanvas canvas, int width, int height, TitleContent content, TitleAnimationInfo info, double t, double duration, SKImage? video)
        {
            Canvas = canvas;
            W = width;
            H = height;
            S = Math.Min(width, height);
            Content = content;
            Info = info;
            T = Math.Clamp(t, 0, duration);
            D = duration;
            Video = video;
            double e = info.Entrance, x = info.Exit;
            if (e + x > D)
            {
                double k = D / (e + x);
                e *= k;
                x *= k;
            }

            In = e > 0 ? (float)Math.Clamp(T / e, 0, 1) : 1;
            Out = x > 0 ? (float)Math.Clamp((D - T) / x, 0, 1) : 1;
            Alpha = Math.Clamp((100 - content.Transparency) / 100f, 0, 1);
            float size = TitleLayout.FontSize(content.Font, (int)S);
            string first = content.Lines.Count > 0 ? content.Lines[0] : string.Empty;
            string second = info.TwoLines && content.Lines.Count > 1 ? content.Lines[1] : string.Empty;
            Main = Lines(first, size);
            Second = Lines(second, size * TitleLayout.SecondLineScale);

            float mainH = BlockHeight(Main), secondH = BlockHeight(Second);
            float top = H * TitleLayout.TwoLineCenter - (mainH + secondH) / 2;
            MainY = top + mainH / 2;
            SecondY = top + mainH + secondH / 2;
        }

        public float MainY { get; }

        public float SecondY { get; }

        private static float BlockHeight(TextLine[] lines) => lines.Sum(l => l.Pitch);

        public SKCanvas Canvas { get; }

        public float W { get; }

        public float H { get; }

        // Text and stroke sizes follow the short side so portrait frames get text that fits their width.
        public float S { get; }

        public TitleContent Content { get; }

        public TitleAnimationInfo Info { get; }

        public double T { get; }

        public double D { get; }

        public SKImage? Video { get; }

        public float In { get; }

        public float Out { get; }

        public float Alpha { get; }

        public float Progress => (float)(T / D);

        public TextLine[] Main { get; }

        public TextLine[] Second { get; }

        public float MainWidth => Main.Length == 0 ? 0 : Main.Max(l => l.Width);

        private TextLine[] Lines(string text, float size)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            return [.. TitleLayout.Wrap(text, Content.Font, size, W * TitleLayout.WrapWidth)];
        }

        public TextLine[] Heading(string text) => Lines(text, TitleLayout.FontSize(Content.Font, (int)S, 0.6f * 16 / 25));

        public TextLine[] Names(string text) => Lines(text, TitleLayout.FontSize(Content.Font, (int)S, 0.6f));

        public TextLine[] CreditTitle(string text) => Lines(text, TitleLayout.FontSize(Content.Font, (int)S, 0.6f * 30 / 25));

        public TextLine[] HeadingOf(IReadOnlyList<CreditRow> rows, int index) =>
            index == 0 && rows[0].Names.Length == 0 ? CreditTitle(rows[0].Heading) : Heading(rows[index].Heading);

        public void DrawBackground(bool fullFrame)
        {
            if (fullFrame)
            {
                Canvas.Clear(new SKColor(Content.BackgroundColor));
                return;
            }

            if (Info.VideoRect is { } r && Video is not null)
            {
                Canvas.Clear(SKColors.Black);
                if (Info.Kind != "newspaper")
                {
                    DrawVideoIn(new SKRect(W * r[0], H * r[1], W * (r[0] + r[2]), H * (r[1] + r[3])));
                }
            }
        }

        private void DrawVideoIn(SKRect box)
        {
            if (Video is null)
            {
                return;
            }

            float s = Math.Min(box.Width / Video.Width, box.Height / Video.Height);
            float w = Video.Width * s, h = Video.Height * s;
            Canvas.DrawImage(Video, SKRect.Create(box.MidX - w / 2, box.MidY - h / 2, w, h), Linear);
        }

        private SKPaint TextPaint(float alpha)
        {
            var c = new SKColor(Content.TextColor);
            return new SKPaint
            {
                IsAntialias = true,
                Color = c.WithAlpha((byte)Math.Clamp(c.Alpha * alpha * Alpha, 0, 255)),
            };
        }

        public void Block(TextLine[] lines, float cx, float cy, float alpha, float scale = 1, float rotation = 0, float scaleY = 1, float reveal = 1)
        {
            if (lines.Length == 0 || alpha <= 0 || scale <= 0.001f || scaleY <= 0.001f)
            {
                return;
            }

            float total = lines.Sum(l => l.Pitch);
            float maxW = lines.Max(l => l.Width);
            Canvas.Save();
            Canvas.Translate(cx, cy);
            Canvas.RotateDegrees(rotation);
            Canvas.Scale(scale, scale * scaleY);
            float y = -total / 2;
            int glyphBudget = reveal >= 1 ? int.MaxValue : (int)Math.Round(reveal * lines.Sum(l => l.Glyphs.Length));
            foreach (TextLine line in lines)
            {
                float x = Content.Alignment switch
                {
                    TitleAlignment.Left => -maxW / 2,
                    TitleAlignment.Right => maxW / 2 - line.Width,
                    _ => -line.Width / 2,
                };
                int count = Math.Min(glyphBudget, line.Glyphs.Length);
                DrawLine(line, x, y + line.BaselineInPitch, alpha, count);
                glyphBudget -= count;
                y += line.Pitch;
            }

            Canvas.Restore();
        }

        public void BlockLeft(TextLine[] lines, float left, float cy, float alpha, float scale = 1)
        {
            if (lines.Length == 0)
            {
                return;
            }

            float wmax = lines.Max(l => l.Width) * scale;
            Block(lines, left + wmax / 2, cy, alpha, scale);
        }

        public void BlockRight(TextLine[] lines, float right, float cy, float alpha, float scale = 1)
        {
            if (lines.Length == 0)
            {
                return;
            }

            float wmax = lines.Max(l => l.Width) * scale;
            Block(lines, right - wmax / 2, cy, alpha, scale);
        }

        private void DrawLine(TextLine line, float x, float baseline, float alpha, int glyphs)
        {
            using SKTextBlob? blob = line.Blob(glyphs);
            if (blob is null)
            {
                return;
            }

            float unit = S * 0.004f;
            if (Info.Shadow)
            {
                using var shadow = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha((byte)(150 * alpha * Alpha)) };
                Canvas.DrawText(blob, x + unit, baseline + unit, shadow);
            }

            using SKPaint paint = TextPaint(alpha);
            if (Info.Outline)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = unit;
            }

            Canvas.DrawText(blob, x, baseline, paint);
            if (line.Underline)
            {
                float uy = baseline + line.Descent * 0.4f;
                Canvas.DrawRect(new SKRect(x, uy, x + line.Width, uy + unit), paint);
            }
        }

        public void Band(float top, float height, uint argb)
        {
            if (height <= 0)
            {
                return;
            }

            using var paint = new SKPaint { Color = new SKColor(argb) };
            Canvas.DrawRect(new SKRect(0, top, W, top + height), paint);
        }

        public void Reflection(TextLine[] lines, float cx, float cy, float alpha)
        {
            if (lines.Length == 0)
            {
                return;
            }

            float total = lines.Sum(l => l.Pitch);
            Canvas.Save();
            Canvas.Translate(0, cy + total * 1.05f);
            Canvas.Scale(1, -1);
            Canvas.Translate(0, -cy);
            Block(lines, cx, cy, alpha * 0.35f);
            Canvas.Restore();
        }

        public void Perspective(TextLine[] lines, float progress)
        {
            float y = H * (1.2f - 1.6f * progress);
            float depth = Math.Max(0.05f, 1 - progress * 0.85f);
            Canvas.Save();
            Canvas.Translate(W / 2, y);
            Canvas.Scale(depth, depth);
            Block(lines, 0, 0, Math.Min(In, Out));
            Canvas.Restore();
        }

        public void Wipe(TextLine[] lines, float ein, float eout, bool bounce)
        {
            float k = bounce ? Bounce(ein) : ein;
            Canvas.Save();
            float edge = W * (k * 1.2f - 0.1f);
            Canvas.ClipRect(new SKRect(0, 0, Math.Max(0, edge), H));
            Block(lines, W / 2, H / 2, eout);
            Canvas.Restore();
        }

        public void EllipseWipe(TextLine[] lines, float ein, float eout)
        {
            float rx = W * 0.75f * ein, ry = H * 0.75f * ein;
            if (rx <= 0)
            {
                return;
            }

            using var oval = new SKPathBuilder();
            oval.AddOval(new SKRect(W / 2 - rx, H / 2 - ry, W / 2 + rx, H / 2 + ry));
            using SKPath path = oval.Detach();
            Canvas.Save();
            Canvas.ClipPath(path, antialias: true);
            Block(lines, W / 2, H / 2, eout);
            Canvas.Restore();
        }

        public void Explode(TextLine[] lines, float cx, float cy, float ein, float eout)
        {
            for (int i = 3; i >= 1; i--)
            {
                float k = Math.Clamp(ein * 1.5f - i * 0.15f, 0, 1);
                Block(lines, cx, cy, (1 - k) * 0.6f * eout, 0.5f + k * (1 + i), 0);
            }

            Block(lines, cx, cy, Math.Min(ein, eout), 0.5f + 0.5f * EaseOut(ein));
        }

        public void PaintDrip(TextLine[] lines, float progress, float eout)
        {
            if (lines.Length == 0)
            {
                return;
            }

            float total = lines.Sum(l => l.Pitch);
            float top = H / 2 - total / 2;
            float reveal = Math.Clamp(progress * 3, 0, 1);
            using var columns = new SKPathBuilder();
            int cols = 40;
            for (int i = 0; i < cols; i++)
            {
                float jitter = (float)((Math.Sin(i * 12.9898) * 43758.5453) % 1 + 1) % 1;
                float colReveal = Math.Clamp(reveal * (1.2f + jitter * 0.6f) - jitter * 0.2f, 0, 1);
                columns.AddRect(new SKRect(W * i / cols, 0, W * (i + 1) / cols + 1, top + total * colReveal + 2));
            }

            using SKPath clip = columns.Detach();
            Canvas.Save();
            Canvas.ClipPath(clip);
            Block(lines, W / 2, H / 2, eout);
            Canvas.Restore();
        }

        public void VideoInText(float alpha)
        {
            if (Main.Length == 0)
            {
                return;
            }

            if (Video is null)
            {
                Block(Main, W / 2, H / 2, alpha, 1.6f);
                return;
            }

            Canvas.Clear(SKColors.Black);
            using var shader = Video.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Linear);
            float scale = 1.6f;
            TextLine line = Main[0];
            using var letters = new SKPathBuilder();
            using SKTextBlob? blob = line.Blob();
            for (int i = 0; i < line.Glyphs.Length; i++)
            {
                using SKPath? g = line.Font.GetGlyphPath(line.Glyphs[i]);
                if (g is not null)
                {
                    g.Transform(SKMatrix.CreateTranslation(line.Positions[i].X, line.Positions[i].Y));
                    letters.AddPath(g);
                }
            }

            using SKPath path = letters.Detach();
            path.Transform(SKMatrix.CreateTranslation(-line.Width / 2, line.Ascent - line.Height / 2).PostConcat(SKMatrix.CreateScale(scale, scale)).PostConcat(SKMatrix.CreateTranslation(W / 2, H / 2)));
            using var paint = new SKPaint { IsAntialias = true, Shader = shader, Color = SKColors.White.WithAlpha((byte)(255 * alpha * Alpha)) };
            Canvas.DrawPath(path, paint);
        }

        public void Newspaper(float ein, float eout)
        {
            float k = EaseOut(ein);
            float scale = 0.01f + 0.99f * k;
            Canvas.Save();
            Canvas.Translate(W / 2, H / 2);
            Canvas.RotateDegrees((1 - k) * 720);
            Canvas.Scale(scale);
            Canvas.Translate(-W / 2, -H / 2);
            var page = new SKRect(W * 0.075f, H * 0.075f, W * 0.925f, H * 0.925f);
            using var paper = new SKPaint { Color = new SKColor(0xFFF4F1E8) };
            Canvas.DrawRect(page, paper);
            using var ink = new SKPaint { Color = new SKColor(0xFF505050), StrokeWidth = S * 0.004f };
            Canvas.DrawLine(page.Left + W * 0.03f, page.Top + H * 0.22f, page.Right - W * 0.03f, page.Top + H * 0.22f, ink);
            for (int i = 0; i < 9; i++)
            {
                float y = page.Top + H * (0.36f + i * 0.05f);
                Canvas.DrawLine(page.Left + W * 0.03f, y, page.Left + W * 0.28f, y, ink);
            }

            if (Video is not null && Info.VideoRect is { } r)
            {
                DrawVideoIn(new SKRect(W * r[0], H * r[1], W * (r[0] + r[2]), H * (r[1] + r[3])));
            }

            Block(Main, W / 2, page.Top + H * 0.13f, eout, 0.8f);
            BlockLeft(Second, page.Left + W * 0.03f, page.Top + H * 0.29f, eout, 0.8f);
            Canvas.Restore();
        }

        private static float Bounce(float t)
        {
            if (t < 1 / 2.75f)
            {
                return 7.5625f * t * t;
            }

            if (t < 2 / 2.75f)
            {
                t -= 1.5f / 2.75f;
                return 7.5625f * t * t + 0.75f;
            }

            if (t < 2.5f / 2.75f)
            {
                t -= 2.25f / 2.75f;
                return 7.5625f * t * t + 0.9375f;
            }

            t -= 2.625f / 2.75f;
            return 7.5625f * t * t + 0.984375f;
        }
    }
}
