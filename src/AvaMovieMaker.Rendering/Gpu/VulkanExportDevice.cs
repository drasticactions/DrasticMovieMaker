using System.Runtime.InteropServices;
using System.Text;

namespace AvaMovieMaker.Rendering.Gpu;

internal sealed unsafe class VulkanExportDevice : IDisposable
{
    private static readonly string[] DeviceExtensions = ["VK_KHR_external_memory_fd", "VK_KHR_external_semaphore_fd"];

    private IntPtr _instance;
    private IntPtr _device;
    private readonly IntPtr _physical;
    private readonly delegate* unmanaged<IntPtr, Vk.GetFdInfo*, int*, int> _getMemoryFd;
    private readonly delegate* unmanaged<IntPtr, Vk.GetFdInfo*, int*, int> _getSemaphoreFd;

    private VulkanExportDevice(IntPtr instance, IntPtr physical, IntPtr device, string name)
    {
        _instance = instance;
        _physical = physical;
        _device = device;
        Name = name;
        _getMemoryFd = (delegate* unmanaged<IntPtr, Vk.GetFdInfo*, int*, int>)Proc(device, "vkGetMemoryFdKHR");
        _getSemaphoreFd = (delegate* unmanaged<IntPtr, Vk.GetFdInfo*, int*, int>)Proc(device, "vkGetSemaphoreFdKHR");
    }

    public string Name { get; }

    public static VulkanExportDevice? TryCreate(ReadOnlySpan<byte> uuid, out string? why)
    {
        try
        {
            return Create(uuid, out why);
        }
        catch (DllNotFoundException)
        {
            why = "no Vulkan loader";
            return null;
        }
        catch (EntryPointNotFoundException e)
        {
            why = $"Vulkan loader incomplete ({e.Message})";
            return null;
        }
    }

    private static VulkanExportDevice? Create(ReadOnlySpan<byte> uuid, out string? why)
    {
        byte[] appName = "AvaMovieMaker\0"u8.ToArray();
        IntPtr instance;
        fixed (byte* pName = appName)
        {
            var app = new Vk.ApplicationInfo { SType = Vk.StApplicationInfo, PApplicationName = pName, PEngineName = pName, ApiVersion = Vk.ApiVersion11 };
            var ici = new Vk.InstanceCreateInfo { SType = Vk.StInstanceCreateInfo, PApplicationInfo = &app };
            int r = Vk.vkCreateInstance(&ici, null, &instance);
            if (r != Vk.Success)
            {
                why = $"vkCreateInstance failed ({r})";
                return null;
            }
        }

        uint count = 0;
        Vk.vkEnumeratePhysicalDevices(instance, &count, null);
        IntPtr* devices = stackalloc IntPtr[(int)Math.Min(count, 16u)];
        count = Math.Min(count, 16u);
        Vk.vkEnumeratePhysicalDevices(instance, &count, devices);
        for (int i = 0; i < count; i++)
        {
            IntPtr physical = devices[i];
            var id = new Vk.PhysicalDeviceIdProperties { SType = Vk.StPhysicalDeviceIdProperties };
            byte* props = stackalloc byte[4096];
            new Span<byte>(props, 4096).Clear();
            *(int*)props = Vk.StPhysicalDeviceProperties2;
            *(void**)(props + 8) = &id;
            Vk.vkGetPhysicalDeviceProperties2(physical, props);
            if (!new ReadOnlySpan<byte>(id.DeviceUuid, 16).SequenceEqual(uuid))
            {
                continue;
            }

            string name = Marshal.PtrToStringUTF8((IntPtr)(props + 16 + 20)) ?? "?";
            if (!HasExtensions(physical))
            {
                why = $"{name} has no external memory/semaphore fd export";
                Vk.vkDestroyInstance(instance, null);
                return null;
            }

            IntPtr device = CreateDevice(physical);
            if (device == IntPtr.Zero)
            {
                why = $"vkCreateDevice failed on {name}";
                Vk.vkDestroyInstance(instance, null);
                return null;
            }

            var result = new VulkanExportDevice(instance, physical, device, name);
            if (result._getMemoryFd == null || result._getSemaphoreFd == null)
            {
                result.Dispose();
                why = "vkGetMemoryFdKHR/vkGetSemaphoreFdKHR missing";
                return null;
            }

            why = null;
            return result;
        }

        Vk.vkDestroyInstance(instance, null);
        why = "no Vulkan device has the compositor's GPU UUID";
        return null;
    }

