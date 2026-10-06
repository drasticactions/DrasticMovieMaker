using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.Media.Probing;

public static class ExifReader
{
    public readonly record struct ExifData(int Orientation, DateTimeOffset? DateTaken);

    public static ExifData Read(string path)
    {
        try
        {
            using Stream fs = FileStore.Current.OpenRead(path);
            byte[] head = new byte[Math.Min(fs.Length, 256 * 1024)];
            int n = fs.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            return Parse(head.AsSpan(0, n));
        }
        catch (IOException)
        {
            return new ExifData(1, null);
        }
    }

    public static ExifData Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4 && (data[0] == 'I' && data[1] == 'I' || data[0] == 'M' && data[1] == 'M'))
        {
            return ParseTiff(data);
        }

        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return new ExifData(1, null);
        }

        int p = 2;
        while (p + 4 <= data.Length)
        {
            if (data[p] != 0xFF)
            {
                break;
            }

            byte marker = data[p + 1];
            if (marker is 0xD9 or 0xDA)
            {
                break;
            }

            int len = BinaryPrimitives.ReadUInt16BigEndian(data[(p + 2)..]);
            if (marker == 0xE1 && len > 8 && p + 2 + len <= data.Length)
            {
                ReadOnlySpan<byte> seg = data.Slice(p + 4, len - 2);
                if (seg.Length > 6 && seg[..6].SequenceEqual("Exif\0\0"u8))
                {
                    return ParseTiff(seg[6..]);
                }
            }

            p += 2 + len;
        }

        return new ExifData(1, null);
    }

    private static ExifData ParseTiff(ReadOnlySpan<byte> t)
    {
        if (t.Length < 8)
        {
            return new ExifData(1, null);
        }

        bool le = t[0] == 'I';
        int orientation = 1;
        DateTimeOffset? date = null;
        uint ifd0 = U32(t, 4, le);
        uint exifIfd = 0;
        ReadIfd(t, ifd0, le, ref orientation, ref date, ref exifIfd);
        if (exifIfd != 0)
        {
            uint dummy = 0;
            ReadIfd(t, exifIfd, le, ref orientation, ref date, ref dummy);
        }

        return new ExifData(orientation is >= 1 and <= 8 ? orientation : 1, date);
    }

    private static void ReadIfd(ReadOnlySpan<byte> t, uint offset, bool le, ref int orientation, ref DateTimeOffset? date, ref uint exifIfd)
    {
        if (offset == 0 || offset + 2 > t.Length)
        {
            return;
        }

        int count = U16(t, (int)offset, le);
        for (int i = 0; i < count; i++)
        {
            int e = (int)offset + 2 + i * 12;
            if (e + 12 > t.Length)
            {
                return;
            }

            int tag = U16(t, e, le);
            int type = U16(t, e + 2, le);
            uint n = U32(t, e + 4, le);
            switch (tag)
            {
                case 0x0112 when type == 3:
                    orientation = U16(t, e + 8, le);
                    break;
                case 0x8769:
                    exifIfd = U32(t, e + 8, le);
                    break;
                case 0x9003 or 0x0132 when type == 2 && n >= 19:
                    uint at = U32(t, e + 8, le);
                    if (at + 19 <= t.Length)
                    {
                        string s = Encoding.ASCII.GetString(t.Slice((int)at, 19));
                        if (DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime d) &&
                            (date is null || tag == 0x9003))
                        {
                            date = new DateTimeOffset(d);
                        }
                    }

                    break;
            }
        }
    }

    private static int U16(ReadOnlySpan<byte> t, int at, bool le) =>
        le ? BinaryPrimitives.ReadUInt16LittleEndian(t[at..]) : BinaryPrimitives.ReadUInt16BigEndian(t[at..]);

    private static uint U32(ReadOnlySpan<byte> t, int at, bool le) =>
        le ? BinaryPrimitives.ReadUInt32LittleEndian(t[at..]) : BinaryPrimitives.ReadUInt32BigEndian(t[at..]);

    public static (int Rotation, bool Flip) Transform(int orientation) => orientation switch
    {
        2 => (0, true),
        3 => (180, false),
        4 => (180, true),
        5 => (90, true),
        6 => (90, false),
        7 => (270, true),
        8 => (270, false),
        _ => (0, false),
    };
}
