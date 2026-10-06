using System.Runtime.InteropServices;

namespace AvaMovieMaker.Rendering.Gpu;

internal static partial class Egl
{
    private const string Lib = "libEGL.so.1";

    public const int EGL_SUCCESS = 0x3000;
    public const int EGL_NONE = 0x3038;
    public const int EGL_EXTENSIONS = 0x3055;
    public const int EGL_VENDOR = 0x3053;
    public const int EGL_RENDERABLE_TYPE = 0x3040;
    public const int EGL_SURFACE_TYPE = 0x3033;
    public const int EGL_RED_SIZE = 0x3024;
    public const int EGL_GREEN_SIZE = 0x3023;
    public const int EGL_BLUE_SIZE = 0x3022;
    public const int EGL_ALPHA_SIZE = 0x3021;
    public const int EGL_OPENGL_ES2_BIT = 0x0004;
    public const int EGL_OPENGL_ES3_BIT = 0x0040;
    public const int EGL_OPENGL_BIT = 0x0008;
    public const int EGL_PBUFFER_BIT = 0x0001;
    public const int EGL_OPENGL_ES_API = 0x30A0;
    public const int EGL_OPENGL_API = 0x30A2;
    public const int EGL_CONTEXT_MAJOR_VERSION = 0x3098;
    public const int EGL_CONTEXT_MINOR_VERSION = 0x30FB;
    public const int EGL_CONTEXT_OPENGL_PROFILE_MASK = 0x30FD;
    public const int EGL_CONTEXT_OPENGL_CORE_PROFILE_BIT = 0x00000001;
    public const int EGL_PLATFORM_SURFACELESS_MESA = 0x31DD;
    public const int EGL_PLATFORM_GBM_KHR = 0x31D7;
    public const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;
    public const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
    public const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 0x3208;
    public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 0x3209;
    public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 0x322A;
    public const int EGL_WIDTH = 0x3057;
    public const int EGL_HEIGHT = 0x3056;
    public const int GL_RENDERER = 0x1F01;
    public const int GL_VERSION = 0x1F02;

    public static IntPtr eglGetProcAddress(string name) => OperatingSystem.IsWindows() ? Angle.GetProcAddress(name) : Native.eglGetProcAddress(name);

