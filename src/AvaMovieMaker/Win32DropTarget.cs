using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaMovieMaker.Diagnostics;

namespace AvaMovieMaker;

[SupportedOSPlatform("windows")]
internal static partial class Win32DropTarget
{
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public static void Install() =>
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => Wrap(window), RoutingStrategies.Direct);

    private static void Wrap(Window window)
    {
        if (!DragDrop.GetAllowDrop(window) || window.TryGetPlatformHandle() is not { HandleDescriptor: "HWND" } handle)
        {
            return;
        }

        IntPtr hwnd = handle.Handle;
        IntPtr inner = GetPropW(hwnd, "OleDropTargetInterface");
        if (inner == IntPtr.Zero)
        {
            return;
        }

        Marshal.AddRef(inner);
        IntPtr unknown = ComWrappers.GetOrCreateComInterfaceForObject(new Forwarder(inner), CreateComInterfaceFlags.None);
        Guid iid = typeof(IDropTarget).GUID;
        int qi = Marshal.QueryInterface(unknown, in iid, out IntPtr target);
        Marshal.Release(unknown);
        if (qi != 0 || RevokeDragDrop(hwnd) != 0)
        {
            Marshal.Release(inner);
            if (qi == 0)
            {
                Marshal.Release(target);
            }

            Log.Warn("dragdrop", "Could not put the drop target in front of Avalonia's; files from Explorer may not drop");
            return;
        }

        int hr = RegisterDragDrop(hwnd, target);
        Marshal.Release(target);
        if (hr != 0)
        {
            RegisterDragDrop(hwnd, inner);
            Log.Warn("dragdrop", $"RegisterDragDrop failed (0x{hr:X8}); files from Explorer may not drop");
        }

        window.Closed += (_, _) => Marshal.Release(inner);
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetPropW(IntPtr hwnd, string name);

    [LibraryImport("ole32.dll")]
    private static partial int RegisterDragDrop(IntPtr hwnd, IntPtr target);

    [LibraryImport("ole32.dll")]
    private static partial int RevokeDragDrop(IntPtr hwnd);

    [GeneratedComInterface]
    [Guid("00000122-0000-0000-C000-000000000046")]
    internal partial interface IDropTarget
    {
        [PreserveSig]
        int DragEnter(IntPtr dataObject, int keyState, long point, IntPtr effect);

        [PreserveSig]
        int DragOver(int keyState, long point, IntPtr effect);

        [PreserveSig]
        int DragLeave();

        [PreserveSig]
        int Drop(IntPtr dataObject, int keyState, long point, IntPtr effect);
    }

    [GeneratedComClass]
    internal sealed unsafe partial class Forwarder(IntPtr inner) : IDropTarget
    {
        private IntPtr Method(int slot) => (*(IntPtr**)inner)[slot];

        public int DragEnter(IntPtr dataObject, int keyState, long point, IntPtr effect) =>
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, long, IntPtr, int>)Method(3))(inner, dataObject, keyState, point, effect);

        public int DragOver(int keyState, long point, IntPtr effect) =>
            ((delegate* unmanaged[Stdcall]<IntPtr, int, long, IntPtr, int>)Method(4))(inner, keyState, point, effect);

        public int DragLeave() => ((delegate* unmanaged[Stdcall]<IntPtr, int>)Method(5))(inner);

        public int Drop(IntPtr dataObject, int keyState, long point, IntPtr effect) =>
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, long, IntPtr, int>)Method(6))(inner, dataObject, keyState, point, effect);
    }
}
