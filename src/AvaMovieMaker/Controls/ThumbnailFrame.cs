using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaMovieMaker.Media;

namespace AvaMovieMaker.Controls;

public sealed class ThumbnailFrame : Control
{
    public static readonly StyledProperty<Bitmap?> ImageProperty = AvaloniaProperty.Register<ThumbnailFrame, Bitmap?>(nameof(Image));
    public static readonly StyledProperty<MediaKind> KindProperty = AvaloniaProperty.Register<ThumbnailFrame, MediaKind>(nameof(Kind));
    public static readonly StyledProperty<bool> IsMissingProperty = AvaloniaProperty.Register<ThumbnailFrame, bool>(nameof(IsMissing));

    static ThumbnailFrame() => AffectsRender<ThumbnailFrame>(ImageProperty, KindProperty, IsMissingProperty);

    public Bitmap? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public MediaKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsMissing
    {
        get => GetValue(IsMissingProperty);
        set => SetValue(IsMissingProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var r = new Rect(Bounds.Size);
        if (IsMissing)
        {
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8)), r);
            var pen = new Pen(Brushes.Red, 3);
            ctx.DrawLine(pen, r.Center + new Vector(-10, -10), r.Center + new Vector(10, 10));
            ctx.DrawLine(pen, r.Center + new Vector(10, -10), r.Center + new Vector(-10, 10));
            return;
        }

        if (Kind == MediaKind.Audio)
        {
            const double size = 48;
            Bitmap b = Glyph.Picture("audio-music", size * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1));
            using DrawingContext.PushedState q = ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality });
            ctx.DrawImage(b, new Rect(b.Size), new Rect(r.Center.X - size / 2, r.Center.Y - size / 2, size, size));
            return;
        }

        if (Kind == MediaKind.Video)
        {
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)), r);
            for (double y = 4; y < r.Height - 4; y += 8)
            {
                ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xEC)), new Rect(2.5, y, 5, 4), 1);
                ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xEC)), new Rect(r.Width - 7.5, y, 5, 4), 1);
            }

            r = r.Deflate(new Thickness(10, 0));
        }

        if (Image is { } img)
        {
            ctx.DrawImage(img, new Rect(img.Size), r);
        }
        else
        {
            ctx.FillRectangle(Brushes.Black, r);
        }
    }
}
