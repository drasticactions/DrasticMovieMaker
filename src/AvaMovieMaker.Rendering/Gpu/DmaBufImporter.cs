using System.Runtime.InteropServices;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Media.Decoding;
using SkiaSharp;

namespace AvaMovieMaker.Rendering.Gpu;

internal sealed unsafe class DmaBufImporter
{
    private const uint EglLinuxDmaBufExt = 0x3270;
    private const int EglWidth = 0x3057, EglHeight = 0x3056, EglLinuxDrmFourccExt = 0x3271;
    private const int EglPlane0FdExt = 0x3272, EglPlane0OffsetExt = 0x3273, EglPlane0PitchExt = 0x3274;
    private const int EglPlane0ModifierLoExt = 0x3443, EglPlane0ModifierHiExt = 0x3444;
    private const uint GlTexture2D = 0x0DE1;
    private const uint GlTextureMinFilter = 0x2801, GlTextureMagFilter = 0x2800, GlTextureWrapS = 0x2802, GlTextureWrapT = 0x2803;
    private const int GlLinear = 0x2601, GlClampToEdge = 0x812F;
    private const uint GlR8 = 0x8229, GlRg8 = 0x822B, GlR16 = 0x822A, GlRg16 = 0x822C;

    private const uint DrmR8 = 0x20203852, DrmGr88 = 0x38385247, DrmR16 = 0x20363152, DrmGr1616 = 0x32335247;
    private const uint DrmNv12 = 0x3231564E, DrmP010 = 0x30313050;

    private readonly IntPtr _display;
    private readonly delegate* unmanaged<IntPtr, IntPtr, uint, IntPtr, int*, IntPtr> _createImage;
    private readonly delegate* unmanaged<IntPtr, IntPtr, int> _destroyImage;
    private readonly delegate* unmanaged<uint, IntPtr, void> _imageTargetTexture;
    private readonly delegate* unmanaged<int, uint*, void> _genTextures;
    private readonly delegate* unmanaged<int, uint*, void> _deleteTextures;
    private readonly delegate* unmanaged<uint, uint, void> _bindTexture;
    private readonly delegate* unmanaged<uint, uint, int, void> _texParameteri;
    private bool _failed;

    private DmaBufImporter(IntPtr display, Func<string, IntPtr> proc)
    {
        _display = display;
        _createImage = (delegate* unmanaged<IntPtr, IntPtr, uint, IntPtr, int*, IntPtr>)proc("eglCreateImageKHR");
        _destroyImage = (delegate* unmanaged<IntPtr, IntPtr, int>)proc("eglDestroyImageKHR");
        _imageTargetTexture = (delegate* unmanaged<uint, IntPtr, void>)proc("glEGLImageTargetTexture2DOES");
        _genTextures = (delegate* unmanaged<int, uint*, void>)proc("glGenTextures");
        _deleteTextures = (delegate* unmanaged<int, uint*, void>)proc("glDeleteTextures");
        _bindTexture = (delegate* unmanaged<uint, uint, void>)proc("glBindTexture");
        _texParameteri = (delegate* unmanaged<uint, uint, int, void>)proc("glTexParameteri");
    }

    public static DmaBufImporter? TryCreate(EglContext egl)
    {
        string ext = egl.Extensions;
        if (!ext.Contains("EGL_EXT_image_dma_buf_import", StringComparison.Ordinal))
        {
            return null;
        }

        var imp = new DmaBufImporter(egl.Display, egl.GetProcAddress);
        if (imp._createImage == null || imp._destroyImage == null || imp._imageTargetTexture == null || imp._genTextures == null
            || imp._deleteTextures == null || imp._bindTexture == null || imp._texParameteri == null)
        {
            return null;
        }

        return imp;
    }

