using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaWpf;

namespace AvaMovieMaker.Dialogs;

public sealed class ColorDialog : UserControl
{
    private static readonly uint[] Basic =
    [
        0xFF8080, 0xFFFF80, 0x80FF80, 0x00FF80, 0x80FFFF, 0x0080FF, 0xFF80C0, 0xFF80FF,
        0xFF0000, 0xFFFF00, 0x80FF00, 0x00FF40, 0x00FFFF, 0x0080C0, 0x8080C0, 0xFF00FF,
        0x804040, 0xFF8040, 0x00FF00, 0x008080, 0x004080, 0x8080FF, 0x800040, 0xFF0080,
        0x800000, 0xFF8000, 0x008000, 0x008040, 0x0000FF, 0x0000A0, 0x800080, 0x8000FF,
        0x400000, 0x804000, 0x004000, 0x004040, 0x000080, 0x000040, 0x400040, 0x400080,
        0x000000, 0x808000, 0x808040, 0x808080, 0x408080, 0xC0C0C0, 0x400040, 0xFFFFFF,
    ];

    private readonly List<uint> _custom;
    private readonly WrapPanel _customPanel = new() { Width = 220 };
    private readonly Border _solid = new() { Width = 64, Height = 42, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
    private readonly NumericUpDown _hue = Box(239), _sat = Box(240), _lum = Box(240), _red = Box(255), _green = Box(255), _blue = Box(255);
    private readonly HsField _field = new();
    private readonly LumBar _lumBar = new();
    private readonly StackPanel _define = new() { Spacing = 8, IsVisible = false, Margin = new Thickness(14, 0, 0, 0) };
    private int _customSlot;
    private bool _syncing;
    private uint _rgb;

    public ColorDialog(uint initial, List<uint> customColors)
    {
        WindowSettings.SetTitle(this, Strings.ColorWindowTitle);
        WindowSettings.SetFrameKind(this, WindowFrameKind.Dialog);
        WindowSettings.SetCanResize(this, false);
        WindowSettings.SetSizeToContent(this, SizeToContent.WidthAndHeight);
        WindowSettings.SetWindowStartupLocation(this, WindowStartupLocation.CenterOwner);
        WindowSettings.SetShowInTaskbar(this, false);
        _custom = customColors;
        while (_custom.Count < 16)
        {
            _custom.Add(0xFFFFFF);
        }

        var left = new StackPanel { Spacing = 6, Width = 222 };
        left.Children.Add(new TextBlock { Text = Strings.ColorBasicColors });
        var basic = new WrapPanel { Width = 220 };
        foreach (uint c in Basic)
        {
            basic.Children.Add(Swatch(c, () => SetRgb(c)));
        }

        left.Children.Add(basic);
        left.Children.Add(new TextBlock { Text = Strings.ColorCustomColors, Margin = new Thickness(0, 10, 0, 0) });
        left.Children.Add(_customPanel);
        RebuildCustom();
        var defineButton = new Button { Content = Strings.ColorDefineCustom, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        defineButton.Click += (_, _) =>
        {
            _define.IsVisible = true;
            defineButton.IsEnabled = false;
        };
        left.Children.Add(defineButton);
        var ok = new Button { Content = Strings.ButtonOk, MinWidth = 66, IsDefault = true };
        ok.Click += (_, _) => WindowHost.CloseDialog(this, (uint?)(0xFF000000 | _rgb));
        var cancel = new Button { Content = Strings.ButtonCancel, MinWidth = 66, IsCancel = true };
        cancel.Click += (_, _) => WindowHost.CloseDialog(this, null);
        left.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { ok, cancel } });

        _field.Picked += (h, s) => SetHsl(h, s, _lum.Value is { } l && l > 0 && l < 240 ? (int)l : 120);
        _lumBar.Picked += l => SetHsl((int)(_hue.Value ?? 0), (int)(_sat.Value ?? 0), l);
        _define.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _field, _lumBar } });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), RowSpacing = 4, ColumnSpacing = 4 };
        AddRow(grid, 0, Strings.ColorHue, _hue, Strings.ColorRed, _red);
        AddRow(grid, 1, Strings.ColorSat, _sat, Strings.ColorGreen, _green);
        AddRow(grid, 2, Strings.ColorLum, _lum, Strings.ColorBlue, _blue);
        var solidLabel = new TextBlock { Text = Strings.ColorSolid, HorizontalAlignment = HorizontalAlignment.Center };
        _define.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new StackPanel { Children = { _solid, solidLabel } }, grid } });
        var add = new Button { Content = Strings.ColorAddToCustom, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        add.Click += (_, _) =>
        {
            _custom[_customSlot] = _rgb;
            _customSlot = (_customSlot + 1) % 16;
            RebuildCustom();
        };
        _define.Children.Add(add);

        foreach (NumericUpDown b in new[] { _hue, _sat, _lum })
        {
            b.ValueChanged += (_, _) =>
            {
                if (!_syncing)
                {
                    SetHsl((int)(_hue.Value ?? 0), (int)(_sat.Value ?? 0), (int)(_lum.Value ?? 0));
                }
            };
        }

        foreach (NumericUpDown b in new[] { _red, _green, _blue })
        {
            b.ValueChanged += (_, _) =>
            {
                if (!_syncing)
                {
                    SetRgb(((uint)(_red.Value ?? 0) << 16) | ((uint)(_green.Value ?? 0) << 8) | (uint)(_blue.Value ?? 0));
                }
            };
        }

        Content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10), Children = { left, _define } };
        SetRgb(initial & 0xFFFFFF);
    }

    private static NumericUpDown Box(int max) => new() { Minimum = 0, Maximum = max, Increment = 1, FormatString = "0", Width = 70, ShowButtonSpinner = false };

    private static void AddRow(Grid g, int row, string a, Control ca, string b, Control cb)
    {
        var la = new TextBlock { Text = a, VerticalAlignment = VerticalAlignment.Center };
        var lb = new TextBlock { Text = b, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetRow(la, row);
        Grid.SetRow(ca, row);
        Grid.SetColumn(ca, 1);
        Grid.SetRow(lb, row);
        Grid.SetColumn(lb, 2);
        Grid.SetRow(cb, row);
        Grid.SetColumn(cb, 3);
        g.Children.Add(la);
        g.Children.Add(ca);
        g.Children.Add(lb);
        g.Children.Add(cb);
    }

    private static Control Swatch(uint rgb, Action pick)
    {
        var b = new Border
        {
            Width = 21,
            Height = 17,
            Margin = new Thickness(3, 3, 3, 3),
            Background = new SolidColorBrush(Color.FromUInt32(0xFF000000 | rgb)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(1),
            Focusable = true,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        b.PointerPressed += (_, _) => pick();
        return b;
    }

    private void RebuildCustom()
    {
        _customPanel.Children.Clear();
        for (int i = 0; i < 16; i++)
        {
            int slot = i;
            _customPanel.Children.Add(Swatch(_custom[i], () =>
            {
                _customSlot = slot;
                SetRgb(_custom[slot]);
            }));
        }
    }

    private void SetRgb(uint rgb)
    {
        _rgb = rgb & 0xFFFFFF;
        (int h, int s, int l) = ToHsl(_rgb);
        Sync(h, s, l);
    }

    private void SetHsl(int h, int s, int l)
    {
        _rgb = FromHsl(h, s, l);
        Sync(h, s, l);
    }

    private void Sync(int h, int s, int l)
    {
        _syncing = true;
        _hue.Value = h;
        _sat.Value = s;
        _lum.Value = l;
        _red.Value = (_rgb >> 16) & 0xFF;
        _green.Value = (_rgb >> 8) & 0xFF;
        _blue.Value = _rgb & 0xFF;
        _syncing = false;
        _solid.Background = new SolidColorBrush(Color.FromUInt32(0xFF000000 | _rgb));
        _field.Mark = (h, s);
        _lumBar.Hue = h;
        _lumBar.Sat = s;
        _lumBar.Lum = l;
    }

    internal static (int H, int S, int L) ToHsl(uint rgb)
    {
        double r = ((rgb >> 16) & 0xFF) / 255.0, g = ((rgb >> 8) & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, h = 0, s = 0;
        if (max > min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            h /= 6;
        }

        return ((int)Math.Round(h * 240) % 240, (int)Math.Round(s * 240), (int)Math.Round(l * 240));
    }

    internal static uint FromHsl(int h, int s, int l)
    {
        double hh = h / 240.0, ss = s / 240.0, ll = l / 240.0;
        if (ss <= 0)
        {
            uint v = (uint)Math.Round(ll * 255);
            return (v << 16) | (v << 8) | v;
        }

        double q = ll < 0.5 ? ll * (1 + ss) : ll + ss - ll * ss, p = 2 * ll - q;
        static double Hue(double p, double q, double t)
        {
            t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
            return t < 1.0 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 : p;
        }

        uint R = (uint)Math.Round(Hue(p, q, hh + 1.0 / 3) * 255), G = (uint)Math.Round(Hue(p, q, hh) * 255), B = (uint)Math.Round(Hue(p, q, hh - 1.0 / 3) * 255);
        return (R << 16) | (G << 8) | B;
    }

    private sealed class HsField : Control
    {
        private Avalonia.Media.Imaging.WriteableBitmap? _bmp;

        public (int H, int S) Mark { get => _mark; set { _mark = value; InvalidateVisual(); } }

        private (int H, int S) _mark;

        public event Action<int, int>? Picked;

        public HsField()
        {
            Width = 175;
            Height = 187;
            Cursor = new Cursor(StandardCursorType.Cross);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            e.Pointer.Capture(this);
            Pick(e.GetPosition(this));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                Pick(e.GetPosition(this));
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            e.Pointer.Capture(null);
        }

        private void Pick(Point p) => Picked?.Invoke((int)Math.Clamp(p.X / Width * 239, 0, 239), (int)Math.Clamp(240 - p.Y / Height * 240, 0, 240));

        public override unsafe void Render(DrawingContext ctx)
        {
            int w = (int)Width, h = (int)Height;
            if (_bmp is null)
            {
                _bmp = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
                using Avalonia.Platform.ILockedFramebuffer fb = _bmp.Lock();
                for (int y = 0; y < h; y++)
                {
                    uint* row = (uint*)((byte*)fb.Address + y * fb.RowBytes);
                    for (int x = 0; x < w; x++)
                    {
                        row[x] = 0xFF000000 | FromHsl(x * 239 / w, 240 - y * 240 / h, 120);
                    }
                }
            }

            ctx.DrawImage(_bmp, new Rect(0, 0, w, h));
            double mx = _mark.H / 239.0 * w, my = (240 - _mark.S) / 240.0 * h;
            var pen = new Pen(Brushes.Black, 2);
            ctx.DrawLine(pen, new Point(mx - 9, my), new Point(mx - 3, my));
            ctx.DrawLine(pen, new Point(mx + 3, my), new Point(mx + 9, my));
            ctx.DrawLine(pen, new Point(mx, my - 9), new Point(mx, my - 3));
            ctx.DrawLine(pen, new Point(mx, my + 3), new Point(mx, my + 9));
        }
    }

    private sealed class LumBar : Control
    {
        public int Hue { get; set; }

        public int Sat { get; set; }

        public int Lum { get => _lum; set { _lum = value; InvalidateVisual(); } }

        private int _lum;

        public event Action<int>? Picked;

        public LumBar()
        {
            Width = 24;
            Height = 187;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            e.Pointer.Capture(this);
            Pick(e.GetPosition(this));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (ReferenceEquals(e.Pointer.Captured, this))
            {
                Pick(e.GetPosition(this));
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            e.Pointer.Capture(null);
        }

        private void Pick(Point p) => Picked?.Invoke((int)Math.Clamp(240 - p.Y / Height * 240, 0, 240));

        public override void Render(DrawingContext ctx)
        {
            for (int y = 0; y < (int)Height; y += 2)
            {
                uint c = FromHsl(Hue, Sat, 240 - y * 240 / (int)Height);
                ctx.FillRectangle(new SolidColorBrush(Color.FromUInt32(0xFF000000 | c)), new Rect(0, y, 12, 2));
            }

            double ay = (240 - _lum) / 240.0 * Height;
            ctx.DrawGeometry(Brushes.Black, null, new PolylineGeometry([new Point(14, ay), new Point(22, ay - 5), new Point(22, ay + 5)], true));
        }
    }
}
