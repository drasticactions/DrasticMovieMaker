using AvaMovieMaker.Effects;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Plan;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Tests;

// Renders every effect, transition and title animation at each project frame size. Set AMM_RENDER_DIR to write the
// frames there for review; also set AMM_RENDER_REF to a directory written earlier to require the landscape frames to
// match it pixel for pixel.
public class AspectRenderTests
{
    private static readonly (int Width, int Height)[] Landscape = [(640, 480), (854, 480)];
    private static readonly (int Width, int Height)[] Other = [(480, 854), (480, 480), (480, 600)];

    public static TheoryData<int, int> Sizes()
    {
        var d = new TheoryData<int, int>();
        foreach ((int w, int h) in Landscape.Concat(Other))
        {
            d.Add(w, h);
        }

        return d;
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void RendersEverythingAtProjectSize(int width, int height)
    {
        string? dir = Environment.GetEnvironmentVariable("AMM_RENDER_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set AMM_RENDER_DIR to render every effect at each project size.");
        string? reference = Environment.GetEnvironmentVariable("AMM_RENDER_REF");
        bool compare = !string.IsNullOrEmpty(reference) && Landscape.Contains((width, height));
        string size = $"{width}x{height}";
        string outDir = Path.Combine(dir!, size);
        Directory.CreateDirectory(outDir);

        var ctx = new RenderContext(Cards.Cpu, width, height);
        using SKImage a = Cards.A(ctx);
        using SKImage b = Cards.B(ctx);
        var mismatches = new List<string>();

        void Save(SKImage img, string name)
        {
            using SKData png = img.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), png.ToArray());
            if (compare)
            {
                string refPath = Path.Combine(reference!, size, name + ".png");
                if (!File.Exists(refPath))
                {
                    mismatches.Add($"{name} (no reference)");
                    return;
                }

                using SKBitmap expected = SKBitmap.Decode(refPath);
                using var conv = new SKBitmap(new SKImageInfo(expected.Width, expected.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                expected.CopyTo(conv, SKColorType.Rgba8888);
                int max = Cards.MaxDiff(Cards.Pixels(img), conv.Bytes);
                if (max > 0)
                {
                    mismatches.Add($"{name} (max {max})");
                }
            }
        }

        foreach (EffectInfo e in EffectCatalog.All.Where(e => e.Family != EffectFamily.Framing))
        {
            foreach (double t in new[] { 0.5, 2.5, 4.5 })
            {
                SKImage img = EffectLibrary.Instance.ApplyEffect(ctx, a, new EffectInstance(e.Id, t, 5.0), 11);
                Save(img, $"effect-{e.Id}-{t:0.0}".Replace('.', '_'));
                if (!ReferenceEquals(img, a))
                {
                    img.Dispose();
                }
            }
        }

        foreach (TransitionInfo t in TransitionCatalog.All)
        {
            foreach (double p in new[] { 0.25, 0.5, 0.75 })
            {
                using SKImage img = EffectLibrary.Instance.ApplyTransition(ctx, a, b, new TransitionInstance(t.Id, p, 7, 1.25));
                Save(img, $"transition-{t.Id}-{(int)(p * 100):D2}");
            }
        }

        foreach (TitleAnimationInfo info in TitleAnimationCatalog.All)
        {
            var content = new TitleContent
            {
                AnimationId = info.Id,
                Placement = TitlePlacement.OnClip,
                Lines = ["AvaMovieMaker 1234 with a longer line", "Second line"],
                Credits = [new CreditRow("Director", "A. Person"), new CreditRow("Music", "B. Person")],
            };
            double d = TitleAnimationCatalog.DefaultDuration(info.Id, 2);
            foreach (double f in new[] { 0.1, 0.5, 0.9 })
            {
                using SKImage img = ctx.Draw(c =>
                {
                    c.DrawImage(a, 0, 0, SKSamplingOptions.Default);
                    EffectLibrary.Instance.DrawTitle(ctx, c, content, d * f, d, a, fullFrame: false);
                });
                Save(img, $"title-{info.Id}-{(int)(f * 100):D2}");
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} frames differ from the reference: {string.Join(", ", mismatches.Take(40))}");
    }
}
