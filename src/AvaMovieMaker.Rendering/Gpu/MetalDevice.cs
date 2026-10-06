using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AvaMovieMaker.Diagnostics;

namespace AvaMovieMaker.Rendering.Gpu;

internal sealed partial class MetalDevice : IDisposable
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string Metal = "/System/Library/Frameworks/Metal.framework/Metal";

    private MetalDevice(IntPtr device, IntPtr queue, string name)
    {
        Device = device;
        Queue = queue;
        Name = name;
    }

    public IntPtr Device { get; private set; }

    public IntPtr Queue { get; private set; }

    public string Name { get; }

    [SupportedOSPlatform("macos")]
    public static MetalDevice? TryCreate()
    {
        try
        {
            NativeLibrary.TryLoad("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics", out _);
            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr device = MTLCreateSystemDefaultDevice();
                if (device == IntPtr.Zero)
                {
                    Log.Warn("render", "No Metal device; using CPU raster");
                    return null;
                }

                IntPtr queue = objc_msgSend(device, sel_registerName("newCommandQueue"));
                if (queue == IntPtr.Zero)
                {
                    Log.Warn("render", "Metal command queue creation failed; using CPU raster");
                    Release(device);
                    return null;
                }

                IntPtr nsName = objc_msgSend(device, sel_registerName("name"));
                string name = nsName == IntPtr.Zero
                    ? Strings.MetalDeviceUnnamed
                    : Marshal.PtrToStringUTF8(objc_msgSend(nsName, sel_registerName("UTF8String"))) ?? Strings.MetalDeviceUnnamed;
                return new MetalDevice(device, queue, name);
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Warn("render", $"Metal is not available ({e.Message}); using CPU raster");
            return null;
        }
    }

    [SupportedOSPlatform("macos")]
    public static void InPool(Action action)
    {
        IntPtr pool = objc_autoreleasePoolPush();
        try
        {
            action();
        }
        finally
        {
            objc_autoreleasePoolPop(pool);
        }
    }

    public void Dispose()
    {
        Release(Queue);
        Release(Device);
        Queue = IntPtr.Zero;
        Device = IntPtr.Zero;
    }

    private static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero)
        {
            objc_msgSend(obj, sel_registerName("release"));
        }
    }

    [LibraryImport(Metal)]
    private static partial IntPtr MTLCreateSystemDefaultDevice();

    [LibraryImport(ObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjC)]
    private static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjC)]
    private static partial IntPtr objc_autoreleasePoolPush();

    [LibraryImport(ObjC)]
    private static partial void objc_autoreleasePoolPop(IntPtr pool);
}
