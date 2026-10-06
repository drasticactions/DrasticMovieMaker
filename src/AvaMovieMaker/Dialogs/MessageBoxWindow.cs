using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using AvaWpf;
using Path = Avalonia.Controls.Shapes.Path;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Dialogs;

public sealed class MessageBoxWindow : UserControl
{
    private readonly CheckBox? _dontShow;

    private MessageBoxWindow(string title, MessageRequest request)
    {
        WindowSettings.SetTitle(this, title);
        WindowSettings.SetFrameKind(this, WindowFrameKind.Dialog);
        WindowSettings.SetSizeToContent(this, SizeToContent.WidthAndHeight);
        WindowSettings.SetCanResize(this, false);
        WindowSettings.SetWindowStartupLocation(this, WindowStartupLocation.CenterOwner);
        WindowSettings.SetMinWidth(this, 300);
        WindowSettings.SetMaxWidth(this, 560);
        var root = new DockPanel { Margin = new Thickness(0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(12, 10, 12, 12) };
        foreach ((string text, MessageResult r, bool isDefault, bool isCancel) in ButtonsFor(request.Buttons))
        {
            var b = new Button { Content = text, MinWidth = 75, IsDefault = isDefault, IsCancel = isCancel };
            b.Click += (_, _) => WindowHost.CloseDialog(this, r);
            buttons.Children.Add(b);
        }

        var footer = new Border { Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)), Child = buttons };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        var body = new DockPanel { Margin = new Thickness(12, 18, 18, 12), Background = Brushes.White };
        Control icon = PromptIcon(request.Icon);
        DockPanel.SetDock(icon, Dock.Left);
        body.Children.Add(icon);
        var stack = new StackPanel { Spacing = 14 };
        if (request.IsList)
        {
            stack.Children.Add(new TextBox
            {
                Text = request.Text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Width = 420,
                Height = 150,
                Name = "ListText",
            });
        }
        else
        {
            stack.Children.Add(new TextBlock { Text = request.Text, TextWrapping = TextWrapping.Wrap, MaxWidth = 460, VerticalAlignment = VerticalAlignment.Center });
        }

        if (request.DontShowAgainKey is not null)
        {
            _dontShow = new CheckBox { Content = Strings.DontShowAgain };
            stack.Children.Add(_dontShow);
        }

        body.Children.Add(stack);
        root.Children.Add(new Border { Background = Brushes.White, Child = body });
        Content = root;
    }

    public bool DontShowAgain => _dontShow?.IsChecked == true;

    public static MessageBoxWindow Create(string title, MessageRequest request) => new(title, request);

    private static IEnumerable<(string, MessageResult, bool, bool)> ButtonsFor(MessageButtons b) => b switch
    {
        MessageButtons.OkCancel => [(Strings.ButtonOk, MessageResult.Ok, true, false), (Strings.ButtonCancel, MessageResult.Cancel, false, true)],
        MessageButtons.YesNo => [(Strings.ButtonYes, MessageResult.Yes, true, false), (Strings.ButtonNo, MessageResult.No, false, true)],
        MessageButtons.YesNoCancel => [(Strings.ButtonYes, MessageResult.Yes, true, false), (Strings.ButtonNo, MessageResult.No, false, false), (Strings.ButtonCancel, MessageResult.Cancel, false, true)],
        _ => [(Strings.ButtonOk, MessageResult.Ok, true, true)],
    };

    private static Control PromptIcon(MessageIcon icon)
    {
        var canvas = new Canvas { Width = 32, Height = 32, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top };
        if (icon == MessageIcon.None)
        {
            canvas.Width = 0;
            return canvas;
        }

        if (icon == MessageIcon.Warning)
        {
            canvas.Children.Add(new Path
            {
                Data = Geometry.Parse("M16,2 L31,29 L1,29 Z"),
                Fill = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromRgb(0xFF, 0xE8, 0x6B), 0), new GradientStop(Color.FromRgb(0xF2, 0xB4, 0x00), 1) },
                },
                Stroke = new SolidColorBrush(Color.FromRgb(0x8A, 0x6A, 0x00)),
                StrokeThickness = 1,
            });
            canvas.Children.Add(Text("!", Colors.Black, 16, 9));
            return canvas;
        }

        (Color top, Color bottom, string glyph) = icon switch
        {
            MessageIcon.Error => (Color.FromRgb(0xF2, 0x6B, 0x5E), Color.FromRgb(0xC0, 0x1E, 0x12), "×"),
            MessageIcon.Question => (Color.FromRgb(0x6F, 0xA8, 0xF0), Color.FromRgb(0x1C, 0x54, 0xB4), "?"),
            _ => (Color.FromRgb(0x6F, 0xA8, 0xF0), Color.FromRgb(0x1C, 0x54, 0xB4), "i"),
        };
        canvas.Children.Add(new Ellipse
        {
            Width = 30,
            Height = 30,
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, 1) },
            },
            Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
            StrokeThickness = 1,
        });
        canvas.Children.Add(Text(glyph, Colors.White, 18, 7));
        return canvas;
    }

    private static TextBlock Text(string s, Color c, double size, double top)
    {
        var t = new TextBlock { Text = s, Foreground = new SolidColorBrush(c), FontSize = size, FontWeight = FontWeight.Bold, Width = 30, TextAlignment = TextAlignment.Center };
        Canvas.SetTop(t, top - 2);
        Canvas.SetLeft(t, 1);
        return t;
    }
}
