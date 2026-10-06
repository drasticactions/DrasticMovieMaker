using System.Globalization;
using AvaMovieMaker.Diagnostics;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Gpu;

public sealed class RenderDevice : IDisposable
{
    private readonly RenderThread? _thread;
    private EglContext? _egl;
    private GRGlInterface? _gl;
    private MetalDevice? _metal;

    private RenderDevice(RenderThread? thread)
    {
        _thread = thread;
    }

    public RenderThread Thread => _thread ?? throw new InvalidOperationException("A borrowed render device has no render thread.");

    public GRContext? Context { get; private set; }

    public bool IsGpu => Context is not null;

    internal EglContext? Egl => _egl;

    internal DmaBufImporter? DmaBuf { get; private set; }

    public bool SupportsZeroCopy => DmaBuf is not null;

    public byte[]? DeviceUuid { get; private set; }

    public string Description { get; private set; } = Strings.RendererSoftware;

    public static RenderDevice Create(bool preferGpu = true)
    {
        var device = new RenderDevice(new RenderThread());
        if (preferGpu && Environment.GetEnvironmentVariable("AMM_SOFTWARE_RENDER") != "1")
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                device.Thread.Invoke(device.InitGpu);
            }
            else if (OperatingSystem.IsMacOS())
            {
                device.Thread.Invoke(device.InitMetal);
            }
        }

        Log.Info("render", device.Description);
        return device;
    }

    public static RenderDevice Borrow(GRContext context) => new(null) { Context = context, Description = Strings.RendererBorrowed };

    private void InitGpu()
    {
        _egl = EglContext.TryCreate();
        if (_egl is null)
        {
            return;
        }

        _gl = _egl.IsGles ? GRGlInterface.CreateGles(_egl.GetProcAddress) : GRGlInterface.CreateOpenGl(_egl.GetProcAddress);
        if (_gl is null || !_gl.Validate())
        {
            Log.Warn("render", "GL interface did not validate; using CPU raster");
            Cleanup();
            return;
        }

        Context = GRContext.CreateGl(_gl);
        if (Context is null)
        {
            Log.Warn("render", "GRContext creation failed; using CPU raster");
            Cleanup();
            return;
        }

        Context.SetResourceCacheLimit(256L * 1024 * 1024);
        if (OperatingSystem.IsLinux())
        {
            DmaBuf = DmaBufImporter.TryCreate(_egl);
            DeviceUuid = GlExternalObjects.TryCreate(_egl)?.DeviceUuidOf();
        }

        Description = string.Format(CultureInfo.CurrentCulture, DmaBuf is null ? Strings.RendererGpu : Strings.RendererGpuZeroCopy, _egl.Renderer);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private void InitMetal()
    {
        _metal = MetalDevice.TryCreate();
        if (_metal is null)
        {
            return;
        }

        using var backend = new GRMtlBackendContext { DeviceHandle = _metal.Device, QueueHandle = _metal.Queue };
        Context = GRContext.CreateMetal(backend);
        if (Context is null)
        {
            Log.Warn("render", "Metal GRContext creation failed; using CPU raster");
            Cleanup();
            return;
        }

        Context.SetResourceCacheLimit(256L * 1024 * 1024);
        Description = string.Format(CultureInfo.CurrentCulture, Strings.RendererMetal, _metal.Name);
    }

    public SKSurface CreateSurface(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        if (Context is not null)
        {
            SKSurface? s = SKSurface.Create(Context, false, info);
            if (s is not null)
            {
                return s;
            }
        }

        return SKSurface.Create(info) ?? throw new InvalidOperationException($"Could not create a {width}x{height} surface.");
    }

    public SKImage Upload(SKImage raster)
    {
        if (Context is null)
        {
            return raster;
        }

        SKImage? tex = raster.ToTextureImage(Context, false);
        if (tex is null)
        {
            return raster;
        }

        raster.Dispose();
        return tex;
    }

    public void Flush() => Context?.Flush(true, false);

    private void Cleanup()
    {
        Context?.AbandonContext(true);
        Context?.Dispose();
        Context = null;
        _gl?.Dispose();
        _gl = null;
        _egl?.Dispose();
        _egl = null;
        _metal?.Dispose();
        _metal = null;
    }

    public void Dispose()
    {
        if (_thread is null)
        {
            Context = null;
            return;
        }

        _thread.Invoke(() =>
        {
            Context?.Flush(true, true);
            Context?.Dispose();
            Context = null;
            _gl?.Dispose();
            _gl = null;
            _egl?.Dispose();
            _egl = null;
            _metal?.Dispose();
            _metal = null;
        });
        _thread.Dispose();
    }
}
