using System.Runtime.InteropServices;
using AvaMovieMaker.Diagnostics;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Gpu;

public sealed partial class SharedFramePool : IDisposable
{
    private const int MaxBuffersPerSize = 3;

    private readonly RenderDevice _device;
    private readonly GlExternalObjects _gl;
    private readonly VulkanExportDevice _vk;
    private readonly List<SharedFrameBuffer> _buffers = [];
    private readonly Lock _gate = new();
    private int _nextId;
    private bool _disposed;

    private SharedFramePool(RenderDevice device, GlExternalObjects gl, VulkanExportDevice vk)
    {
        _device = device;
        _gl = gl;
        _vk = vk;
    }

    public string Description => $"shared GPU frames on {_vk.Name}";

    public static SharedFramePool? TryCreate(RenderDevice device, byte[] compositorDeviceUuid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        string? why = null;
        SharedFramePool? pool = device.Thread.Invoke(() => Create(device, compositorDeviceUuid, out why));
        if (pool is null)
        {
            Log.Info("render", $"No shared preview frames: {why}");
        }
        else
        {
            Log.Info("render", $"Preview without readback: {pool.Description}");
        }

        return pool;
    }

    private static SharedFramePool? Create(RenderDevice device, byte[] uuid, out string? why)
    {
        if (device.Egl is not { } egl || device.Context is null)
        {
            why = "software rendering";
            return null;
        }

        GlExternalObjects? gl = GlExternalObjects.TryCreate(egl);
        if (gl is null)
        {
            why = "the render context has no GL_EXT_memory_object_fd / GL_EXT_semaphore_fd";
            return null;
        }

        if (gl.DeviceUuidOf() is not { } ours || !ours.AsSpan().SequenceEqual(uuid))
        {
            why = "the render context is not on the compositor's GPU";
            return null;
        }

        VulkanExportDevice? vk = VulkanExportDevice.TryCreate(uuid, out why);
        if (vk is null)
        {
            return null;
        }

        var pool = new SharedFramePool(device, gl, vk);
        why = pool.Probe();
        if (why is not null)
        {
            pool.Dispose();
            return null;
        }

        return pool;
    }

    public SharedFrameBuffer? TryPresent(SKImage image)
    {
        SharedFrameBuffer? b = Acquire(image.Width, image.Height);
        if (b is null)
        {
            return null;
        }

        WaitUntilFree(b);
        using (var paint = new SKPaint { BlendMode = SKBlendMode.Src })
        {
            b.Surface.Canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default, paint);
        }

