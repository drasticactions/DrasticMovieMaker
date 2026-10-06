using System.Buffers.Binary;
using System.Text;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.Interop.Mswmm;

public sealed class CompoundFile
{
    private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FreeSector = 0xFFFFFFFF;
    private const uint NoStream = 0xFFFFFFFF;

    private readonly byte[] _data;
    private readonly int _sectorSize;
    private readonly int _miniSectorSize;
    private readonly uint _miniCutoff;
    private readonly uint[] _fat;
    private readonly uint[] _miniFat;
    private readonly byte[] _miniStream;

    public CompoundFile(byte[] data)
    {
        _data = data;
        if (data.Length < 512 || !data.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException(Strings.NotACompoundFile);
        }

        int sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(30));
        int miniShift = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(32));
        if (sectorShift is not (9 or 12) || miniShift != 6)
        {
            throw new InvalidDataException(Strings.UnsupportedSectorSize);
        }

        _sectorSize = 1 << sectorShift;
        _miniSectorSize = 1 << miniShift;
        uint fatSectors = U32(44);
        uint dirStart = U32(48);
        _miniCutoff = U32(56);
        uint miniFatStart = U32(60);
        uint miniFatCount = U32(64);
        uint difatStart = U32(68);
        uint difatCount = U32(72);

        var fatSectorList = new List<uint>();
        for (int i = 0; i < 109 && fatSectorList.Count < fatSectors; i++)
        {
            fatSectorList.Add(U32(76 + i * 4));
        }

        uint d = difatStart;
        for (uint k = 0; k < difatCount && d != EndOfChain && d != FreeSector; k++)
        {
            int off = SectorOffset(d);
            int per = _sectorSize / 4 - 1;
            for (int i = 0; i < per && fatSectorList.Count < fatSectors; i++)
            {
                fatSectorList.Add(BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(off + i * 4)));
            }

            d = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(off + per * 4));
        }

        var fat = new List<uint>();
        foreach (uint s in fatSectorList)
        {
            int off = SectorOffset(s);
            for (int i = 0; i < _sectorSize / 4; i++)
            {
                fat.Add(BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(off + i * 4)));
            }
        }

        _fat = [.. fat];
        byte[] dir = ReadChain(dirStart, -1);
        Entries = ParseDirectory(dir);
        byte[] miniFatBytes = miniFatCount > 0 ? ReadChain(miniFatStart, -1) : [];
        _miniFat = new uint[miniFatBytes.Length / 4];
        for (int i = 0; i < _miniFat.Length; i++)
        {
            _miniFat[i] = BinaryPrimitives.ReadUInt32LittleEndian(miniFatBytes.AsSpan(i * 4));
        }

        Entry root = Entries[0];
        _miniStream = root.Size > 0 ? ReadChain(root.StartSector, (long)root.Size) : [];
        Root = root;
    }

    public static CompoundFile Open(string path) => new(FileStore.ReadAllBytes(path));

    public static bool IsCompoundFile(ReadOnlySpan<byte> head) => head.Length >= 8 && head[..8].SequenceEqual(Signature);

    public sealed record Entry(int Index, string Name, int Type, uint Left, uint Right, uint Child, uint StartSector, ulong Size)
    {
        public bool IsStream => Type == 2;

        public bool IsStorage => Type is 1 or 5;
    }

    public IReadOnlyList<Entry> Entries { get; }

    public Entry Root { get; }

    public IReadOnlyList<Entry> Children(Entry storage)
    {
        var result = new List<Entry>();
        var stack = new Stack<uint>();
        if (storage.Child != NoStream)
        {
            stack.Push(storage.Child);
        }

        while (stack.Count > 0)
        {
            uint i = stack.Pop();
            if (i >= Entries.Count)
            {
                continue;
            }

            Entry e = Entries[(int)i];
            result.Add(e);
            if (e.Left != NoStream)
            {
                stack.Push(e.Left);
            }

            if (e.Right != NoStream)
            {
                stack.Push(e.Right);
            }
        }

        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    public Entry? Find(string path)
    {
        Entry current = Root;
        foreach (string part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            Entry? next = Children(current).FirstOrDefault(e => string.Equals(e.Name, part, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    public IEnumerable<(string Path, Entry Entry)> Walk(Entry? from = null, string prefix = "")
    {
        foreach (Entry e in Children(from ?? Root))
        {
            string p = prefix.Length == 0 ? e.Name : prefix + "/" + e.Name;
            yield return (p, e);
            if (e.IsStorage)
            {
                foreach (var x in Walk(e, p))
                {
                    yield return x;
                }
            }
        }
    }

    public byte[] Read(Entry e)
    {
        if (!e.IsStream)
        {
            throw new InvalidOperationException($"{e.Name} is not a stream.");
        }

        return e.Size < _miniCutoff ? ReadMini(e.StartSector, (long)e.Size) : ReadChain(e.StartSector, (long)e.Size);
    }

    private byte[] ReadChain(uint start, long size)
    {
        var ms = new MemoryStream();
        uint s = start;
        int guard = 0;
        while (s != EndOfChain && s != FreeSector && s < _fat.Length && guard++ < _fat.Length + 1)
        {
            int off = SectorOffset(s);
            int n = (int)Math.Min(_sectorSize, Math.Max(0, _data.Length - off));
            ms.Write(_data, off, n);
            s = _fat[s];
        }

        byte[] bytes = ms.ToArray();
        return size >= 0 && bytes.Length > size ? bytes[..(int)size] : bytes;
    }

    private byte[] ReadMini(uint start, long size)
    {
        var ms = new MemoryStream();
        uint s = start;
        int guard = 0;
        while (s != EndOfChain && s != FreeSector && s < _miniFat.Length && guard++ < _miniFat.Length + 1)
        {
            int off = (int)s * _miniSectorSize;
            ms.Write(_miniStream, off, Math.Min(_miniSectorSize, _miniStream.Length - off));
            s = _miniFat[s];
        }

        byte[] bytes = ms.ToArray();
        return bytes.Length > size ? bytes[..(int)size] : bytes;
    }

    private List<Entry> ParseDirectory(byte[] dir)
    {
        var list = new List<Entry>();
        for (int i = 0; i + 128 <= dir.Length; i += 128)
        {
            int nameLen = BinaryPrimitives.ReadUInt16LittleEndian(dir.AsSpan(i + 64));
            string name = nameLen >= 2 ? Encoding.Unicode.GetString(dir, i, Math.Min(64, nameLen) - 2) : string.Empty;
            int type = dir[i + 66];
            uint left = BinaryPrimitives.ReadUInt32LittleEndian(dir.AsSpan(i + 68));
            uint right = BinaryPrimitives.ReadUInt32LittleEndian(dir.AsSpan(i + 72));
            uint child = BinaryPrimitives.ReadUInt32LittleEndian(dir.AsSpan(i + 76));
            uint start = BinaryPrimitives.ReadUInt32LittleEndian(dir.AsSpan(i + 116));
            ulong size = BinaryPrimitives.ReadUInt64LittleEndian(dir.AsSpan(i + 120));
            if (_sectorSize == 512)
            {
                size &= 0xFFFFFFFF;
            }

            list.Add(new Entry(i / 128, name, type, left, right, child, start, size));
        }

        if (list.Count == 0 || list[0].Type != 5)
        {
            throw new InvalidDataException(Strings.NoRootEntry);
        }

        return list;
    }

    private int SectorOffset(uint sector) => (int)((sector + 1) * (long)_sectorSize);

    private uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset));
}
