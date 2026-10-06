using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaMovieMaker.Views;

public static class DragGhost
{
    private static readonly AttachedProperty<Control?> GhostProperty =
        AvaloniaProperty.RegisterAttached<TopLevel, Control?>("DragGhost", typeof(DragGhost));

    public static void Attach(TopLevel top)
    {
        top.AddHandler(DragDrop.DragEnterEvent, (_, e) => Move(top, e), RoutingStrategies.Bubble, handledEventsToo: true);
        top.AddHandler(DragDrop.DragOverEvent, (_, e) => Move(top, e), RoutingStrategies.Bubble, handledEventsToo: true);
        top.AddHandler(DragDrop.DropEvent, (_, _) => Hide(top), RoutingStrategies.Bubble, handledEventsToo: true);
        top.AddHandler(DragDrop.DragLeaveEvent, (_, e) =>
        {
            if (!new Rect(top.Bounds.Size).Contains(e.GetPosition(top)))
            {
                Hide(top);
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void Move(TopLevel top, DragEventArgs e)
    {
        if (DragPayload.From(e) is not { Image: { } image } payload || OverlayLayer.GetOverlayLayer(top) is not { } layer)
        {
            return;
        }

        Control ghost = top.GetValue(GhostProperty) ?? Create(top, layer, payload, image);
        Point p = e.GetPosition(layer);
        Canvas.SetLeft(ghost, p.X - payload.Grab.X);
        Canvas.SetTop(ghost, p.Y - payload.Grab.Y);
    }

    private static Control Create(TopLevel top, OverlayLayer layer, DragPayload payload, IImage image)
    {
        var panel = new Panel
        {
            IsHitTestVisible = false,
            Opacity = 0.6,
            Width = image.Size.Width,
            Height = image.Size.Height,
            Children = { new Image { Source = image, Stretch = Stretch.None } },
        };
        if (payload.Count > 1)
        {
            panel.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = new TextBlock { Text = payload.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = Brushes.Black },
            });
        }

        layer.Children.Add(panel);
        top.SetValue(GhostProperty, panel);
        return panel;
    }

    public static void Hide(TopLevel top)
    {
        if (top.GetValue(GhostProperty) is { } ghost)
        {
            (ghost.Parent as Panel)?.Children.Remove(ghost);
            top.SetValue(GhostProperty, null);
        }
    }
}