        b.Surface.Flush(submit: true);
        _gl.Signal(b.GlRendered, b.GlTexture, GlExternalObjects.LayoutTransferSrc);
        _gl.Flush();
        return b;
    }

    private void WaitUntilFree(SharedFrameBuffer b)
    {
        switch (b.PendingWait)
        {
            case SharedFrameBuffer.Wait.Released:
                _gl.Wait(b.GlReleased, b.GlTexture, GlExternalObjects.LayoutNone);
                break;
            case SharedFrameBuffer.Wait.Rendered:
                _gl.Wait(b.GlRendered, b.GlTexture, GlExternalObjects.LayoutTransferSrc);
                break;
        }

        b.PendingWait = SharedFrameBuffer.Wait.None;
    }

    private SharedFrameBuffer? Acquire(int width, int height)
    {
        List<SharedFrameBuffer> stale = [];
        SharedFrameBuffer? free = null;
        int sameSize = 0;
        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            foreach (SharedFrameBuffer b in _buffers)
            {
                if (b.Width != width || b.Height != height)
                {
                    if (!b.InFlight)
                    {
                        stale.Add(b);
                    }

                    continue;
                }

                sameSize++;
                if (!b.InFlight && free is null)
                {
                    free = b;
                }
            }

            foreach (SharedFrameBuffer b in stale)
            {
                _buffers.Remove(b);
            }

            if (free is not null)
            {
                free.InFlight = true;
            }
        }

        foreach (SharedFrameBuffer b in stale)
        {
            Destroy(b);
        }

        if (free is not null || sameSize >= MaxBuffersPerSize)
        {
            return free;
        }

        SharedFrameBuffer? created = CreateBuffer(width, height);
        if (created is not null)
        {
            created.InFlight = true;
            lock (_gate)
            {
                _buffers.Add(created);
            }
        }

        return created;
    }

    private SharedFrameBuffer? CreateBuffer(int width, int height)
    {
        ulong size = (ulong)(((width * 4) + 255) & ~255) * (ulong)((height + 63) & ~63) + 65536;
        if (_vk.Allocate(size) is not { } alloc)
        {
            Log.Warn("render", $"Could not allocate a {width}x{height} shared frame");
            return null;
        }

        ulong semRendered = _vk.CreateSemaphore(), semReleased = _vk.CreateSemaphore();
        var b = new SharedFrameBuffer(this, Interlocked.Increment(ref _nextId), width, height, alloc.Buffer, alloc.Memory, alloc.Size, semRendered, semReleased);
        if (semRendered == 0 || semReleased == 0)
        {
            Destroy(b);
            return null;
        }

        int fd = _vk.ExportMemory(alloc.Memory);
        if (fd < 0 || _gl.ImportTexture(fd, alloc.Size, width, height) is not { } tex)
        {
            CloseFd(fd);
            Destroy(b);
            return null;
        }

        b.GlMemory = tex.Memory;
        b.GlTexture = tex.Texture;
        b.GlRendered = ImportSemaphore(semRendered);
        b.GlReleased = ImportSemaphore(semReleased);
        _device.Context!.ResetContext();
        var backend = new GRBackendTexture(width, height, false, new GRGlTextureInfo(GlExternalObjects.Texture2D, tex.Texture, GlExternalObjects.Rgba8));
        b.SurfaceOrNull = SKSurface.Create(_device.Context, backend, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);
        if (b.GlRendered == 0 || b.GlReleased == 0 || b.SurfaceOrNull is null)
        {
            Destroy(b);
            return null;
        }

        return b;
    }

    private uint ImportSemaphore(ulong semaphore)
    {
        int fd = _vk.ExportSemaphore(semaphore);
        if (fd < 0)
        {
            return 0;
        }

        uint s = _gl.ImportSemaphore(fd);
        if (s == 0)
        {
            CloseFd(fd);
        }

        return s;
    }

    private void Destroy(SharedFrameBuffer b)
    {
        b.SurfaceOrNull?.Dispose();
        b.SurfaceOrNull = null;
        if (b.GlTexture != 0)
        {
            _gl.DeleteTexture(b.GlMemory, b.GlTexture);
        }

        if (b.GlRendered != 0)
        {
            _gl.DeleteSemaphore(b.GlRendered);
        }

        if (b.GlReleased != 0)
        {
            _gl.DeleteSemaphore(b.GlReleased);
        }

        _device.Context?.ResetContext();
        if (b.SemRendered != 0)
        {
            _vk.DestroySemaphore(b.SemRendered);
        }

        if (b.SemReleased != 0)
        {
            _vk.DestroySemaphore(b.SemReleased);
        }

        _vk.Free(b.VkBuffer, b.VkMemory);
        b.RaiseRetired();
    }

    internal int ExportMemory(SharedFrameBuffer b) => _vk.ExportMemory(b.VkMemory);

    internal int ExportSemaphore(ulong semaphore) => _vk.ExportSemaphore(semaphore);

    internal void Released(SharedFrameBuffer b, bool consumed)
    {
        bool destroy;
        lock (_gate)
        {
            b.PendingWait = consumed ? SharedFrameBuffer.Wait.Released : SharedFrameBuffer.Wait.Rendered;
            b.InFlight = false;
            destroy = _disposed;
            if (destroy)
            {
                _buffers.Remove(b);
            }
        }

        if (destroy)
        {
            OnRenderThread(() =>
            {
                Destroy(b);
                ReleaseDeviceIfIdle();
            });
        }
    }

    private void OnRenderThread(Action action)
    {
        try
        {
            _ = _device.Thread.InvokeAsync(action);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ReleaseDeviceIfIdle()
    {
        lock (_gate)
        {
            if (!_disposed || _buffers.Count > 0)
            {
                return;
            }
        }

        _vk.Dispose();
    }

    private string? Probe()
    {
        const int w = 173, h = 97;
        SharedFrameBuffer? b = Acquire(w, h);
        if (b is null)
        {
            return "could not create a shared frame";
        }

        using (SKSurface pattern = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            DrawPattern(pattern.Canvas, w, h);
            using SKImage img = pattern.Snapshot();
            WaitUntilFree(b);
            using var src = new SKPaint { BlendMode = SKBlendMode.Src };
            b.Surface.Canvas.DrawImage(img, 0, 0, SKSamplingOptions.Default, src);
            b.Surface.Flush(submit: true);
            _gl.Signal(b.GlRendered, b.GlTexture, GlExternalObjects.LayoutTransferSrc);
            _gl.Flush();
        }

        byte[] px = new byte[w * h * 4];
        string? why = ReadAsCompositor(b, px);
        Released(b, consumed: why is null);
        if (why is not null)
        {
            return why;
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                (byte r, byte g, byte bl) = PatternAt(x, y, w, h);
                int i = (y * w + x) * 4;
                if (px[i] != r || px[i + 1] != g || px[i + 2] != bl || px[i + 3] != 255)
                {
                    return $"the compositor-style import reads different pixels (first at {x},{y})";
                }
            }
        }

        return null;
    }

    internal string? ReadAsCompositor(SharedFrameBuffer b, byte[] px)
    {
        EglContext egl = _device.Egl!;
        EglContext? other = egl.CreateSibling();
        if (other is null)
        {
            egl.MakeCurrent();
            return "no second GL context for the import probe";
        }

        try
        {
            GlExternalObjects? gl = GlExternalObjects.TryCreate(other);
            if (gl is null)
            {
                return "the probe context has no external objects";
            }

            int fd = b.ExportMemoryFd();
            if (fd < 0 || gl.ImportTexture(fd, b.MemorySize, b.Width, b.Height) is not { } tex)
            {
                CloseFd(fd);
                return "the compositor-style texture import failed";
            }

            uint rendered = ImportInto(gl, b.ExportRenderedSemaphoreFd());
            uint released = ImportInto(gl, b.ExportReleasedSemaphoreFd());
            string? why = null;
            if (rendered == 0 || released == 0)
            {
                why = "the compositor-style semaphore import failed";
            }
            else
            {
                gl.Wait(rendered, tex.Texture, GlExternalObjects.LayoutTransferSrc);
                if (!gl.ReadTexture(tex.Texture, b.Width, b.Height, px))
                {
                    why = "reading the imported texture failed";
                }

                gl.Signal(released, tex.Texture, GlExternalObjects.LayoutNone);
                gl.Flush();
            }

            if (rendered != 0)
            {
                gl.DeleteSemaphore(rendered);
            }

            if (released != 0)
            {
                gl.DeleteSemaphore(released);
            }

            gl.DeleteTexture(tex.Memory, tex.Texture);
            return why;
        }
        finally
        {
            egl.MakeCurrent();
            other.Dispose();
            _device.Context!.ResetContext();
        }
    }

    private static uint ImportInto(GlExternalObjects gl, int fd)
    {
        if (fd < 0)
        {
            return 0;
        }

        uint s = gl.ImportSemaphore(fd);
        if (s == 0)
        {
            CloseFd(fd);
        }

        return s;
    }

    private static void DrawPattern(SKCanvas canvas, int w, int h)
    {
        int mx = w / 2, my = h / 2;
        using var paint = new SKPaint { IsAntialias = false };
        foreach ((int x0, int y0, int x1, int y1) in new[] { (0, 0, mx, my), (mx, 0, w, my), (0, my, mx, h), (mx, my, w, h) })
        {
            (byte r, byte g, byte b) = PatternAt(x0, y0, w, h);
            paint.Color = new SKColor(r, g, b);
            canvas.DrawRect(SKRect.Create(x0, y0, x1 - x0, y1 - y0), paint);
        }
    }

    private static (byte R, byte G, byte B) PatternAt(int x, int y, int w, int h) => (x < w / 2, y < h / 2) switch
    {
        (true, true) => (255, 0, 0),
        (false, true) => (0, 255, 0),
        (true, false) => (0, 0, 255),
        _ => (255, 255, 255),
    };

    private static void CloseFd(int fd)
    {
        if (fd >= 0)
        {
            Libc.close(fd);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        void Free()
        {
            List<SharedFrameBuffer> idle;
            lock (_gate)
            {
                idle = _buffers.Where(b => !b.InFlight).ToList();
                foreach (SharedFrameBuffer b in idle)
                {
                    _buffers.Remove(b);
                }
            }

            foreach (SharedFrameBuffer b in idle)
            {
                Destroy(b);
            }

            ReleaseDeviceIfIdle();
        }

        if (_device.Thread.IsCurrent)
        {
            Free();
        }
        else
        {
            OnRenderThread(Free);
        }
    }

    private static partial class Libc
    {
        [LibraryImport("libc", SetLastError = false)]
        public static partial int close(int fd);
    }
}

public sealed class SharedFrameBuffer
{
    internal enum Wait
    {
        None,
        Released,
        Rendered,
    }

    private readonly SharedFramePool _pool;

    internal SharedFrameBuffer(SharedFramePool pool, int id, int width, int height, ulong vkBuffer, ulong vkMemory, ulong size, ulong semRendered, ulong semReleased)
    {
        _pool = pool;
        Id = id;
        Width = width;
        Height = height;
        VkBuffer = vkBuffer;
        VkMemory = vkMemory;
        MemorySize = size;
        SemRendered = semRendered;
        SemReleased = semReleased;
    }

    public int Id { get; }

    public int Width { get; }

    public int Height { get; }

    public ulong MemorySize { get; }

    public event Action<SharedFrameBuffer>? Retired;

    internal ulong VkBuffer { get; }

    internal ulong VkMemory { get; }

    internal ulong SemRendered { get; }

    internal ulong SemReleased { get; }

    internal uint GlMemory { get; set; }

    internal uint GlTexture { get; set; }

    internal uint GlRendered { get; set; }

    internal uint GlReleased { get; set; }

    internal SKSurface? SurfaceOrNull { get; set; }

    internal SKSurface Surface => SurfaceOrNull!;

    internal bool InFlight { get; set; }

    internal Wait PendingWait { get; set; }

    public int ExportMemoryFd() => _pool.ExportMemory(this);

    public int ExportRenderedSemaphoreFd() => _pool.ExportSemaphore(SemRendered);

    public int ExportReleasedSemaphoreFd() => _pool.ExportSemaphore(SemReleased);

    public void Release(bool consumed) => _pool.Released(this, consumed);

    internal void RaiseRetired() => Retired?.Invoke(this);
}