    private static bool HasExtensions(IntPtr physical)
    {
        uint n = 0;
        Vk.vkEnumerateDeviceExtensionProperties(physical, null, &n, null);
        var list = new Vk.ExtensionProperties[n];
        fixed (Vk.ExtensionProperties* p = list)
        {
            Vk.vkEnumerateDeviceExtensionProperties(physical, null, &n, p);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++)
            {
                names.Add(Marshal.PtrToStringUTF8((IntPtr)p[i].ExtensionName) ?? string.Empty);
            }

            return DeviceExtensions.All(names.Contains);
        }
    }

    private static IntPtr CreateDevice(IntPtr physical)
    {
        byte[][] names = DeviceExtensions.Select(e => Encoding.UTF8.GetBytes(e + "\0")).ToArray();
        GCHandle[] pins = names.Select(b => GCHandle.Alloc(b, GCHandleType.Pinned)).ToArray();
        try
        {
            byte** ext = stackalloc byte*[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                ext[i] = (byte*)pins[i].AddrOfPinnedObject();
            }

            float priority = 1f;
            var queue = new Vk.DeviceQueueCreateInfo { SType = Vk.StDeviceQueueCreateInfo, QueueFamilyIndex = 0, QueueCount = 1, PQueuePriorities = &priority };
            var dci = new Vk.DeviceCreateInfo
            {
                SType = Vk.StDeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queue,
                EnabledExtensionCount = (uint)names.Length,
                PpEnabledExtensionNames = ext,
            };
            IntPtr device;
            return Vk.vkCreateDevice(physical, &dci, null, &device) == Vk.Success ? device : IntPtr.Zero;
        }
        finally
        {
            foreach (GCHandle h in pins)
            {
                h.Free();
            }
        }
    }

    private static IntPtr Proc(IntPtr device, string name)
    {
        byte[] b = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = b)
        {
            return Vk.vkGetDeviceProcAddr(device, p);
        }
    }

    public (ulong Buffer, ulong Memory, ulong Size)? Allocate(ulong size)
    {
        var ext = new Vk.ExternalInfo { SType = Vk.StExternalMemoryBufferCreateInfo, HandleTypes = Vk.HandleTypeOpaqueFd };
        var bci = new Vk.BufferCreateInfo
        {
            SType = Vk.StBufferCreateInfo,
            PNext = &ext,
            Size = size,
            Usage = Vk.BufferUsageTransferSrc | Vk.BufferUsageTransferDst,
        };
        ulong buffer;
        if (Vk.vkCreateBuffer(_device, &bci, null, &buffer) != Vk.Success)
        {
            return null;
        }

        Vk.MemoryRequirements req;
        Vk.vkGetBufferMemoryRequirements(_device, buffer, &req);
        int type = MemoryType(req.MemoryTypeBits);
        if (type < 0)
        {
            Vk.vkDestroyBuffer(_device, buffer, null);
            return null;
        }

        var export = new Vk.ExternalInfo { SType = Vk.StExportMemoryAllocateInfo, HandleTypes = Vk.HandleTypeOpaqueFd };
        var mai = new Vk.MemoryAllocateInfo { SType = Vk.StMemoryAllocateInfo, PNext = &export, AllocationSize = req.Size, MemoryTypeIndex = (uint)type };
        ulong memory;
        if (Vk.vkAllocateMemory(_device, &mai, null, &memory) != Vk.Success)
        {
            Vk.vkDestroyBuffer(_device, buffer, null);
            return null;
        }

        Vk.vkBindBufferMemory(_device, buffer, memory, 0);
        return (buffer, memory, req.Size);
    }

    private int MemoryType(uint bits)
    {
        byte* props = stackalloc byte[1024];
        new Span<byte>(props, 1024).Clear();
        Vk.vkGetPhysicalDeviceMemoryProperties(_physical, props);
        uint count = *(uint*)props;
        int fallback = -1;
        for (int i = 0; i < count && i < 32; i++)
        {
            if ((bits & (1u << i)) == 0)
            {
                continue;
            }

            uint flags = *(uint*)(props + 4 + i * 8);
            if ((flags & Vk.MemoryPropertyDeviceLocal) != 0)
            {
                return i;
            }

            fallback = fallback < 0 ? i : fallback;
        }

        return fallback;
    }

    public void Free(ulong buffer, ulong memory)
    {
        Vk.vkDestroyBuffer(_device, buffer, null);
        Vk.vkFreeMemory(_device, memory, null);
    }

    public ulong CreateSemaphore()
    {
        var export = new Vk.ExternalInfo { SType = Vk.StExportSemaphoreCreateInfo, HandleTypes = Vk.HandleTypeOpaqueFd };
        var sci = new Vk.SemaphoreCreateInfo { SType = Vk.StSemaphoreCreateInfo, PNext = &export };
        ulong sem;
        return Vk.vkCreateSemaphore(_device, &sci, null, &sem) == Vk.Success ? sem : 0;
    }

    public void DestroySemaphore(ulong semaphore) => Vk.vkDestroySemaphore(_device, semaphore, null);

    public int ExportMemory(ulong memory)
    {
        var info = new Vk.GetFdInfo { SType = Vk.StMemoryGetFdInfo, Handle = memory, HandleType = Vk.HandleTypeOpaqueFd };
        int fd = -1;
        return _getMemoryFd(_device, &info, &fd) == Vk.Success ? fd : -1;
    }

    public int ExportSemaphore(ulong semaphore)
    {
        var info = new Vk.GetFdInfo { SType = Vk.StSemaphoreGetFdInfo, Handle = semaphore, HandleType = Vk.HandleTypeOpaqueFd };
        int fd = -1;
        return _getSemaphoreFd(_device, &info, &fd) == Vk.Success ? fd : -1;
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            Vk.vkDestroyDevice(_device, null);
            _device = IntPtr.Zero;
        }

        if (_instance != IntPtr.Zero)
        {
            Vk.vkDestroyInstance(_instance, null);
            _instance = IntPtr.Zero;
        }
    }
}
