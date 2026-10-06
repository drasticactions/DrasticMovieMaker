using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Decoding;

public sealed unsafe class DmaBufFrame : IDisposable
{
    public const ulong ModifierInvalid = 0x00ffffffffffffffUL;

    private AVFrame* _mapped;

    internal DmaBufFrame(AVFrame* mapped, int width, int height)
    {
        _mapped = mapped;
        Width = width;
        Height = height;
        var d = (DrmFrameDescriptor*)mapped->data[0];
        var objects = new DmaBufObject[d->NbObjects];
        for (int i = 0; i < objects.Length; i++)
        {
            DrmObjectDescriptor o = d->Object(i);
            objects[i] = new DmaBufObject(o.Fd, (long)o.Size, o.FormatModifier);
        }

        var layers = new List<DmaBufLayer>();
        for (int i = 0; i < d->NbLayers; i++)
        {
            DrmLayerDescriptor* l = d->Layer(i);
            var planes = new DmaBufPlane[l->NbPlanes];
            for (int p = 0; p < planes.Length; p++)
            {
                DrmPlaneDescriptor pl = l->Plane(p);
                planes[p] = new DmaBufPlane(pl.ObjectIndex, (long)pl.Offset, (long)pl.Pitch);
            }

            layers.Add(new DmaBufLayer(l->Format, planes));
        }

        Objects = objects;
        Layers = layers;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<DmaBufObject> Objects { get; }

    public IReadOnlyList<DmaBufLayer> Layers { get; }

    public void Dispose()
    {
        if (_mapped != null)
        {
            AVFrame* f = _mapped;
            ffmpeg.av_frame_free(&f);
            _mapped = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrmObjectDescriptor
    {
        public int Fd;
        public nuint Size;
        public ulong FormatModifier;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrmPlaneDescriptor
    {
        public int ObjectIndex;
        public nint Offset;
        public nint Pitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrmLayerDescriptor
    {
        public uint Format;
        public int NbPlanes;
        private DrmPlaneDescriptor _p0, _p1, _p2, _p3;

        public readonly DrmPlaneDescriptor Plane(int i) => i switch { 0 => _p0, 1 => _p1, 2 => _p2, _ => _p3 };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrmFrameDescriptor
    {
        public int NbObjects;
        private DrmObjectDescriptor _o0, _o1, _o2, _o3;
        public int NbLayers;
        private DrmLayerDescriptor _l0, _l1, _l2, _l3;

        public readonly DrmObjectDescriptor Object(int i) => i switch { 0 => _o0, 1 => _o1, 2 => _o2, _ => _o3 };

        public DrmLayerDescriptor* Layer(int i)
        {
            fixed (DrmLayerDescriptor* l = &_l0)
            {
                return l + i;
            }
        }
    }
}

public readonly record struct DmaBufObject(int Fd, long Size, ulong Modifier);

public readonly record struct DmaBufPlane(int ObjectIndex, long Offset, long Pitch);

public sealed record DmaBufLayer(uint Format, IReadOnlyList<DmaBufPlane> Planes);
