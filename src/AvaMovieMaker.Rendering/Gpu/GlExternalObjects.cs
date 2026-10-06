namespace AvaMovieMaker.Rendering.Gpu;

internal sealed unsafe class GlExternalObjects
{
    public const uint Texture2D = 0x0DE1, Rgba8 = 0x8058;
    public const uint LayoutNone = 0, LayoutTransferSrc = 0x9592;
    private const uint HandleTypeOpaqueFd = 0x9586, NumDeviceUuids = 0x9596, DeviceUuid = 0x9597;
    private const uint TextureBinding2D = 0x8069, FramebufferBinding = 0x8CA6, Framebuffer = 0x8D40, ColorAttachment0 = 0x8CE0;
    private const uint Rgba = 0x1908, UnsignedByte = 0x1401;

    private readonly delegate* unmanaged<int, uint*, void> _createMemoryObjects;
    private readonly delegate* unmanaged<int, uint*, void> _deleteMemoryObjects;
    private readonly delegate* unmanaged<uint, ulong, uint, int, void> _importMemoryFd;
    private readonly delegate* unmanaged<uint, int, uint, int, int, uint, ulong, void> _texStorageMem2D;
    private readonly delegate* unmanaged<int, uint*, void> _genSemaphores;
    private readonly delegate* unmanaged<int, uint*, void> _deleteSemaphores;
    private readonly delegate* unmanaged<uint, uint, int, void> _importSemaphoreFd;
    private readonly delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void> _signalSemaphore;
    private readonly delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void> _waitSemaphore;
    private readonly delegate* unmanaged<uint, uint, byte*, void> _getUnsignedBytei;
    private readonly delegate* unmanaged<uint, int*, void> _getIntegerv;
    private readonly delegate* unmanaged<int, uint*, void> _genTextures;
    private readonly delegate* unmanaged<int, uint*, void> _deleteTextures;
    private readonly delegate* unmanaged<uint, uint, void> _bindTexture;
    private readonly delegate* unmanaged<int, uint*, void> _genFramebuffers;
    private readonly delegate* unmanaged<int, uint*, void> _deleteFramebuffers;
    private readonly delegate* unmanaged<uint, uint, void> _bindFramebuffer;
    private readonly delegate* unmanaged<uint, uint, uint, uint, int, void> _framebufferTexture2D;
    private readonly delegate* unmanaged<int, int, int, int, uint, uint, void*, void> _readPixels;
    private readonly delegate* unmanaged<uint> _getError;
    private readonly delegate* unmanaged<void> _flush;
    private readonly delegate* unmanaged<void> _finish;

    private GlExternalObjects(Func<string, IntPtr> proc)
    {
        _createMemoryObjects = (delegate* unmanaged<int, uint*, void>)proc("glCreateMemoryObjectsEXT");
        _deleteMemoryObjects = (delegate* unmanaged<int, uint*, void>)proc("glDeleteMemoryObjectsEXT");
        _importMemoryFd = (delegate* unmanaged<uint, ulong, uint, int, void>)proc("glImportMemoryFdEXT");
        _texStorageMem2D = (delegate* unmanaged<uint, int, uint, int, int, uint, ulong, void>)proc("glTexStorageMem2DEXT");
        _genSemaphores = (delegate* unmanaged<int, uint*, void>)proc("glGenSemaphoresEXT");
        _deleteSemaphores = (delegate* unmanaged<int, uint*, void>)proc("glDeleteSemaphoresEXT");
        _importSemaphoreFd = (delegate* unmanaged<uint, uint, int, void>)proc("glImportSemaphoreFdEXT");
        _signalSemaphore = (delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void>)proc("glSignalSemaphoreEXT");
        _waitSemaphore = (delegate* unmanaged<uint, uint, uint*, uint, uint*, uint*, void>)proc("glWaitSemaphoreEXT");
        _getUnsignedBytei = (delegate* unmanaged<uint, uint, byte*, void>)proc("glGetUnsignedBytei_vEXT");
        _getIntegerv = (delegate* unmanaged<uint, int*, void>)proc("glGetIntegerv");
        _genTextures = (delegate* unmanaged<int, uint*, void>)proc("glGenTextures");
        _deleteTextures = (delegate* unmanaged<int, uint*, void>)proc("glDeleteTextures");
        _bindTexture = (delegate* unmanaged<uint, uint, void>)proc("glBindTexture");
        _genFramebuffers = (delegate* unmanaged<int, uint*, void>)proc("glGenFramebuffers");
        _deleteFramebuffers = (delegate* unmanaged<int, uint*, void>)proc("glDeleteFramebuffers");
        _bindFramebuffer = (delegate* unmanaged<uint, uint, void>)proc("glBindFramebuffer");
        _framebufferTexture2D = (delegate* unmanaged<uint, uint, uint, uint, int, void>)proc("glFramebufferTexture2D");
        _readPixels = (delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)proc("glReadPixels");
        _getError = (delegate* unmanaged<uint>)proc("glGetError");
        _flush = (delegate* unmanaged<void>)proc("glFlush");
        _finish = (delegate* unmanaged<void>)proc("glFinish");
    }

