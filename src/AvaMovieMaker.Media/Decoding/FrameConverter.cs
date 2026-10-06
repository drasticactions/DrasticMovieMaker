using AvaMovieMaker.Media.FFmpeg;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Decoding;

internal static unsafe class FrameConverter
{
    public static bool TryMap(AVPixelFormat f, out FramePixelFormat format, out bool fullRange)
    {
        fullRange = false;
        switch (f)
        {
            case AVPixelFormat.AV_PIX_FMT_YUV420P:
                format = FramePixelFormat.Yuv420P;
                return true;
            case AVPixelFormat.AV_PIX_FMT_YUVJ420P:
                format = FramePixelFormat.Yuv420P;
                fullRange = true;
                return true;
            case AVPixelFormat.AV_PIX_FMT_NV12:
                format = FramePixelFormat.Nv12;
                return true;
            case AVPixelFormat.AV_PIX_FMT_YUV420P10LE:
                format = FramePixelFormat.Yuv420P10;
                return true;
            case AVPixelFormat.AV_PIX_FMT_P010LE:
                format = FramePixelFormat.P010;
                return true;
            case AVPixelFormat.AV_PIX_FMT_BGRA:
                format = FramePixelFormat.Bgra;
                fullRange = true;
                return true;
            case AVPixelFormat.AV_PIX_FMT_RGBA:
                format = FramePixelFormat.Rgba;
                fullRange = true;
                return true;
            default:
                format = FramePixelFormat.Yuv420P;
                return false;
        }
    }

    public static bool HasAlpha(AVPixelFormat f)
    {
        AVPixFmtDescriptor* d = ffmpeg.av_pix_fmt_desc_get(f);
        return d != null && (d->flags & ffmpeg.AV_PIX_FMT_FLAG_ALPHA) != 0;
    }

    public static bool IsRgbFamily(AVPixelFormat f)
    {
        AVPixFmtDescriptor* d = ffmpeg.av_pix_fmt_desc_get(f);
        return d != null && ((d->flags & ffmpeg.AV_PIX_FMT_FLAG_RGB) != 0 || (d->flags & ffmpeg.AV_PIX_FMT_FLAG_PAL) != 0 || d->nb_components <= 2);
    }

    public static AVFrame* Convert(AVFrame* src, AVPixelFormat dst)
    {
        AVFrame* outFrame = ffmpeg.av_frame_alloc();
        outFrame->format = (int)dst;
        outFrame->width = src->width;
        outFrame->height = src->height;
        FFmpegRuntime.Throw(ffmpeg.av_frame_get_buffer(outFrame, 32), "Allocate frame");
        SwsContext* sws = ffmpeg.sws_getContext(src->width, src->height, (AVPixelFormat)src->format,
            src->width, src->height, dst, (int)SwsFlags.SWS_BICUBIC | (int)SwsFlags.SWS_ACCURATE_RND | (int)SwsFlags.SWS_FULL_CHR_H_INT, null, null, null);
        if (sws == null)
        {
            ffmpeg.av_frame_free(&outFrame);
            throw new FFmpegException($"No conversion from {FFmpegUtil.PixelFormatName((AVPixelFormat)src->format)}.");
        }

        try
        {
            ffmpeg.sws_scale(sws, src->data, src->linesize, 0, src->height, outFrame->data, outFrame->linesize);
        }
        finally
        {
            ffmpeg.sws_freeContext(sws);
        }

        ffmpeg.av_frame_copy_props(outFrame, src);
        outFrame->color_range = AVColorRange.AVCOL_RANGE_MPEG;
        return outFrame;
    }

    public static byte[] ToBgra(AVFrame* src, bool fullRange, FrameColorSpace cs)
    {
        int w = src->width;
        int h = src->height;
        byte[] result = new byte[w * h * 4];
        SwsContext* sws = ffmpeg.sws_getContext(w, h, (AVPixelFormat)src->format, w, h, AVPixelFormat.AV_PIX_FMT_BGRA,
            (int)SwsFlags.SWS_BICUBIC | (int)SwsFlags.SWS_ACCURATE_RND | (int)SwsFlags.SWS_FULL_CHR_H_INT, null, null, null);
        if (sws == null)
        {
            throw new FFmpegException("No conversion to BGRA.");
        }

        try
        {
            int colorspace = cs switch
            {
                FrameColorSpace.Bt709 => ffmpeg.SWS_CS_ITU709,
                FrameColorSpace.Bt2020 => ffmpeg.SWS_CS_BT2020,
                _ => ffmpeg.SWS_CS_ITU601,
            };
            int4 coef = Coefficients(colorspace);
            ffmpeg.sws_setColorspaceDetails(sws, coef, fullRange ? 1 : 0, Coefficients(ffmpeg.SWS_CS_DEFAULT), 1, 0, 1 << 16, 1 << 16);
            fixed (byte* p = result)
            {
                byte_ptr4 dstData = default;
                int4 dstStride = default;
                dstData[0] = p;
                dstStride[0] = w * 4;
                ffmpeg.sws_scale(sws, src->data, src->linesize, 0, h, dstData, dstStride);
            }
        }
        finally
        {
            ffmpeg.sws_freeContext(sws);
        }

        return result;
    }

    private static int4 Coefficients(int colorspace)
    {
        int* c = ffmpeg.sws_getCoefficients(colorspace);
        int4 r = default;
        for (uint i = 0; i < 4; i++)
        {
            r[i] = c[i];
        }

        return r;
    }
}
