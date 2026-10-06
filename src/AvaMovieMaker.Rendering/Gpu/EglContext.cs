using AvaMovieMaker.Diagnostics;

namespace AvaMovieMaker.Rendering.Gpu;

internal sealed class EglContext : IDisposable
{
    private IntPtr _display;
    private IntPtr _context;
    private IntPtr _surface;
    private readonly IntPtr _config;
    private readonly bool _ownsDisplay;

    private EglContext(IntPtr display, IntPtr context, IntPtr surface, IntPtr config, bool gles, bool ownsDisplay = true)
    {
        _display = display;
        _context = context;
        _surface = surface;
        _config = config;
        _ownsDisplay = ownsDisplay;
        IsGles = gles;
    }

    public bool IsGles { get; }

    public string Renderer { get; private set; } = string.Empty;

    public IntPtr Display => _display;

    public string Extensions => System.Runtime.InteropServices.Marshal.PtrToStringUTF8(Egl.eglQueryString(_display, Egl.EGL_EXTENSIONS)) ?? string.Empty;

    public static EglContext? TryCreate()
    {
        try
        {
            return Create();
        }
        catch (DllNotFoundException e)
        {
            Log.Info("render", $"EGL not available: {e.Message}");
            return null;
        }
        catch (EntryPointNotFoundException e)
        {
            Log.Info("render", $"EGL incomplete: {e.Message}");
            return null;
        }
        catch (TypeInitializationException e) when (e.InnerException is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Info("render", $"EGL not available: {e.InnerException.Message}");
            return null;
        }
    }

    private static EglContext? Create()
    {
        IntPtr display = OperatingSystem.IsWindows()
            ? Egl.GetPlatformDisplay(Egl.EGL_PLATFORM_ANGLE_ANGLE, IntPtr.Zero,
                [Egl.EGL_PLATFORM_ANGLE_TYPE_ANGLE, Egl.EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
                 Egl.EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE, Egl.EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE, Egl.EGL_NONE])
            : Egl.GetPlatformDisplay(Egl.EGL_PLATFORM_SURFACELESS_MESA, IntPtr.Zero);
        if (display == IntPtr.Zero || !Egl.eglInitialize(display, out _, out _))
        {
            display = Egl.eglGetDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero || !Egl.eglInitialize(display, out _, out _))
            {
                Log.Info("render", $"No EGL display (0x{Egl.eglGetError():X})");
                return null;
            }
        }

        AddDisplayRef(display);

        string ext = Egl.QueryString(display, Egl.EGL_EXTENSIONS) ?? string.Empty;
        bool surfaceless = ext.Contains("EGL_KHR_surfaceless_context", StringComparison.Ordinal);
        if (!surfaceless && !OperatingSystem.IsWindows())
        {
            Log.Info("render", "EGL has no surfaceless context support");
            ReleaseDisplay(display);
            return null;
        }

        foreach (bool gles in new[] { true, false })
        {
            if (!Egl.eglBindAPI(gles ? Egl.EGL_OPENGL_ES_API : Egl.EGL_OPENGL_API))
            {
                continue;
            }

            int[] cfgAttribs =
            [
                Egl.EGL_RENDERABLE_TYPE, gles ? Egl.EGL_OPENGL_ES3_BIT : Egl.EGL_OPENGL_BIT,
                Egl.EGL_RED_SIZE, 8, Egl.EGL_GREEN_SIZE, 8, Egl.EGL_BLUE_SIZE, 8, Egl.EGL_ALPHA_SIZE, 8,
                Egl.EGL_SURFACE_TYPE, surfaceless ? 0 : Egl.EGL_PBUFFER_BIT,
                Egl.EGL_NONE,
            ];
            IntPtr[] configs = new IntPtr[1];
            IntPtr config = IntPtr.Zero;
            if (Egl.eglChooseConfig(display, cfgAttribs, configs, 1, out int n) && n > 0)
            {
                config = configs[0];
            }

            int[] ctxAttribs = gles
                ? [Egl.EGL_CONTEXT_MAJOR_VERSION, 3, Egl.EGL_CONTEXT_MINOR_VERSION, 0, Egl.EGL_NONE]
                : [Egl.EGL_CONTEXT_MAJOR_VERSION, 3, Egl.EGL_CONTEXT_MINOR_VERSION, 3, Egl.EGL_CONTEXT_OPENGL_PROFILE_MASK, Egl.EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT, Egl.EGL_NONE];
            IntPtr ctx = Egl.eglCreateContext(display, config, IntPtr.Zero, ctxAttribs);
            if (ctx == IntPtr.Zero)
            {
                continue;
            }

            IntPtr surface = surfaceless ? IntPtr.Zero : CreatePbuffer(display, config);
            if ((!surfaceless && surface == IntPtr.Zero) || !Egl.eglMakeCurrent(display, surface, surface, ctx))
            {
                Egl.eglDestroyContext(display, ctx);
                DestroySurface(display, surface);
                continue;
            }

            var result = new EglContext(display, ctx, surface, config, gles)
            {
                Renderer = $"{Egl.GlString(Egl.GL_RENDERER)} ({Egl.GlString(Egl.GL_VERSION)})",
            };
            return result;
        }

