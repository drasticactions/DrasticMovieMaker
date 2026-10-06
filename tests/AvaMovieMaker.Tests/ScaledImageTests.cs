using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaMovieMaker.Views;

namespace AvaMovieMaker.Tests;

public sealed class ScaledImageTests
{
    private static RenderTargetBitmap HalfRedHalfBlue()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(40, 20), new Vector(192, 192));
        using (DrawingContext dc = rtb.CreateDrawingContext())
        {
            dc.FillRectangle(Brushes.Red, new Rect(0, 0, 10, 10));
            dc.FillRectangle(Brushes.Blue, new Rect(10, 0, 10, 10));
        }

        return rtb;
    }

    private static uint PixelAt(IImage image, int x, int y)
    {
        var w = new Window
        {
            Width = 60,
            Height = 40,
            Background = Brushes.White,
            Content = new Image { Source = image, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top },
        };
        w.Show();
        try
        {
            using WriteableBitmap frame = w.CaptureRenderedFrame()!;
            using ILockedFramebuffer fb = frame.Lock();
            unsafe
            {
                uint v = ((uint*)(fb.Address + y * fb.RowBytes))[x];
                return fb.Format == PixelFormat.Rgba8888 ? ((v & 0xFF) << 16) | (v & 0xFF00) | ((v >> 16) & 0xFF) : v & 0xFFFFFF;
            }
        }
        finally
        {
            w.Close();
        }
    }

    private const uint Red = 0xFF0000, Blue = 0x0000FF;

    [AvaloniaFact]
    public void A_high_dpi_snapshot_draws_whole()
    {
        RenderTargetBitmap rtb = HalfRedHalfBlue();
        var whole = new ScaledImage(rtb, new PixelRect(0, 0, 40, 20), 2);
        Assert.Equal(new Size(20, 10), whole.Size);
        Assert.Equal(Red, PixelAt(whole, 5, 5));
        Assert.Equal(Blue, PixelAt(whole, 15, 5));

        var right = new ScaledImage(rtb, new PixelRect(20, 0, 20, 20), 2);
        Assert.Equal(new Size(10, 10), right.Size);
        Assert.Equal(Blue, PixelAt(right, 2, 5));
        Assert.Equal(Blue, PixelAt(right, 8, 5));
    }
}
