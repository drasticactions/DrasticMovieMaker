using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Views;

public sealed record DragPayload
{
    public static readonly DataFormat<DragPayload> Format = DataFormat.CreateInProcessFormat<DragPayload>("avamoviemaker-drag");

    private static readonly DataFormat<string> Marker = DataFormat.CreateStringApplicationFormat("avamoviemaker-drag");

    public IReadOnlyList<(MediaItem Media, SourceClip? Clip)> Media { get; private init; } = [];

    public string? EffectId { get; private init; }

    public string? TransitionId { get; private init; }

    public bool IsStoryboardMove { get; private init; }

    public IImage? Image { get; init; }

    public Point Grab { get; init; }

    public int Count { get; init; } = 1;

    public static DragPayload ForStoryboardMove() => new() { IsStoryboardMove = true };

    public static DragPayload ForMedia(IReadOnlyList<(MediaItem, SourceClip?)> items) => new() { Media = items, Count = items.Count };

    public static DragPayload ForEffect(string id) => new() { EffectId = id };

    public static DragPayload ForTransition(string id) => new() { TransitionId = id };

    public bool IsRightDrag { get; private init; }

    public DragPayload WithImage(IImage? image, Point grab) => this with { Image = image, Grab = grab };

    public DragPayload AsRightDrag() => this with { IsRightDrag = true };

    public static DragPayload? From(DragEventArgs e) => e.DataTransfer.Contains(Format) ? e.DataTransfer.TryGetValue(Format) : null;

    public static IImage? Snapshot(Visual visual, Rect? area = null)
    {
        Rect bounds = new(visual.Bounds.Size);
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return null;
        }

        double scale = TopLevel.GetTopLevel(visual)?.RenderScaling ?? 1;
        var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(bounds.Width * scale), (int)Math.Ceiling(bounds.Height * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(visual);
        Rect a = (area ?? bounds).Intersect(bounds);
        if (a.Width < 1 || a.Height < 1)
        {
            return null;
        }

        var pixels = new PixelRect((int)(a.X * scale), (int)(a.Y * scale), (int)Math.Round(a.Width * scale), (int)Math.Round(a.Height * scale))
            .Intersect(new PixelRect(bitmap.PixelSize));
        return new ScaledImage(bitmap, pixels, scale);
    }

    public static async Task DoDragAsync(PointerPressedEventArgs pressed, DragPayload payload)
    {
        if (pressed.GetCurrentPoint(null).Properties.IsRightButtonPressed)
        {
            payload = payload.AsRightDrag();
        }

        DataTransferItem item = DataTransferItem.Create(Format, payload);
        item.Set(Marker, "AvaMovieMaker");
        var data = new DataTransfer();
        data.Add(item);
        TopLevel? top = TopLevel.GetTopLevel(pressed.Source as Visual);
        try
        {
            await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally
        {
            if (top is not null)
            {
                DragGhost.Hide(top);
            }
        }
    }
}
