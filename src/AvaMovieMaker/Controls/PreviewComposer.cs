using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using SkiaSharp;

namespace AvaMovieMaker.Controls;

internal static class PreviewComposer
{
    private static readonly List<Slot> Slots = [];
    private static GRContext? _context;
    private static RenderDevice? _device;
    private static Compositor? _compositor;
    private static IEffectLibrary? _effects;

    public sealed class Slot
    {
        private const int Depth = 2;

        internal SKImage? Image;
        private readonly Queue<PreparedFrame> _pending = new();
        private volatile bool _retired;
        private long _composed;

        public long Composed => Interlocked.Read(ref _composed);

        internal void CountComposed() => Interlocked.Increment(ref _composed);

        public event Action? Unavailable;

        internal bool IsRetired => _retired;

        public void Retire() => _retired = true;

        public event Action? MoreQueued;

        public void Offer(PreparedFrame frame)
        {
            PreparedFrame? dropped = null;
            lock (_pending)
            {
                _pending.Enqueue(frame);
                if (_pending.Count > Depth)
                {
                    dropped = _pending.Dequeue();
                }
            }

            Drop(dropped);
        }

        public void Clear()
        {
            PreparedFrame[] waiting;
            lock (_pending)
            {
                waiting = [.. _pending];
                _pending.Clear();
            }

            foreach (PreparedFrame f in waiting)
            {
                Drop(f);
            }
        }

        internal PreparedFrame? TakeNext(out bool more)
        {
            lock (_pending)
            {
                PreparedFrame? next = _pending.Count > 0 ? _pending.Dequeue() : null;
                more = _pending.Count > 0;
                return next;
            }
        }

        internal void RaiseMoreQueued() => MoreQueued?.Invoke();

        private static void Drop(PreparedFrame? frame)
        {
            if (frame is not null && frame.TryClaim())
            {
                frame.Release();
            }
        }

        internal void RaiseUnavailable() => Unavailable?.Invoke();
    }

    public static Slot NewSlot()
    {
        var slot = new Slot();
        lock (Slots)
        {
            Slots.Add(slot);
        }

        return slot;
    }

    public static ICustomDrawOperation Operation(Slot slot, long version, Rect bounds) => new DrawOperation(slot, version, bounds);

    private static void Draw(ImmediateDrawingContext context, Slot slot, Rect bounds)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
        {
            Unavailable(slot);
            return;
        }

        using ISkiaSharpApiLease lease = feature.Lease();
        if (lease.GrContext is not { } gr)
        {
            Unavailable(slot);
            return;
        }

        if (!ReferenceEquals(gr, _context))
        {
            Reset(gr);
        }

        FreeRetired();
        if (slot.TakeNext(out bool more) is { } frame && frame.TryClaim())
        {
            if (_compositor is null || !ReferenceEquals(frame.Effects, _effects))
            {
                _compositor?.Dispose();
                _effects = frame.Effects;
                _compositor = new Compositor(_device!, frame.Effects, NoFrames.Instance);
                WarmUp(_compositor);
            }

            SKImage next = _compositor.Render(frame);
            slot.Image?.Dispose();
            slot.Image = next;
            slot.CountComposed();
            if (more)
            {
                slot.RaiseMoreQueued();
            }
        }

        if (slot.Image is { } image)
        {
            var dest = new SKRect((float)bounds.X, (float)bounds.Y, (float)bounds.Right, (float)bounds.Bottom);
            lease.SkCanvas.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Linear));
        }
    }

    private static void WarmUp(Compositor compositor)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            compositor.WarmUp();
            Log.Info("preview", $"Warmed up effect shaders in {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            Log.Warn("preview", $"Effect shader warm-up failed: {e.Message}");
        }
    }

    private static void Unavailable(Slot slot)
    {
        slot.Clear();
        slot.RaiseUnavailable();
    }

    private static void Reset(GRContext context)
    {
        lock (Slots)
        {
            foreach (Slot s in Slots)
            {
                s.Image?.Dispose();
                s.Image = null;
            }
        }

        _compositor?.Dispose();
        _compositor = null;
        _device?.Dispose();
        _context = context;
        _device = RenderDevice.Borrow(context);
    }

    private static void FreeRetired()
    {
        lock (Slots)
        {
            for (int i = Slots.Count - 1; i >= 0; i--)
            {
                if (Slots[i].IsRetired)
                {
                    Slots[i].Clear();
                    Slots[i].Image?.Dispose();
                    Slots.RemoveAt(i);
                }
            }
        }
    }

    private sealed class NoFrames : IFrameProvider
    {
        public static readonly NoFrames Instance = new();

        public AvaMovieMaker.Media.Decoding.DecodedFrame? GetFrame(string path, AvaMovieMaker.Time.MediaTime time) => null;
    }

    private sealed class DrawOperation(Slot slot, long version, Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) =>
            other is DrawOperation o && o.Version == version && o.Bounds == bounds;

        private long Version => version;

        public void Render(ImmediateDrawingContext context) => Draw(context, slot, bounds);

        public void Dispose()
        {
        }
    }
}