    public static unsafe IntPtr eglQueryString(IntPtr display, int name) =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<IntPtr, int, IntPtr>)Angle.QueryString)(display, name) : Native.eglQueryString(display, name);

    public static unsafe IntPtr eglGetDisplay(IntPtr nativeDisplay) =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<IntPtr, IntPtr>)Angle.GetDisplay)(nativeDisplay) : Native.eglGetDisplay(nativeDisplay);

    public static unsafe bool eglInitialize(IntPtr display, out int major, out int minor)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Native.eglInitialize(display, out major, out minor);
        }

        fixed (int* a = &major, b = &minor)
        {
            return ((delegate* unmanaged<IntPtr, int*, int*, int>)Angle.Initialize)(display, a, b) != 0;
        }
    }

    public static unsafe bool eglTerminate(IntPtr display) =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<IntPtr, int>)Angle.Terminate)(display) != 0 : Native.eglTerminate(display);

    public static unsafe bool eglBindAPI(int api) =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<int, int>)Angle.BindApi)(api) != 0 : Native.eglBindAPI(api);

    public static unsafe bool eglChooseConfig(IntPtr display, int[] attribs, IntPtr[] configs, int configSize, out int numConfig)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Native.eglChooseConfig(display, attribs, configs, configSize, out numConfig);
        }

        fixed (int* a = attribs, n = &numConfig)
        fixed (IntPtr* c = configs)
        {
            return ((delegate* unmanaged<IntPtr, int*, IntPtr*, int, int*, int>)Angle.ChooseConfig)(display, a, c, configSize, n) != 0;
        }
    }

    public static unsafe IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int[] attribs)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Native.eglCreateContext(display, config, shareContext, attribs);
        }

        fixed (int* a = attribs)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, int*, IntPtr>)Angle.CreateContext)(display, config, shareContext, a);
        }
    }

    public static unsafe bool eglDestroyContext(IntPtr display, IntPtr context) =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<IntPtr, IntPtr, int>)Angle.DestroyContext)(display, context) != 0 : Native.eglDestroyContext(display, context);

    public static unsafe bool eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context) =>
        OperatingSystem.IsWindows()
            ? ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, int>)Angle.MakeCurrent)(display, draw, read, context) != 0
            : Native.eglMakeCurrent(display, draw, read, context);

    public static unsafe int eglGetError() =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<int>)Angle.GetError)() : Native.eglGetError();

    public static unsafe bool eglReleaseThread() =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<int>)Angle.ReleaseThread)() != 0 : Native.eglReleaseThread();

    public static unsafe IntPtr eglCreatePbufferSurface(IntPtr display, IntPtr config, int[] attribs)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Native.eglCreatePbufferSurface(display, config, attribs);
        }

        fixed (int* a = attribs)
        {
            return ((delegate* unmanaged<IntPtr, IntPtr, int*, IntPtr>)Angle.CreatePbufferSurface)(display, config, a);
        }
    }

    public static unsafe bool eglDestroySurface(IntPtr display, IntPtr surface) =>
        OperatingSystem.IsWindows() ? ((delegate* unmanaged<IntPtr, IntPtr, int>)Angle.DestroySurface)(display, surface) != 0 : Native.eglDestroySurface(display, surface);

    public static string? QueryString(IntPtr display, int name)
    {
        IntPtr p = eglQueryString(display, name);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(p);
    }

    public static unsafe IntPtr GetPlatformDisplay(int platform, IntPtr nativeDisplay, int[]? attribs = null)
    {
        IntPtr fn = eglGetProcAddress("eglGetPlatformDisplayEXT");
        if (fn == IntPtr.Zero)
        {
            fn = eglGetProcAddress("eglGetPlatformDisplay");
        }

        if (fn == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        attribs ??= [EGL_NONE];
        fixed (int* a = attribs)
        {
            return ((delegate* unmanaged<int, IntPtr, int*, IntPtr>)fn)(platform, nativeDisplay, a);
        }
    }

    public static unsafe string? GlString(int name)
    {
        IntPtr fn = eglGetProcAddress("glGetString");
        if (fn == IntPtr.Zero)
        {
            return null;
        }

        IntPtr s = ((delegate* unmanaged<int, IntPtr>)fn)(name);
        return s == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(s);
    }

    private static partial class Native
    {
        [LibraryImport(Lib)]
        public static partial IntPtr eglGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [LibraryImport(Lib)]
        public static partial IntPtr eglQueryString(IntPtr display, int name);

        [LibraryImport(Lib)]
        public static partial IntPtr eglGetDisplay(IntPtr nativeDisplay);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglInitialize(IntPtr display, out int major, out int minor);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglTerminate(IntPtr display);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglBindAPI(int api);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglChooseConfig(IntPtr display, int[] attribs, IntPtr[] configs, int configSize, out int numConfig);

        [LibraryImport(Lib)]
        public static partial IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int[] attribs);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglDestroyContext(IntPtr display, IntPtr context);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);

        [LibraryImport(Lib)]
        public static partial int eglGetError();

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglReleaseThread();

        [LibraryImport(Lib)]
        public static partial IntPtr eglCreatePbufferSurface(IntPtr display, IntPtr config, int[] attribs);

        [LibraryImport(Lib)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static partial bool eglDestroySurface(IntPtr display, IntPtr surface);
    }

    private static unsafe class Angle
    {
        private static readonly IntPtr GetProc = Load();

        public static readonly IntPtr QueryString = Fn("eglQueryString");
        public static readonly IntPtr GetDisplay = Fn("eglGetDisplay");
        public static readonly IntPtr Initialize = Fn("eglInitialize");
        public static readonly IntPtr Terminate = Fn("eglTerminate");
        public static readonly IntPtr BindApi = Fn("eglBindAPI");
        public static readonly IntPtr ChooseConfig = Fn("eglChooseConfig");
        public static readonly IntPtr CreateContext = Fn("eglCreateContext");
        public static readonly IntPtr DestroyContext = Fn("eglDestroyContext");
        public static readonly IntPtr MakeCurrent = Fn("eglMakeCurrent");
        public static readonly IntPtr GetError = Fn("eglGetError");
        public static readonly IntPtr ReleaseThread = Fn("eglReleaseThread");
        public static readonly IntPtr CreatePbufferSurface = Fn("eglCreatePbufferSurface");
        public static readonly IntPtr DestroySurface = Fn("eglDestroySurface");

        public static IntPtr GetProcAddress(string name)
        {
            IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(name);
            try
            {
                return ((delegate* unmanaged<IntPtr, IntPtr>)GetProc)(utf8);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }

        private static IntPtr Fn(string name)
        {
            IntPtr p = GetProcAddress(name);
            return p != IntPtr.Zero ? p : throw new EntryPointNotFoundException($"ANGLE has no {name}.");
        }

        private static IntPtr Load()
        {
            foreach (string candidate in new[] { Path.Combine(AppContext.BaseDirectory, "av_libglesv2.dll"), "av_libglesv2.dll" })
            {
                if (NativeLibrary.TryLoad(candidate, typeof(Egl).Assembly, null, out IntPtr lib)
                    && NativeLibrary.TryGetExport(lib, "EGL_GetProcAddress", out IntPtr getProc))
                {
                    return getProc;
                }
            }

            throw new DllNotFoundException("ANGLE (av_libglesv2.dll) was not found.");
        }
    }
}
