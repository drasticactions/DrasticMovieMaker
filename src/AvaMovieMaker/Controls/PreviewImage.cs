using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Rendering.Compositing;
using AvaMovieMaker.Rendering.Gpu;
using AvaMovieMaker.Timeline.Playback;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Controls;

public sealed class PreviewImage : Control, IPreviewSurface
{
    public static readonly StyledProperty<double> AspectProperty = AvaloniaProperty.Register<PreviewImage, double>(nameof(Aspect), 4.0 / 3.0);

    private const string HandleType = KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;
    private const string SemaphoreType = KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;

    private WriteableBitmap? _bitmap;
    private readonly List<PreviewFrame> _pending = [];
    private readonly Lock _gate = new();
    private ICompositionGpuInterop? _interop;
    private CompositionDrawingSurface? _gpuSurface;
    private CompositionSurfaceVisual? _gpuVisual;
    private readonly Dictionary<SharedFrameBuffer, Imported> _imports = [];
    private bool _showingGpu;
    private bool _sharedShown;
    private int _attachGeneration;

    static PreviewImage() => AffectsRender<PreviewImage>(AspectProperty);

    public double Aspect
    {
        get => GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    private PreviewComposer.Slot? _slot;
    private PreparedFrame? _prepared;
    private long _drawVersion;
    private bool _composeFailed;
    private bool _composeFallback;
    private DispatcherTimer? _runTimer;
    private long _runStartShown;
    private DateTime _runStart, _runLast;
    private long _framesCopied;

    public long FramesShown => _framesCopied + (_slot?.Composed ?? 0);

    public PixelSize LastFrameSize { get; private set; }

    public event Action<int, int>? PixelSizeChanged;

    public byte[]? SharedFrameDevice { get; private set; }

    public bool ComposesFrames => (App.Platform.ComposePreviewOnInterfaceGpu || _composeFallback) && !_composeFailed;

    public event EventHandler? ComposesFramesChanged;

    public event EventHandler? SharedFrameDeviceChanged;

    public Rect VideoRect
    {
        get
        {
            Rect b = new(Bounds.Size);
            double w = b.Width, h = w / Aspect;
            if (h > b.Height)
            {
                h = b.Height;
                w = h * Aspect;
            }

            return new Rect((b.Width - w) / 2, (b.Height - h) / 2, w, h);
        }
    }

    public void Present(PreviewFrame frame)
    {
        lock (_gate)
        {
            _pending.Add(frame);
        }

        Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
    }

    private void Apply()
    {
        PreviewFrame[] frames;
        lock (_gate)
        {
            frames = [.. _pending];
            _pending.Clear();
        }

        if (frames.Length == 0)
        {
            return;
        }

        for (int i = 0; i < frames.Length - 1; i++)
        {
            frames[i].Drop();
        }

        PreviewFrame f = frames[^1];
        LastFrameSize = new PixelSize(f.Width, f.Height);
        CountRun();
        if (f.Prepared is { } prepared)
        {
            ShowPrepared(prepared);
            return;
        }

        _framesCopied++;
        if (f.Shared is { } shared)
        {
            ShowShared(shared);
        }
        else
        {
            ShowPixels(f);
        }
    }

    private string PathName => ComposesFrames ? "composed on the interface's GPU" : _showingGpu ? "shared GPU frames" : "pixels";

    private void CountRun()
    {
        DateTime now = DateTime.UtcNow;
        if (_runTimer is null)
        {
            _runTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _runTimer.Tick += (_, _) =>
            {
                _runTimer.Stop();
                long shown = FramesShown - _runStartShown;
                double seconds = (_runLast - _runStart).TotalSeconds;
                if (shown >= 10 && seconds > 0)
                {
                    Log.Info("preview", $"Showed {shown} frames in {seconds:0.0} s ({shown / seconds:0.0} fps) at {LastFrameSize.Width}x{LastFrameSize.Height}, {PathName}");
                }

                _runStartShown = -1;
            };
            _runStartShown = -1;
        }

        if (_runStartShown < 0)
        {
            _runStartShown = FramesShown;
            _runStart = now;
        }

        _runLast = now;
        _runTimer.Stop();
        _runTimer.Start();
    }

    private void ShowPrepared(PreparedFrame prepared)
    {
        if (_slot is null)
        {
            if (prepared.TryClaim())
            {
                prepared.Release();
            }

            return;
        }

        _slot.Offer(prepared);
        _prepared = prepared;
        _drawVersion++;
        SetShowingGpu(false);
        InvalidateVisual();
    }

    private void ComposeInstead(string why)
    {
        if (_composeFallback || App.Platform.ComposePreviewOnInterfaceGpu || _composeFailed)
        {
            return;
        }

        _composeFallback = true;
        Log.Info("preview", $"Preview frames are composed on the interface's GPU: {why}");
        if (_slot is null && TopLevel.GetTopLevel(this) is not null)
        {
            NewSlot();
        }

        ComposesFramesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NewSlot()
    {
        _slot = PreviewComposer.NewSlot();
        _slot.Unavailable += OnComposerUnavailable;
        _slot.MoreQueued += OnMoreQueued;
    }

    public void SharedFramesRefused() => ComposeInstead("the engine's device cannot share frames with the compositor (see log)");

    private void OnMoreQueued() => Dispatcher.UIThread.Post(() =>
    {
        if (_prepared is not null)
        {
            _drawVersion++;
            InvalidateVisual();
        }
    }, DispatcherPriority.Render);

    private void OnComposerUnavailable() => Dispatcher.UIThread.Post(() =>
    {
        if (_composeFailed)
        {
            return;
        }

        _composeFailed = true;
        Log.Warn("preview", "The interface has no GPU context to compose on; preview frames are composed by the engine");
        ComposesFramesChanged?.Invoke(this, EventArgs.Empty);
    });

    private void DropPrepared()
    {
        _slot?.Clear();
        _prepared = null;
    }

    private void ShowPixels(PreviewFrame f)
    {
        DropPrepared();
        if (_bitmap is null || _bitmap.PixelSize.Width != f.Width || _bitmap.PixelSize.Height != f.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(f.Width, f.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        using (ILockedFramebuffer fb = _bitmap.Lock())
        {
            unsafe
            {
                fixed (byte* src = f.Pixels)
                {
                    for (int y = 0; y < f.Height; y++)
                    {
                        Buffer.MemoryCopy(src + y * f.Width * 4, (byte*)fb.Address + y * fb.RowBytes, fb.RowBytes, f.Width * 4);
                    }
                }
            }
        }

        SetShowingGpu(false);
        InvalidateVisual();
    }

    private void ShowShared(SharedFrameBuffer buffer)
    {
        DropPrepared();
        if (_interop is not { IsLost: false } || _gpuSurface is null)
        {
            buffer.Release(consumed: false);
            return;
        }

        if (!_imports.TryGetValue(buffer, out Imported? imp))
        {
            imp = Import(buffer);
            if (imp is null)
            {
                buffer.Release(consumed: false);
                DisableShared("importing a shared frame failed");
                return;
            }

            _imports[buffer] = imp;
            buffer.Retired += OnRetired;
        }

        _ = UpdateAsync(_gpuSurface, buffer, imp);
        SetShowingGpu(true);
    }

    private async Task UpdateAsync(CompositionDrawingSurface surface, SharedFrameBuffer buffer, Imported imp)
    {
        bool consumed = false;
        try
        {
            await imp.Image.ImportCompleted.ConfigureAwait(true);
            await imp.Rendered.ImportCompleted.ConfigureAwait(true);
            await imp.Released.ImportCompleted.ConfigureAwait(true);
            await surface.UpdateWithSemaphoresAsync(imp.Image, imp.Rendered, imp.Released).ConfigureAwait(true);
            consumed = true;
            if (!_sharedShown)
            {
                _sharedShown = true;
                Log.Info("preview", $"Showing shared GPU frames without readback ({buffer.Width}x{buffer.Height})");
            }
        }
        catch (Exception e)
        {
            if (ReferenceEquals(surface, _gpuSurface))
            {
                DisableShared($"a shared frame update failed ({e.Message})");
            }
        }
        finally
        {
            buffer.Release(consumed);
        }
    }

    private Imported? Import(SharedFrameBuffer buffer)
    {
        int memory = buffer.ExportMemoryFd(), rendered = buffer.ExportRenderedSemaphoreFd(), released = buffer.ExportReleasedSemaphoreFd();
        if (memory < 0 || rendered < 0 || released < 0)
        {
            return null;
        }

        try
        {
            ICompositionImportedGpuImage image = _interop!.ImportImage(new PlatformHandle(memory, HandleType), new PlatformGraphicsExternalImageProperties
            {
                Width = buffer.Width,
                Height = buffer.Height,
                Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
                MemorySize = buffer.MemorySize,
                MemoryOffset = 0,
                TopLeftOrigin = true,
            });
            ICompositionImportedGpuSemaphore r = _interop.ImportSemaphore(new PlatformHandle(rendered, SemaphoreType));
            ICompositionImportedGpuSemaphore s = _interop.ImportSemaphore(new PlatformHandle(released, SemaphoreType));
            return new Imported(image, r, s);
        }
        catch (Exception e)
        {
            Log.Warn("preview", $"Shared frame import failed: {e.Message}");
            return null;
        }
    }

    private void OnRetired(SharedFrameBuffer buffer) => Dispatcher.UIThread.Post(() =>
    {
        if (_imports.Remove(buffer, out Imported? imp))
        {
            imp.Dispose();
        }
    });

    private void SetShowingGpu(bool gpu)
    {
        if (_showingGpu == gpu)
        {
            return;
        }

        _showingGpu = gpu;
        if (_gpuVisual is not null)
        {
            _gpuVisual.Visible = gpu;
        }

        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (App.Platform.ComposePreviewOnInterfaceGpu || _composeFallback)
        {
            NewSlot();
        }

        _ = EnableSharedAsync(++_attachGeneration);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DropPrepared();
        if (_slot is not null)
        {
            _framesCopied += _slot.Composed;
            _slot.Unavailable -= OnComposerUnavailable;
            _slot.MoreQueued -= OnMoreQueued;
            _slot.Retire();
            _slot = null;
        }

        _attachGeneration++;
        TearDownShared();
    }

    private async Task EnableSharedAsync(int generation)
    {
        if (ElementComposition.GetElementVisual(this)?.Compositor is not { } compositor)
        {
            return;
        }

        ICompositionGpuInterop? interop;
        try
        {
            interop = await compositor.TryGetCompositionGpuInterop().ConfigureAwait(true);
        }
        catch (Exception e)
        {
            Log.Info("preview", $"No compositor GPU interop: {e.Message}");
            return;
        }

        if (generation != _attachGeneration)
        {
            return;
        }

        string? forced = Environment.GetEnvironmentVariable("AMM_PREVIEW_PATH");
        if (forced == "readback")
        {
            _composeFailed = true;
            Log.Info("preview", "Preview frames are read back (AMM_PREVIEW_PATH=readback)");
            ComposesFramesChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        string? why = forced == "compose" ? "AMM_PREVIEW_PATH=compose"
            : interop is null ? "the compositor has no GPU interop (software or headless rendering)"
            : !interop.SupportedImageHandleTypes.Contains(HandleType) ? $"the compositor imports [{string.Join(", ", interop.SupportedImageHandleTypes)}], not {HandleType}"
            : !interop.SupportedSemaphoreTypes.Contains(SemaphoreType) ? $"the compositor imports no {SemaphoreType} semaphores"
            : !interop.GetSynchronizationCapabilities(HandleType).HasFlag(CompositionGpuImportedImageSynchronizationCapabilities.Semaphores) ? "the compositor cannot synchronize with semaphores"
            : interop.DeviceUuid is not { Length: 16 } ? "the compositor does not report its GPU UUID"
            : null;
        if (why is not null)
        {
            ComposeInstead(why);
            return;
        }

        _interop = interop;
        _gpuSurface = compositor.CreateDrawingSurface();
        _gpuVisual = compositor.CreateSurfaceVisual();
        _gpuVisual.Surface = _gpuSurface;
        _gpuVisual.Visible = _showingGpu;
        PlaceVisual();
        ElementComposition.SetElementChildVisual(this, _gpuVisual);
        Log.Info("preview", $"Compositor imports {HandleType} images and semaphores; offering shared GPU frames");
        SharedFrameDevice = interop!.DeviceUuid;
        SharedFrameDeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DisableShared(string why)
    {
        Log.Warn("preview", $"Shared GPU frames off: {why}");
        TearDownShared();
        ComposeInstead(why);
    }

    private void TearDownShared()
    {
        if (_interop is null)
        {
            return;
        }

        foreach ((SharedFrameBuffer buffer, Imported imp) in _imports)
        {
            buffer.Retired -= OnRetired;
            imp.Dispose();
        }

        _imports.Clear();
        ElementComposition.SetElementChildVisual(this, null);
        _gpuSurface?.Dispose();
        _gpuSurface = null;
        _gpuVisual = null;
        _interop = null;
        _showingGpu = false;
        SharedFrameDevice = null;
        SharedFrameDeviceChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void PlaceVisual()
    {
        if (_gpuVisual is null)
        {
            return;
        }

        Rect v = VideoRect;
        _gpuVisual.Offset = new Vector3D(v.X, v.Y, 0);
        _gpuVisual.Size = new Vector(v.Width, v.Height);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PlaceVisual();
        ReportSize();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AspectProperty)
        {
            PlaceVisual();
            ReportSize();
        }
    }

    public void ReportSize()
    {
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        Rect v = VideoRect;
        PixelSizeChanged?.Invoke((int)(v.Width * scale), (int)(v.Height * scale));
    }

    public override void Render(DrawingContext context)
    {
        Rect v = VideoRect;
        context.FillRectangle(Brushes.Black, v);
        if (_slot is not null && _prepared is not null)
        {
            context.Custom(PreviewComposer.Operation(_slot, _drawVersion, v));
        }
        else if (_bitmap is not null && !_showingGpu)
        {
            context.DrawImage(_bitmap, new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height), v);
        }
    }

    private sealed record Imported(ICompositionImportedGpuImage Image, ICompositionImportedGpuSemaphore Rendered, ICompositionImportedGpuSemaphore Released)
    {
        public void Dispose()
        {
            _ = Image.DisposeAsync();
            _ = Rendered.DisposeAsync();
            _ = Released.DisposeAsync();
        }
    }
}