        ReleaseDisplay(display);
        return null;
    }

    private static IntPtr CreatePbuffer(IntPtr display, IntPtr config) =>
        Egl.eglCreatePbufferSurface(display, config, [Egl.EGL_WIDTH, 1, Egl.EGL_HEIGHT, 1, Egl.EGL_NONE]);

    private static void DestroySurface(IntPtr display, IntPtr surface)
    {
        if (surface != IntPtr.Zero)
        {
            Egl.eglDestroySurface(display, surface);
        }
    }

    private static readonly Dictionary<IntPtr, int> DisplayRefs = [];

    private static void AddDisplayRef(IntPtr display)
    {
        lock (DisplayRefs)
        {
            Egl.eglInitialize(display, out _, out _);
            DisplayRefs[display] = DisplayRefs.GetValueOrDefault(display) + 1;
        }
    }

    private static void ReleaseDisplay(IntPtr display)
    {
        lock (DisplayRefs)
        {
            int n = DisplayRefs.GetValueOrDefault(display) - 1;
            if (n > 0)
            {
                DisplayRefs[display] = n;
                return;
            }

            DisplayRefs.Remove(display);
            Egl.eglTerminate(display);
        }
    }

    public EglContext? CreateSibling()
    {
        Egl.eglBindAPI(IsGles ? Egl.EGL_OPENGL_ES_API : Egl.EGL_OPENGL_API);
        int[] ctxAttribs = IsGles
            ? [Egl.EGL_CONTEXT_MAJOR_VERSION, 3, Egl.EGL_CONTEXT_MINOR_VERSION, 0, Egl.EGL_NONE]
            : [Egl.EGL_CONTEXT_MAJOR_VERSION, 3, Egl.EGL_CONTEXT_MINOR_VERSION, 3, Egl.EGL_CONTEXT_OPENGL_PROFILE_MASK, Egl.EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT, Egl.EGL_NONE];
        IntPtr ctx = Egl.eglCreateContext(_display, _config, IntPtr.Zero, ctxAttribs);
        if (ctx == IntPtr.Zero)
        {
            return null;
        }

        IntPtr surface = _surface == IntPtr.Zero ? IntPtr.Zero : CreatePbuffer(_display, _config);
        if ((_surface != IntPtr.Zero && surface == IntPtr.Zero) || !Egl.eglMakeCurrent(_display, surface, surface, ctx))
        {
            Egl.eglDestroyContext(_display, ctx);
            DestroySurface(_display, surface);
            return null;
        }

        return new EglContext(_display, ctx, surface, _config, IsGles, ownsDisplay: false) { Renderer = Renderer };
    }

    public void MakeCurrent() => Egl.eglMakeCurrent(_display, _surface, _surface, _context);

    public IntPtr GetProcAddress(string name) => Egl.eglGetProcAddress(name);

    public void Dispose()
    {
        if (_display != IntPtr.Zero && !_ownsDisplay)
        {
            if (_context != IntPtr.Zero)
            {
                Egl.eglDestroyContext(_display, _context);
            }

            DestroySurface(_display, _surface);
            _display = IntPtr.Zero;
            _context = IntPtr.Zero;
            _surface = IntPtr.Zero;
            return;
        }

        if (_display != IntPtr.Zero)
        {
            Egl.eglMakeCurrent(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_context != IntPtr.Zero)
            {
                Egl.eglDestroyContext(_display, _context);
            }

            DestroySurface(_display, _surface);
            _surface = IntPtr.Zero;
            ReleaseDisplay(_display);
            Egl.eglReleaseThread();
            _display = IntPtr.Zero;
            _context = IntPtr.Zero;
        }
    }
}