    public static GlExternalObjects? TryCreate(EglContext egl)
    {
        string ext = GlExtensions(egl.GetProcAddress);
        if (!ext.Contains(" GL_EXT_memory_object_fd ", StringComparison.Ordinal) || !ext.Contains(" GL_EXT_semaphore_fd ", StringComparison.Ordinal))
        {
            return null;
        }

        var gl = new GlExternalObjects(egl.GetProcAddress);
        return gl._createMemoryObjects == null || gl._importMemoryFd == null || gl._texStorageMem2D == null || gl._genSemaphores == null
            || gl._importSemaphoreFd == null || gl._signalSemaphore == null || gl._waitSemaphore == null || gl._getUnsignedBytei == null
            || gl._genFramebuffers == null || gl._readPixels == null || gl._finish == null
            ? null
            : gl;
    }

    private static string GlExtensions(Func<string, IntPtr> proc)
    {
        var getIntegerv = (delegate* unmanaged<uint, int*, void>)proc("glGetIntegerv");
        var getStringi = (delegate* unmanaged<uint, uint, IntPtr>)proc("glGetStringi");
        if (getIntegerv == null || getStringi == null)
        {
            return " ";
        }

        int n = 0;
        getIntegerv(0x821D, &n);
        var sb = new System.Text.StringBuilder(" ");
        for (uint i = 0; i < n; i++)
        {
            sb.Append(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(getStringi(0x1F03, i))).Append(' ');
        }

        return sb.ToString();
    }

    public byte[]? DeviceUuidOf()
    {
        int n = 0;
        _getIntegerv(NumDeviceUuids, &n);
        if (n <= 0)
        {
            return null;
        }

        byte[] uuid = new byte[16];
        fixed (byte* p = uuid)
        {
            _getUnsignedBytei(DeviceUuid, 0, p);
        }

        return uuid;
    }

    public (uint Memory, uint Texture)? ImportTexture(int fd, ulong size, int width, int height)
    {
        while (_getError() != 0)
        {
        }

        uint mem;
        _createMemoryObjects(1, &mem);
        _importMemoryFd(mem, size, HandleTypeOpaqueFd, fd);
        if (_getError() != 0)
        {
            _deleteMemoryObjects(1, &mem);
            return null;
        }

        int previous;
        _getIntegerv(TextureBinding2D, &previous);
        uint tex;
        _genTextures(1, &tex);
        _bindTexture(Texture2D, tex);
        _texStorageMem2D(Texture2D, 1, Rgba8, width, height, mem, 0);
        uint err = _getError();
        _bindTexture(Texture2D, (uint)previous);
        if (err != 0)
        {
            _deleteTextures(1, &tex);
            _deleteMemoryObjects(1, &mem);
            return null;
        }

        return (mem, tex);
    }

    public void DeleteTexture(uint memory, uint texture)
    {
        _deleteTextures(1, &texture);
        _deleteMemoryObjects(1, &memory);
    }

    public uint ImportSemaphore(int fd)
    {
        while (_getError() != 0)
        {
        }

        uint sem;
        _genSemaphores(1, &sem);
        _importSemaphoreFd(sem, HandleTypeOpaqueFd, fd);
        if (_getError() != 0)
        {
            _deleteSemaphores(1, &sem);
            return 0;
        }

        return sem;
    }

    public void DeleteSemaphore(uint semaphore) => _deleteSemaphores(1, &semaphore);

    public void Signal(uint semaphore, uint texture, uint layout)
    {
        _signalSemaphore(semaphore, 0, null, 1, &texture, &layout);
    }

    public void Wait(uint semaphore, uint texture, uint layout)
    {
        _waitSemaphore(semaphore, 0, null, 1, &texture, &layout);
    }

    public void Flush() => _flush();

    public bool ReadTexture(uint texture, int width, int height, Span<byte> rgba)
    {
        int previous;
        _getIntegerv(FramebufferBinding, &previous);
        uint fbo;
        _genFramebuffers(1, &fbo);
        _bindFramebuffer(Framebuffer, fbo);
        _framebufferTexture2D(Framebuffer, ColorAttachment0, Texture2D, texture, 0);
        fixed (byte* p = rgba)
        {
            _readPixels(0, 0, width, height, Rgba, UnsignedByte, p);
        }

        bool ok = _getError() == 0;
        _bindFramebuffer(Framebuffer, (uint)previous);
        _deleteFramebuffers(1, &fbo);
        _finish();
        return ok;
    }
}
