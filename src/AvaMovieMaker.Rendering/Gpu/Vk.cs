using System.Runtime.InteropServices;

namespace AvaMovieMaker.Rendering.Gpu;

internal static unsafe partial class Vk
{
    private const string Lib = "libvulkan.so.1";

    public const int Success = 0;
    public const uint ApiVersion11 = (1u << 22) | (1u << 12);
    public const uint HandleTypeOpaqueFd = 0x1;
    public const uint MemoryPropertyDeviceLocal = 0x1;
    public const uint BufferUsageTransferSrc = 0x1, BufferUsageTransferDst = 0x2;

    public const int StApplicationInfo = 0, StInstanceCreateInfo = 1, StDeviceQueueCreateInfo = 2, StDeviceCreateInfo = 3;
    public const int StMemoryAllocateInfo = 5, StSemaphoreCreateInfo = 9, StBufferCreateInfo = 12;
    public const int StPhysicalDeviceProperties2 = 1000059001, StPhysicalDeviceIdProperties = 1000071004;
    public const int StExternalMemoryBufferCreateInfo = 1000072000, StExportMemoryAllocateInfo = 1000072002;
    public const int StMemoryGetFdInfo = 1000074002, StExportSemaphoreCreateInfo = 1000077000, StSemaphoreGetFdInfo = 1000079001;

    [StructLayout(LayoutKind.Sequential)]
    public struct ApplicationInfo
    {
        public int SType;
        public void* PNext;
        public byte* PApplicationName;
        public uint ApplicationVersion;
        public byte* PEngineName;
        public uint EngineVersion;
        public uint ApiVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InstanceCreateInfo
    {
        public int SType;
        public void* PNext;
        public uint Flags;
        public ApplicationInfo* PApplicationInfo;
        public uint EnabledLayerCount;
        public byte** PpEnabledLayerNames;
        public uint EnabledExtensionCount;
        public byte** PpEnabledExtensionNames;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicalDeviceIdProperties
    {
        public int SType;
        public void* PNext;
        public fixed byte DeviceUuid[16];
        public fixed byte DriverUuid[16];
        public fixed byte DeviceLuid[8];
        public uint DeviceNodeMask;
        public uint DeviceLuidValid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceQueueCreateInfo
    {
        public int SType;
        public void* PNext;
        public uint Flags;
        public uint QueueFamilyIndex;
        public uint QueueCount;
        public float* PQueuePriorities;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceCreateInfo
    {
        public int SType;
        public void* PNext;
        public uint Flags;
        public uint QueueCreateInfoCount;
        public DeviceQueueCreateInfo* PQueueCreateInfos;
        public uint EnabledLayerCount;
        public byte** PpEnabledLayerNames;
        public uint EnabledExtensionCount;
        public byte** PpEnabledExtensionNames;
        public void* PEnabledFeatures;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExternalInfo
    {
        public int SType;
        public void* PNext;
        public uint HandleTypes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BufferCreateInfo
    {
        public int SType;
        public void* PNext;
        public uint Flags;
        public ulong Size;
        public uint Usage;
        public int SharingMode;
        public uint QueueFamilyIndexCount;
        public uint* PQueueFamilyIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryRequirements
    {
        public ulong Size;
        public ulong Alignment;
        public uint MemoryTypeBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryAllocateInfo
    {
        public int SType;
        public void* PNext;
        public ulong AllocationSize;
        public uint MemoryTypeIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SemaphoreCreateInfo
    {
        public int SType;
        public void* PNext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GetFdInfo
    {
        public int SType;
        public void* PNext;
        public ulong Handle;
        public uint HandleType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtensionProperties
    {
        public fixed byte ExtensionName[256];
        public uint SpecVersion;
    }

    [LibraryImport(Lib)]
    public static partial int vkCreateInstance(InstanceCreateInfo* createInfo, void* allocator, IntPtr* instance);

    [LibraryImport(Lib)]
    public static partial void vkDestroyInstance(IntPtr instance, void* allocator);

    [LibraryImport(Lib)]
    public static partial int vkEnumeratePhysicalDevices(IntPtr instance, uint* count, IntPtr* devices);

    [LibraryImport(Lib)]
    public static partial void vkGetPhysicalDeviceProperties2(IntPtr physicalDevice, void* properties);

    [LibraryImport(Lib)]
    public static partial void vkGetPhysicalDeviceMemoryProperties(IntPtr physicalDevice, void* properties);

    [LibraryImport(Lib)]
    public static partial int vkEnumerateDeviceExtensionProperties(IntPtr physicalDevice, byte* layerName, uint* count, ExtensionProperties* properties);

    [LibraryImport(Lib)]
    public static partial int vkCreateDevice(IntPtr physicalDevice, DeviceCreateInfo* createInfo, void* allocator, IntPtr* device);

    [LibraryImport(Lib)]
    public static partial void vkDestroyDevice(IntPtr device, void* allocator);

    [LibraryImport(Lib)]
    public static partial IntPtr vkGetDeviceProcAddr(IntPtr device, byte* name);

    [LibraryImport(Lib)]
    public static partial int vkCreateBuffer(IntPtr device, BufferCreateInfo* createInfo, void* allocator, ulong* buffer);

    [LibraryImport(Lib)]
    public static partial void vkDestroyBuffer(IntPtr device, ulong buffer, void* allocator);

    [LibraryImport(Lib)]
    public static partial void vkGetBufferMemoryRequirements(IntPtr device, ulong buffer, MemoryRequirements* requirements);

    [LibraryImport(Lib)]
    public static partial int vkAllocateMemory(IntPtr device, MemoryAllocateInfo* allocateInfo, void* allocator, ulong* memory);

    [LibraryImport(Lib)]
    public static partial void vkFreeMemory(IntPtr device, ulong memory, void* allocator);

    [LibraryImport(Lib)]
    public static partial int vkBindBufferMemory(IntPtr device, ulong buffer, ulong memory, ulong offset);

    [LibraryImport(Lib)]
    public static partial int vkCreateSemaphore(IntPtr device, SemaphoreCreateInfo* createInfo, void* allocator, ulong* semaphore);

    [LibraryImport(Lib)]
    public static partial void vkDestroySemaphore(IntPtr device, ulong semaphore, void* allocator);
}
