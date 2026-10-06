using System.Runtime.InteropServices;
using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.FFmpeg;

internal static unsafe class FFmpegUtil
{
    public static Rational ToRational(AVRational r) => new(r.num, r.den);

    public static AVRational ToAV(Rational r) => new() { num = (int)r.Num, den = (int)r.Den };

    public static string CodecName(AVCodecID id) => ffmpeg.avcodec_get_name(id) ?? string.Empty;

    public static string PixelFormatName(AVPixelFormat f) => ffmpeg.av_get_pix_fmt_name(f) ?? string.Empty;

    public static int RotationFromMatrix(int* matrix)
    {
        if (matrix == null)
        {
            return 0;
        }

        int9 m = default;
        for (uint i = 0; i < 9; i++)
        {
            m[i] = matrix[i];
        }

        double ccw = ffmpeg.av_display_rotation_get(m);
        if (double.IsNaN(ccw))
        {
            return 0;
        }

        int cw = (int)Math.Round(-ccw / 90.0) * 90;
        cw %= 360;
        if (cw < 0)
        {
            cw += 360;
        }

        return cw;
    }

    public static int StreamRotation(AVStream* stream)
    {
        AVCodecParameters* par = stream->codecpar;
        AVPacketSideData* sd = ffmpeg.av_packet_side_data_get(par->coded_side_data, par->nb_coded_side_data, AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
        if (sd == null || SideDataSize(sd) < 36)
        {
            return 0;
        }

        return RotationFromMatrix((int*)sd->data);
    }

    public static nuint SideDataSize(AVPacketSideData* sd) => (nuint)sd->size;

    public static Rational FrameRate(AVStream* stream)
    {
        AVRational r = stream->avg_frame_rate;
        if (r.num <= 0 || r.den <= 0)
        {
            r = stream->r_frame_rate;
        }

        if (r.num <= 0 || r.den <= 0 || r.num / (double)r.den > 1000)
        {
            return Rational.Ntsc;
        }

        return new Rational(r.num, r.den);
    }
}
