using System.Collections.Specialized;
using System.ComponentModel;
using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaMovieMaker.Converters;
using AvaMovieMaker.Views;
using AvaMovieMaker.ViewModels.Storyboard;

namespace AvaMovieMaker.Controls;

public sealed class StoryboardStrip : Control
{
    public const double CellWidth = 161;
    public const double CellHeight = 136;
    public const double SlotWidth = 56;
    public const double SlotHeight = 42;
    public const double SlotGap = 10;
    public const double Pitch = CellWidth + SlotGap * 2 + SlotWidth;
    private const int Placeholders = 5;

    private StoryboardViewModel? _vm;
    private int _dropIndex = -1;
    private int _dropSlot = -1;
    private int _dropCell = -1;
    private PointerPressedEventArgs? _pressed;
    private Point _pressPoint;

    public StoryboardStrip()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, e) =>
        {
            if (!new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                ClearDrop();
            }
        });
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        Focusable = true;
    }

    private static Rect StarRect(int i) => new(CellRect(i).X + 8, CellRect(i).Y + 4 + PictureHeight - 26, 25, 25);

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        BoardTarget target = BoardTarget.Empty;
        int cell = e.TryGetPosition(this, out Point p) ? HitTest(p).Cell : _vm.Cells.ToList().FindIndex(c => c.IsSelected);
        if (cell >= 0)
        {
            StoryboardCellViewModel c = _vm.Cells[cell];
            target = c.Clip.Effects.Count > 0 && StarRect(cell).Contains(p) ? BoardTarget.EffectStar
                : c.Clip.Kind == AvaMovieMaker.Timeline.Model.VideoClipKind.Title ? BoardTarget.Title
                : BoardTarget.VideoClip;
        }

        e.Handled = BoardMenus.Show(this, target);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.Cells.CollectionChanged -= OnCellsChanged;
        }

        _vm = DataContext as StoryboardViewModel;
        if (_vm is not null)
        {
            _vm.Cells.CollectionChanged += OnCellsChanged;
            OnCellsChanged(null, null!);
        }
    }

    private void OnCellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        foreach (StoryboardCellViewModel c in _vm.Cells)
        {
            c.PropertyChanged -= OnCellChanged;
            c.PropertyChanged += OnCellChanged;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private int Count => _vm?.Cells.Count ?? 0;

    protected override Size MeasureOverride(Size availableSize) => new((Count + Placeholders) * Pitch, CellHeight + 8);

    private static Rect CellRect(int i) => new(2 + i * Pitch, 0, CellWidth, CellHeight);

    public const double PictureWidth = 153, PictureHeight = 115;

    private static Rect SlotRect(int i) => new(2 + i * Pitch - SlotGap - SlotWidth, (115 - SlotHeight) / 2 + 4, SlotWidth, SlotHeight);

    private (int Cell, int Slot) HitTest(Point p)
    {
        int i = (int)Math.Floor((p.X - 2) / Pitch);
        double local = p.X - 2 - i * Pitch;
        if (local >= 0 && local < CellWidth && i < Count)
        {
            return (i, -1);
        }

        int slot = i + 1;
        return (-1, slot >= 1 && slot < Count ? slot : -1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_vm is null)
        {
            return;
        }

        (int cell, _) = HitTest(e.GetPosition(this));
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (cell >= 0)
            {
                if (!_vm.Cells[cell].IsSelected)
                {
                    _vm.Select(_vm.Cells[cell], toggle: false, range: false);
                }

                _collapse = null;
                _pressed = e;
                _pressPoint = e.GetPosition(this);
            }

            return;
        }

        if (cell >= 0)
        {
            StoryboardCellViewModel c = _vm.Cells[cell];
            bool ctrl = Menus.CommandKeys.HasCommand(e.KeyModifiers);
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            bool inMulti = c.IsSelected && _vm.Cells.Count(x => x.IsSelected) > 1;
            _vm.Select(c, ctrl, shift);
            _collapse = !ctrl && !shift && inMulti ? c : null;
            _pressed = e;
            _pressPoint = e.GetPosition(this);
        }
        else
        {
            if (!Menus.CommandKeys.HasCommand(e.KeyModifiers))
            {
                _vm.ClearSelection();
            }

            _marquee = new Rect(e.GetPosition(this), new Size(0, 0));
            _marqueeStart = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    private Rect? _marquee;
    private Point _marqueeStart;
    private StoryboardCellViewModel? _collapse;

    private string? _tip;

    private void UpdateTip(Point p)
    {
        int i = (int)Math.Floor((p.X - 2) / Pitch);
        double local = p.X - 2 - i * Pitch;
        string? tip = null;
        if (local >= CellWidth && i + 1 < Count + 5)
        {
            tip = Strings.HintDropTransition;
        }
        else if (i >= Count)
        {
            tip = Strings.HintDropVideo;
        }
        else if (i >= 0 && local < 34 && p.Y > CellHeight - 52)
        {
            tip = Strings.HintDropEffect;
        }

        if (tip != _tip)
        {
            _tip = tip;
            ToolTip.SetTip(this, tip);
        }
    }

    protected override async void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressed is null && _marquee is null)
        {
            UpdateTip(e.GetPosition(this));
        }

        if (_marquee is not null)
        {
            Point p = e.GetPosition(this);
            _marquee = new Rect(new Point(Math.Min(p.X, _marqueeStart.X), Math.Min(p.Y, _marqueeStart.Y)),
                new Point(Math.Max(p.X, _marqueeStart.X), Math.Max(p.Y, _marqueeStart.Y)));
            InvalidateVisual();
            return;
        }

        if (_pressed is not null && Math.Abs(e.GetPosition(this).X - _pressPoint.X) > 4)
        {
            _collapse = null;
        }

        if (_pressed is { } pressed && e.GetCurrentPoint(this).Properties is { IsLeftButtonPressed: true } or { IsRightButtonPressed: true } &&
            Math.Abs(e.GetPosition(this).X - _pressPoint.X) > 4)
        {
            _pressed = null;
            (int cell, _) = HitTest(_pressPoint);
            DragPayload payload = DragPayload.ForStoryboardMove();
            if (cell >= 0)
            {
                Rect r = CellRect(cell);
                payload = payload.WithImage(DragPayload.Snapshot(this, r), _pressPoint - r.TopLeft);
            }

            await DragPayload.DoDragAsync(pressed, payload);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_vm is not null && _marquee is { } m)
        {
            if (m.Width > 3 || m.Height > 3)
            {
                _vm.SelectCells([.. Enumerable.Range(0, Count).Where(i => CellRect(i).Intersects(m))], add: Menus.CommandKeys.HasCommand(e.KeyModifiers));
            }

            e.Pointer.Capture(null);
            InvalidateVisual();
        }
        else if (_vm is not null && _collapse is { } c && _pressed is not null)
        {
            _vm.Collapse(c);
        }

        _marquee = null;
        _collapse = null;
        _pressed = null;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_vm is not null)
        {
            _vm.Session.ActiveSelection = ViewModels.Session.SelectionKind.Timeline;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        KeyModifiers mods = Menus.CommandKeys.ToLogical(e.KeyModifiers);
        if (_vm is null || (mods & (KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }

        bool shift = mods.HasFlag(KeyModifiers.Shift);
        bool ctrl = mods.HasFlag(KeyModifiers.Control);
        e.Handled = e.Key switch
        {
            Key.Left or Key.Up => _vm.Key(ViewModels.Session.SelectionKey.Previous, shift, ctrl),
            Key.Right or Key.Down => _vm.Key(ViewModels.Session.SelectionKey.Next, shift, ctrl),
            Key.Home => _vm.Key(ViewModels.Session.SelectionKey.First, shift, ctrl),
            Key.End => _vm.Key(ViewModels.Session.SelectionKey.Last, shift, ctrl),
            Key.Space when ctrl || shift => _vm.Key(ctrl ? ViewModels.Session.SelectionKey.ToggleActive : ViewModels.Session.SelectionKey.ExtendToActive),
            Key.Escape => _vm.Key(ViewModels.Session.SelectionKey.Clear),
            _ => false,
        };
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        DragPayload? payload = DragPayload.From(e);
        Point p = e.GetPosition(this);
        _dropIndex = _dropSlot = _dropCell = -1;
        if (payload is null)
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
            InvalidateVisual();
            return;
        }

        Evaluate(payload, p);
        bool copy = !payload.IsStoryboardMove || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        e.DragEffects = _dropIndex >= 0 || _dropSlot >= 0 || _dropCell >= 0 ? (copy ? DragDropEffects.Copy : DragDropEffects.Move) : DragDropEffects.None;
        e.Handled = true;
        AutoScroll(p);
        InvalidateVisual();
    }

    private Avalonia.Threading.DispatcherTimer? _scrollTimer;
    private int _scrollDirection;

    private void AutoScroll(Point p)
    {
        if (this.FindAncestorOfType<ScrollViewer>() is not { } sv)
        {
            return;
        }

        double left = sv.Offset.X, right = left + sv.Viewport.Width;
        _scrollDirection = p.X < left + 11 ? -1 : p.X > right - 11 ? 1 : 0;
        if (_scrollDirection == 0)
        {
            _scrollTimer?.Stop();
            return;
        }

        _scrollTimer ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(50), Avalonia.Threading.DispatcherPriority.Normal, (_, _) =>
        {
            if (this.FindAncestorOfType<ScrollViewer>() is { } v && _scrollDirection != 0)
            {
                v.Offset = v.Offset.WithX(Math.Clamp(v.Offset.X + _scrollDirection * 20, 0, Math.Max(0, v.Extent.Width - v.Viewport.Width)));
            }
        });
        _scrollTimer.Start();
    }

    private void Evaluate(DragPayload payload, Point p)
    {
        _dropIndex = _dropSlot = _dropCell = -1;
        (int cell, int slot) = HitTest(p);
        if (payload.TransitionId is not null)
        {
            _dropSlot = slot >= 1 ? slot : cell >= 1 ? cell : -1;
        }
        else if (payload.EffectId is not null)
        {
            _dropCell = cell;
        }
        else if (payload.IsStoryboardMove || payload.Media.Count > 0)
        {
            _dropIndex = cell >= 0 ? cell + (p.X > CellRect(cell).Center.X ? 1 : 0)
                : Math.Clamp((int)Math.Ceiling((p.X - 2 - CellWidth) / Pitch), 0, Count);
            _dropIndex = Math.Clamp(_dropIndex, 0, Count);
        }

        if (_dropSlot >= 1 && _vm is not null && !_vm.TransitionFits(_dropSlot))
        {
            _dropSlot = -1;
        }
    }

    private void ClearDrop()
    {
        _dropIndex = _dropSlot = _dropCell = -1;
        _scrollTimer?.Stop();
        InvalidateVisual();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DragPayload? payload = DragPayload.From(e);
        if (_vm is not null && payload is not null)
        {
            e.Handled = true;

            Evaluate(payload, e.GetPosition(this));
            int slot = _dropSlot, cell = _dropCell, index = _dropIndex;
            bool copy = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool target = payload.TransitionId is not null ? slot >= 1 : payload.EffectId is not null ? cell >= 0 : index >= 0;
            if (target && payload.IsRightDrag)
            {
                DragDropEffects choice = await DropMenu.ChooseAsync(this, canMove: payload.IsStoryboardMove);
                if (choice == DragDropEffects.None)
                {
                    ClearDrop();
                    return;
                }

                copy = choice == DragDropEffects.Copy;
            }

            if (payload.TransitionId is { } t && slot >= 1)
            {
                _vm.DropTransition(slot, t);
            }
            else if (payload.EffectId is { } fx && cell >= 0)
            {
                _vm.DropEffect(cell, fx);
            }
            else if (payload.IsStoryboardMove && index >= 0)
            {
                _ = copy ? _vm.CopySelected(index) : _vm.MoveSelected(index);
            }
            else if (payload.Media.Count > 0 && index >= 0)
            {
                _vm.DropMedia(index, payload.Media);
            }
        }

        ClearDrop();
    }

    public override void Render(DrawingContext ctx)
    {
        int n = Count;
        for (int i = 0; i < n + Placeholders; i++)
        {
            if (i > 0)
            {
                DrawSlot(ctx, i, i < n ? _vm!.Cells[i] : null);
            }

            if (i < n)
            {
                DrawCell(ctx, i, _vm!.Cells[i]);
            }
            else
            {
                DrawPlaceholder(ctx, i, i == 0 && n == 0);
            }
        }

        if (_dropIndex >= 0)
        {
            double x = CellRect(_dropIndex).X - 9;
            ctx.FillRectangle(this.Brush("MmSelectionBrush", new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF))), new Rect(Math.Max(0, x - 1), 0, 3, CellHeight));
        }

        if (_marquee is { } m)
        {
            ctx.DrawRectangle(null, new Pen(Brushes.Black, 1, new DashStyle([2, 2], 0)), m);
        }
    }

    private static readonly IBrush StripBrush = new SolidColorBrush(Color.FromRgb(0xB7, 0xC2, 0xCF));
    private static readonly IBrush SprocketBrush = new SolidColorBrush(Color.FromRgb(0xE1, 0xE8, 0xEE));
    private static readonly IBrush EmptyPictureBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0xF0, 0xF6, 0xFB), 0),
            new GradientStop(Color.FromRgb(0xE9, 0xF0, 0xF5), 0.45),
            new GradientStop(Color.FromRgb(0xE1, 0xE8, 0xEE), 0.86),
            new GradientStop(Color.FromRgb(0xEF, 0xF4, 0xF9), 0.92),
            new GradientStop(Color.FromRgb(0xEF, 0xF4, 0xF9), 1),
        },
    };

    private static readonly IBrush SlotLightBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.FromRgb(0xF2, 0xF6, 0xFA), 0), new GradientStop(Color.FromRgb(0xE4, 0xEB, 0xF2), 1) },
    };

    private static Rect DrawFilmFrame(DrawingContext ctx, Rect r, IBrush picture)
    {
        const double strip = 14;
        ctx.FillRectangle(StripBrush, new Rect(r.X, r.Y, strip, r.Height));
        ctx.FillRectangle(StripBrush, new Rect(r.Right - strip, r.Y, strip, r.Height));
        for (double y = r.Y + 3; y + 10 <= r.Bottom - 2; y += 14)
        {
            ctx.DrawRectangle(SprocketBrush, null, new Rect(r.X + 3, y, 8, 10), 2, 2);
            ctx.DrawRectangle(SprocketBrush, null, new Rect(r.Right - 11, y, 8, 10), 2, 2);
        }

        var pic = new Rect(r.X + strip, r.Y, r.Width - 2 * strip, r.Height);
        ctx.FillRectangle(picture, pic);
        return pic;
    }

    private void DrawPlaceholder(DrawingContext ctx, int i, bool hint)
    {
        Rect r = CellRect(i).Deflate(new Thickness(0, 4, 0, 21));
        Rect pic = DrawFilmFrame(ctx, r, EmptyPictureBrush);
        if (hint)
        {
            var text = new FormattedText(Strings.HintDragMedia, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(TextElement.GetFontFamily(this)), 12, Brushes.Black);
            ctx.DrawText(text, new Point(pic.Center.X - text.Width / 2, pic.Center.Y - text.Height / 2));
        }
    }

    private void DrawSlot(DrawingContext ctx, int i, StoryboardCellViewModel? cell)
    {
        Rect r = SlotRect(i);
        bool hot = _dropSlot == i;
        if (cell is { HasTransition: true })
        {
            if (CatalogSample.TryGet(cell.Clip.TransitionIn!.TransitionId, true, InvalidateVisual) is { } sample)
            {
                ctx.DrawImage(sample, new Rect(sample.Size), r.Deflate(1));
            }

            ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x99, 0xD1, 0xFC)), 1), r.Inflate(2));
            return;
        }

        ctx.FillRectangle(hot ? new SolidColorBrush(Color.FromRgb(0x99, 0xB4, 0xD1)) : StripBrush, r);
        double tip = r.X + r.Width * 0.69, notch = r.X + r.Width * 0.44;
        var light = Geometry.Parse(FormattableString.Invariant($"M{r.X},{r.Y} L{notch},{r.Y} L{tip},{r.Center.Y} L{notch},{r.Bottom} L{r.X},{r.Bottom} Z"));
        ctx.DrawGeometry(hot ? new SolidColorBrush(Color.FromRgb(0xC6, 0xE3, 0xFC)) : SlotLightBrush, null, light);
        ctx.DrawRectangle(null, new Pen(Brushes.White), r.Deflate(0.5));
    }

    private void DrawCell(DrawingContext ctx, int i, StoryboardCellViewModel cell)
    {
        Rect r = CellRect(i);
        if (cell.IsSelected)
        {
            ctx.DrawRectangle(this.Brush("MmCellSelectedBrush", Brushes.LightBlue), new Pen(this.Brush("MmCellSelectedBorderBrush", Brushes.SteelBlue)), r, 2, 2);
        }

        var pic = new Rect(r.X + 4, r.Y + 4, PictureWidth, PictureHeight);
        ctx.FillRectangle(Brushes.Black, pic);
        if (cell.Thumbnail is { } sk)
        {
            var bmp = SkBitmapConverter.ToBitmap(sk);
            double s = Math.Min(pic.Width / bmp.Size.Width, pic.Height / bmp.Size.Height);
            Size size = bmp.Size * s;
            ctx.DrawImage(bmp, new Rect(bmp.Size), new Rect(pic.Center.X - size.Width / 2, pic.Center.Y - size.Height / 2, size.Width, size.Height));
        }

        if (_dropCell == i)
        {
            ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x3C, 0x8C, 0xE8)), 2), pic);
        }

        DrawStar(ctx, new Point(pic.X + 4, pic.Bottom - 26), cell.Clip.Effects.Count);
        var name = new FormattedText(cell.Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this)), 12, this.Brush("MmCellNameBrush", Brushes.Black))
        {
            MaxTextWidth = CellWidth - 8,
            Trimming = TextTrimming.CharacterEllipsis,
            MaxLineCount = 1,
        };
        ctx.DrawText(name, new Point(r.X + 5, pic.Bottom + 2));
    }

    private static readonly Glyph StarOff = new() { Kind = "star" };
    private static readonly Glyph StarOn = new() { Kind = "star-on" };
    private static readonly Glyph StarMulti = new() { Kind = "star-multi" };

    private static void DrawStar(DrawingContext ctx, Point at, int effects)
    {
        using (ctx.PushTransform(Matrix.CreateTranslation(at.X, at.Y)))
        {
            (effects switch { 0 => StarOff, 1 => StarOn, _ => StarMulti }).Render(ctx);
        }
    }
}
