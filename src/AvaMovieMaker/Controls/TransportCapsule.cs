using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaMovieMaker.ViewModels.Preview;

namespace AvaMovieMaker.Controls;

public sealed class TransportCapsule : Control
{
    private enum Part
    {
        None,
        Previous,
        Play,
        Next,
    }

    private Part _hot;
    private Part _pressed;

    public TransportCapsule()
    {
        Width = 144;
        Height = 38;
        Cursor = Cursor.Default;
    }

    private MonitorViewModel? Vm => DataContext as MonitorViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is { } vm)
        {
            vm.PropertyChanged += (_, a) =>
            {
                if (a.PropertyName is nameof(MonitorViewModel.IsPlaying) or nameof(MonitorViewModel.DurationSeconds))
                {
                    InvalidateVisual();
                }
            };
        }
    }

    private void UpdateTip(Part p) => ToolTip.SetTip(this, p switch
    {
        Part.Previous => Strings.PreviousFrameTip,
        Part.Next => Strings.NextFrameTip,
        Part.Play => Vm?.PlayPauseTip,
        _ => null,
    });

    private static Part HitTest(Point p)
    {
        if (Math.Pow(p.X - 72, 2) + Math.Pow(p.Y - 19, 2) <= 19 * 19)
        {
            return Part.Play;
        }

        return p.X < 72 ? Part.Previous : Part.Next;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Part p = HitTest(e.GetPosition(this));
        if (p != _hot)
        {
            _hot = p;
            UpdateTip(p);
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hot = Part.None;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = HitTest(e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Part p = HitTest(e.GetPosition(this));
        if (p == _pressed && Vm is { } vm)
        {
            System.Windows.Input.ICommand? cmd = p switch
            {
                Part.Play => vm.PlayPauseCommand,
                Part.Previous => vm.PreviousFrameCommand,
                Part.Next => vm.NextFrameCommand,
                _ => null,
            };
            if (cmd?.CanExecute(null) == true)
            {
                cmd.Execute(null);
            }
        }

        _pressed = Part.None;
        InvalidateVisual();
    }

    private static IBrush Gloss(bool hot) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(hot ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0xF6, 0xF8, 0xFA), 0),
            new GradientStop(hot ? Color.FromRgb(0xDC, 0xEC, 0xFC) : Color.FromRgb(0xD6, 0xDE, 0xE8), 0.5),
            new GradientStop(hot ? Color.FromRgb(0xC2, 0xDA, 0xF4) : Color.FromRgb(0xC2, 0xCC, 0xD8), 1),
        },
    };

    public override void Render(DrawingContext ctx)
    {
        var border = new Pen(new SolidColorBrush(Color.FromRgb(0x98, 0xA6, 0xB6)));
        var capsule = new Rect(0.5, 6.5, 143, 25);
        ctx.DrawRectangle(Gloss(false), border, capsule, 12.5, 12.5);
        if (_hot == Part.Previous)
        {
            using (ctx.PushClip(new Rect(0, 0, 72, 38)))
            {
                ctx.DrawRectangle(Gloss(true), border, capsule, 12.5, 12.5);
            }
        }
        else if (_hot == Part.Next)
        {
            using (ctx.PushClip(new Rect(72, 0, 72, 38)))
            {
                ctx.DrawRectangle(Gloss(true), border, capsule, 12.5, 12.5);
            }
        }

        using (ctx.PushOpacity(Vm?.PlayPauseCommand.CanExecute(null) == true ? 1 : 0.45))
        {
            DrawGlyph(ctx, "frame-prev", new Point(16, 8));
            DrawGlyph(ctx, "frame-next", new Point(100, 8));
            DrawGlyph(ctx, Vm?.IsPlaying == true ? "pause-round" : "play-round", new Point(54.5, -2.5));
        }
    }

    private static readonly Dictionary<string, Glyph> Glyphs = new();

    private static void DrawGlyph(DrawingContext ctx, string kind, Point at)
    {
        if (!Glyphs.TryGetValue(kind, out Glyph? g))
        {
            g = new Glyph { Kind = kind };
            Glyphs[kind] = g;
        }

        using (ctx.PushTransform(Matrix.CreateTranslation(at.X, at.Y)))
        {
            g.Render(ctx);
        }
    }
}
