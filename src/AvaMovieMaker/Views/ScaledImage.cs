using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AvaMovieMaker.Views;

public sealed class ScaledImage(Bitmap source, PixelRect pixels, double scale) : IImage
{
    public Bitmap Source { get; } = source;

    public PixelRect Pixels { get; } = pixels;

    public double Scale { get; } = scale;

    public Size Size => new(Pixels.Width / Scale, Pixels.Height / Scale);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) =>
        context.DrawImage(Source,
            new Rect(Pixels.X + sourceRect.X * Scale, Pixels.Y + sourceRect.Y * Scale, sourceRect.Width * Scale, sourceRect.Height * Scale),
            destRect);
}
