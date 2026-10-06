using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaMovieMaker.Controls;

public sealed class ColorSwatch : Control
{
    public static readonly StyledProperty<uint> ColorProperty = AvaloniaProperty.Register<ColorSwatch, uint>(nameof(Color));

    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<ColorSwatch, string>(nameof(Kind), "text");

    static ColorSwatch() => AffectsRender<ColorSwatch>(ColorProperty, KindProperty, IsEffectivelyEnabledProperty);

    public uint Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    public override void Render(DrawingContext ctx)
    {
        using DrawingContext.PushedState o = ctx.PushOpacity(IsEffectivelyEnabled ? 1 : 0.4);
        var fill = new SolidColorBrush(Avalonia.Media.Color.FromUInt32(Color | 0xFF000000));
        var edge = new Pen(new SolidColorBrush(Avalonia.Media.Color.FromRgb(0x1E, 0x3C, 0x8C)));
        if (Kind == "background")
        {
            ctx.DrawRectangle(fill, edge, new Rect(0.5, 0.5, 15, 15));
            return;
        }

        ctx.DrawGeometry(null, new Pen(edge.Brush, 1.6), Geometry.Parse("M2,11 L8,1 L14,11 M4.4,7.5 L11.6,7.5"));
        ctx.DrawRectangle(fill, edge, new Rect(0.5, 12.5, 15, 3));
    }
}
