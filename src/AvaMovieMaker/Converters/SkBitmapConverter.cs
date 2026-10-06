using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace AvaMovieMaker.Converters;

public sealed class SkBitmapConverter : IValueConverter
{
    public static readonly SkBitmapConverter Instance = new();

    private static readonly ConditionalWeakTable<SKBitmap, Bitmap> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SKBitmap sk ? ToBitmap(sk) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    public static Bitmap ToBitmap(SKBitmap sk) => Cache.GetValue(sk, s =>
    {
        using SKBitmap bgra = s.ColorType == SKColorType.Bgra8888 ? s.Copy() : s.Copy(SKColorType.Bgra8888);
        var wb = new WriteableBitmap(new PixelSize(bgra.Width, bgra.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using ILockedFramebuffer fb = wb.Lock();
        unsafe
        {
            for (int y = 0; y < bgra.Height; y++)
            {
                Buffer.MemoryCopy((byte*)bgra.GetPixels() + y * bgra.RowBytes, (byte*)fb.Address + y * fb.RowBytes, fb.RowBytes, bgra.Width * 4);
            }
        }

        return wb;
    });
}
