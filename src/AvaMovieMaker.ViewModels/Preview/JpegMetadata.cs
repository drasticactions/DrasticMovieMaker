using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace AvaMovieMaker.ViewModels.Preview;

public static class JpegMetadata
{
    private const ushort Ascii = 2;
    private const ushort Byte = 1;

    public static byte[] Insert(byte[] jpeg, string title, string author, string software, DateTime taken)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return jpeg;
        }

        var entries = new List<(ushort Tag, ushort Type, byte[] Data)>();
        if (title.Length > 0)
        {
            entries.Add((0x010E, Ascii, AsciiZ(title)));
        }

        entries.Add((0x0131, Ascii, AsciiZ(software)));
        entries.Add((0x0132, Ascii, AsciiZ(taken.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture))));
        if (title.Length > 0)
        {
            entries.Add((0x9C9B, Byte, Ucs2Z(title)));
        }

        if (author.Length > 0)
        {
            entries.Add((0x9C9D, Byte, Ucs2Z(author)));
        }

        byte[] tiff = Tiff(entries);
        int segmentLength = 2 + 6 + tiff.Length;
        if (segmentLength > ushort.MaxValue)
        {
            return jpeg;
        }

        int at = 2;
        if (jpeg.Length > 6 && jpeg[2] == 0xFF && jpeg[3] == 0xE0)
        {
            at = 4 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(4));
        }

        using var ms = new MemoryStream(jpeg.Length + segmentLength + 2);
        ms.Write(jpeg, 0, at);
        ms.WriteByte(0xFF);
        ms.WriteByte(0xE1);
        ms.WriteByte((byte)(segmentLength >> 8));
        ms.WriteByte((byte)segmentLength);
        ms.Write("Exif\0\0"u8);
        ms.Write(tiff);
        ms.Write(jpeg, at, jpeg.Length - at);
        return ms.ToArray();
    }

    public static IReadOnlyDictionary<ushort, string> Read(byte[] jpeg)
    {
        var result = new Dictionary<ushort, string>();
        int i = 2;
        while (i + 4 <= jpeg.Length && jpeg[i] == 0xFF)
        {
            byte marker = jpeg[i + 1];
            int len = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(i + 2));
            if (marker == 0xE1 && len > 8 && jpeg.AsSpan(i + 4, 6).SequenceEqual("Exif\0\0"u8))
            {
                ReadOnlySpan<byte> t = jpeg.AsSpan(i + 10, len - 8);
                int ifd = BinaryPrimitives.ReadInt32LittleEndian(t[4..]);
                int n = BinaryPrimitives.ReadUInt16LittleEndian(t[ifd..]);
                for (int e = 0; e < n; e++)
                {
                    ReadOnlySpan<byte> entry = t.Slice(ifd + 2 + e * 12, 12);
                    ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(entry);
                    ushort type = BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);
                    int count = BinaryPrimitives.ReadInt32LittleEndian(entry[4..]);
                    ReadOnlySpan<byte> data = count <= 4 ? entry.Slice(8, count) : t.Slice(BinaryPrimitives.ReadInt32LittleEndian(entry[8..]), count);
                    result[tag] = type == Ascii ? Encoding.UTF8.GetString(data).TrimEnd('\0') : Encoding.Unicode.GetString(data).TrimEnd('\0');
                }

                break;
            }

            if (marker == 0xDA)
            {
                break;
            }

            i += 2 + len;
        }

        return result;
    }

    private static byte[] AsciiZ(string s) => Encoding.UTF8.GetBytes(s + "\0");

    private static byte[] Ucs2Z(string s) => Encoding.Unicode.GetBytes(s + "\0");

    private static byte[] Tiff(List<(ushort Tag, ushort Type, byte[] Data)> entries)
    {
        entries.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        const int ifd = 8;
        int dataStart = ifd + 2 + entries.Count * 12 + 4;
        int size = dataStart + entries.Sum(e => e.Data.Length <= 4 ? 0 : e.Data.Length + (e.Data.Length & 1));
        byte[] t = new byte[size];
        t[0] = (byte)'I';
        t[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(t.AsSpan(2), 42);
        BinaryPrimitives.WriteInt32LittleEndian(t.AsSpan(4), ifd);
        BinaryPrimitives.WriteUInt16LittleEndian(t.AsSpan(ifd), (ushort)entries.Count);
        int data = dataStart;
        for (int i = 0; i < entries.Count; i++)
        {
            (ushort tag, ushort type, byte[] bytes) = entries[i];
            Span<byte> entry = t.AsSpan(ifd + 2 + i * 12, 12);
            BinaryPrimitives.WriteUInt16LittleEndian(entry, tag);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], type);
            BinaryPrimitives.WriteInt32LittleEndian(entry[4..], bytes.Length);
            if (bytes.Length <= 4)
            {
                bytes.CopyTo(entry[8..]);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(entry[8..], data);
                bytes.CopyTo(t.AsSpan(data));
                data += bytes.Length + (bytes.Length & 1);
            }
        }

        return t;
    }
}
