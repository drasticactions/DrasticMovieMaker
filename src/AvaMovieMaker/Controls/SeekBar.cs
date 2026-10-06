using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Controls;

public sealed class SeekBar : Control
{
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<SeekBar, double>(nameof(Maximum), 1);
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<SeekBar, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private bool _dragging;

    static SeekBar()
    {
        AffectsRender<SeekBar>(MaximumProperty, ValueProperty);
        FocusableProperty.OverrideDefaultValue<SeekBar>(true);
    }

    public SeekBar() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private double ThumbX => 6 + (Bounds.Width - 12) * (Maximum <= 0 ? 0 : Math.Clamp(Value / Maximum, 0, 1));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragging = true;
        e.Pointer.Capture(this);
        Seek(e.GetPosition(this).X);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        double x = e.GetPosition(this).X;
        if (_dragging)
        {
            Seek(x);
        }

        ToolTip.SetTip(this, Maximum > 0 ? TimeFormat.Format(MediaTime.FromSeconds(TimeAt(x))) : null);
    }

    public double TimeAt(double x) => Math.Clamp((x - 6) / Math.Max(1, Bounds.Width - 12), 0, 1) * Maximum;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        double step = Maximum / 100;
        if (e.Key is Key.Left or Key.Right)
        {
            Value = Math.Clamp(Value + (e.Key == Key.Left ? -step : step), 0, Maximum);
            e.Handled = true;
        }
    }

    private void Seek(double x)
    {
        if (Maximum > 0)
        {
            Value = TimeAt(x);
        }
    }

    public override void Render(DrawingContext ctx)
    {
        double cy = Bounds.Height / 2;
        var track = new Rect(0.5, cy - 2.5, Bounds.Width - 1, 5);
        ctx.DrawRectangle(this.Brush("MmSeekTrackBrush", Brushes.White),
            new Pen(this.Brush("MmSeekTrackBorderBrush", Brushes.Gray)), track, 2.5, 2.5);
        double x = ThumbX;
        if (x > 3)
        {
            ctx.DrawRectangle(this.Brush("MmSeekFillBrush", Brushes.Green), null, new Rect(1, cy - 2, x - 1, 4), 2, 2);
        }

        var thumb = new Rect(x - 9, cy - 4.5, 18, 9);
        ctx.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromRgb(0xC8, 0xDC, 0xF8), 0), new GradientStop(Color.FromRgb(0x3A, 0x72, 0xD0), 1) },
        }, new Pen(new SolidColorBrush(Color.FromRgb(0x24, 0x4A, 0x90))), thumb, 4.5, 4.5);
    }
}