    public SKImage[]? TryImport(GRContext context, DecodedFrame frame)
    {
        if (_failed || !frame.IsHardware || frame.Format is not (FramePixelFormat.Nv12 or FramePixelFormat.P010))
        {
            return null;
        }

        DmaBufFrame? buf = frame.TryExportDmaBuf();
        if (buf is null)
        {
            return null;
        }

        bool tenBit = frame.Format == FramePixelFormat.P010;
        List<(uint Fourcc, DmaBufPlane Plane, int W, int H)>? planes = Planes(buf, tenBit);
        if (planes is null)
        {
            buf.Dispose();
            Disable($"unexpected DMA-BUF layout ({buf.Layers.Count} layers)");
            return null;
        }

        var release = new Release(this, buf, planes.Count);
        var images = new SKImage[planes.Count];
        for (int i = 0; i < planes.Count; i++)
        {
            (uint fourcc, DmaBufPlane p, int w, int h) = planes[i];
            DmaBufObject obj = buf.Objects[p.ObjectIndex];
            IntPtr image = CreateImage(fourcc, obj, p, w, h);
            if (image == IntPtr.Zero)
            {
                for (int k = 0; k < i; k++)
                {
                    images[k].Dispose();
                }

                release.Abandon(planes.Count - i);
                Disable("eglCreateImageKHR failed");
                return null;
            }

            uint tex;
            _genTextures(1, &tex);
            _bindTexture(GlTexture2D, tex);
            _texParameteri(GlTexture2D, GlTextureMinFilter, GlLinear);
            _texParameteri(GlTexture2D, GlTextureMagFilter, GlLinear);
            _texParameteri(GlTexture2D, GlTextureWrapS, GlClampToEdge);
            _texParameteri(GlTexture2D, GlTextureWrapT, GlClampToEdge);
            _imageTargetTexture(GlTexture2D, image);
            _bindTexture(GlTexture2D, 0);
            context.ResetContext();

            bool chroma = i > 0;
            uint glFormat = (tenBit, chroma) switch { (false, false) => GlR8, (false, true) => GlRg8, (true, false) => GlR16, _ => GlRg16 };
            SKColorType ct = (tenBit, chroma) switch { (false, false) => SKColorType.Gray8, (false, true) => SKColorType.Rg88, (true, false) => SKColorType.Alpha16, _ => SKColorType.Rg1616 };
            var backend = new GRBackendTexture(w, h, false, new GRGlTextureInfo(GlTexture2D, tex, glFormat));
            uint texCopy = tex;
            IntPtr imageCopy = image;
            SKImage? sk = SKImage.FromTexture(context, backend, GRSurfaceOrigin.TopLeft, ct,
                ct == SKColorType.Alpha16 ? SKAlphaType.Premul : SKAlphaType.Opaque, null, _ => release.Plane(texCopy, imageCopy), null);
            if (sk is null)
            {
                release.Plane(tex, image);
                for (int k = 0; k < i; k++)
                {
                    images[k].Dispose();
                }

                release.Abandon(planes.Count - i - 1);
                Disable($"Skia rejected a {ct} texture");
                return null;
            }

            images[i] = sk;
        }

        return images;
    }

    private void Disable(string why)
    {
        _failed = true;
        Log.Warn("render", $"Zero-copy decode disabled: {why}; frames are copied instead");
    }

    private static List<(uint, DmaBufPlane, int, int)>? Planes(DmaBufFrame f, bool tenBit)
    {
        int cw = (f.Width + 1) / 2, ch = (f.Height + 1) / 2;
        uint lumaFmt = tenBit ? DrmR16 : DrmR8, chromaFmt = tenBit ? DrmGr1616 : DrmGr88;
        if (f.Layers.Count == 2 && f.Layers[0].Planes.Count == 1 && f.Layers[1].Planes.Count == 1)
        {
            return [(f.Layers[0].Format, f.Layers[0].Planes[0], f.Width, f.Height), (f.Layers[1].Format, f.Layers[1].Planes[0], cw, ch)];
        }

        if (f.Layers.Count == 1 && f.Layers[0].Planes.Count == 2 && f.Layers[0].Format is DrmNv12 or DrmP010)
        {
            return [(lumaFmt, f.Layers[0].Planes[0], f.Width, f.Height), (chromaFmt, f.Layers[0].Planes[1], cw, ch)];
        }

        return null;
    }

    private IntPtr CreateImage(uint fourcc, DmaBufObject obj, DmaBufPlane p, int w, int h)
    {
        int* a = stackalloc int[17];
        int n = 0;
        a[n++] = EglWidth; a[n++] = w;
        a[n++] = EglHeight; a[n++] = h;
        a[n++] = EglLinuxDrmFourccExt; a[n++] = (int)fourcc;
        a[n++] = EglPlane0FdExt; a[n++] = obj.Fd;
        a[n++] = EglPlane0OffsetExt; a[n++] = (int)p.Offset;
        a[n++] = EglPlane0PitchExt; a[n++] = (int)p.Pitch;
        if (obj.Modifier != DmaBufFrame.ModifierInvalid)
        {
            a[n++] = EglPlane0ModifierLoExt; a[n++] = unchecked((int)(uint)obj.Modifier);
            a[n++] = EglPlane0ModifierHiExt; a[n++] = unchecked((int)(uint)(obj.Modifier >> 32));
        }

        a[n] = Egl.EGL_NONE;
        return _createImage(_display, IntPtr.Zero, EglLinuxDmaBufExt, IntPtr.Zero, a);
    }

    private sealed class Release(DmaBufImporter owner, DmaBufFrame buf, int planes)
    {
        private int _left = planes;

        public void Plane(uint texture, IntPtr image)
        {
            owner._deleteTextures(1, &texture);
            owner._destroyImage(owner._display, image);
            Done(1);
        }

        public void Abandon(int count) => Done(count);

        private void Done(int count)
        {
            if (Interlocked.Add(ref _left, -count) <= 0)
            {
                buf.Dispose();
            }
        }
    }
}
