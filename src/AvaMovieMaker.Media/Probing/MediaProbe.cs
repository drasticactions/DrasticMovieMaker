using System.Globalization;
using System.Runtime.InteropServices;
using AvaMovieMaker.IO;
using AvaMovieMaker.Media.FFmpeg;
using AvaMovieMaker.Time;
using FFmpeg.AutoGen.Abstractions;

namespace AvaMovieMaker.Media.Probing;

public static unsafe class MediaProbe
{
    private static readonly HashSet<string> ImageCodecs = new(StringComparer.Ordinal)
    {
        "mjpeg", "png", "bmp", "tiff", "webp", "gif", "jpegls", "jpeg2000", "heic", "hevc_image", "av1_image", "qoi", "pgm", "ppm", "targa",
    };

    public static Task<MediaInfo> ProbeAsync(string path, CancellationToken cancel = default) =>
        Task.Run(() => Probe(path), cancel);

    public static MediaInfo Probe(string path)
    {
        if (FileStore.Current.GetInfo(path) is not { } file)
        {
            throw new FileNotFoundException("Media file not found.", path);
        }

        using var input = new InputFile(path);
        int vi = input.FindBest(AVMediaType.AVMEDIA_TYPE_VIDEO);
        int ai = input.FindBest(AVMediaType.AVMEDIA_TYPE_AUDIO);

        VideoStreamInfo? video = null;
        bool isPicture = false;
        if (vi >= 0)
        {
            AVStream* s = input.Stream(vi);
            AVCodecParameters* par = s->codecpar;
            string codec = FFmpegUtil.CodecName(par->codec_id);
            AVRational sar = par->sample_aspect_ratio;
            AVPixelFormat pf = (AVPixelFormat)par->format;
            AVPixFmtDescriptor* desc = ffmpeg.av_pix_fmt_desc_get(pf);
            bool alpha = desc != null && (desc->flags & ffmpeg.AV_PIX_FMT_FLAG_ALPHA) != 0;
            isPicture = MediaFormats.IsPictureExtension(path) ||
                        (ImageCodecs.Contains(codec) && (input.FormatName.Contains("image2", StringComparison.Ordinal) || input.FormatName.EndsWith("_pipe", StringComparison.Ordinal)));

            int rotation = FFmpegUtil.StreamRotation(s);
            bool flip = false;
            if (isPicture)
            {
                ExifReader.ExifData exif = ExifReader.Read(path);
                (rotation, flip) = ExifReader.Transform(exif.Orientation);
            }

            video = new VideoStreamInfo
            {
                StreamIndex = vi,
                Width = par->width,
                Height = par->height,
                SampleAspect = sar.num > 0 && sar.den > 0 ? new Rational(sar.num, sar.den) : new Rational(1, 1),
                FrameRate = isPicture ? Rational.Ntsc : FFmpegUtil.FrameRate(s),
                Rotation = rotation,
                FlipHorizontal = flip,
                Codec = codec,
                PixelFormat = FFmpegUtil.PixelFormatName(pf),
                HasAlpha = alpha,
            };
        }

        AudioStreamInfo? audio = null;
        if (ai >= 0 && !isPicture)
        {
            AVStream* s = input.Stream(ai);
            AVCodecParameters* par = s->codecpar;
            byte* buf = stackalloc byte[64];
            ffmpeg.av_channel_layout_describe(&par->ch_layout, buf, 64);
            audio = new AudioStreamInfo
            {
                StreamIndex = ai,
                SampleRate = par->sample_rate,
                Channels = par->ch_layout.nb_channels,
                Codec = FFmpegUtil.CodecName(par->codec_id),
                Layout = Marshal.PtrToStringUTF8((IntPtr)buf) ?? string.Empty,
            };
        }

        if (video is null && audio is null)
        {
            throw new FFmpegException($"{System.IO.Path.GetFileName(path)} has no video or audio stream.");
        }

        int main = vi >= 0 ? vi : ai;
        if (ffmpeg.avcodec_find_decoder(input.Stream(main)->codecpar->codec_id) == null)
        {
            throw new MissingCodecException(path, (video?.Codec ?? audio?.Codec)!);
        }

        MediaKind kind = isPicture ? MediaKind.Picture : video is not null ? MediaKind.Video : MediaKind.Audio;
        MediaTime duration = MediaTime.Zero;
        if (kind != MediaKind.Picture)
        {
            duration = StreamDuration(input, video?.StreamIndex ?? -1, audio?.StreamIndex ?? -1);
        }

        DateTimeOffset? taken = null;
        if (kind == MediaKind.Picture)
        {
            taken = ExifReader.Read(path).DateTaken;
        }
        else if (input.Tag("creation_time") is { } ct &&
                 DateTimeOffset.TryParse(ct, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dto))
        {
            taken = dto;
        }

        return new MediaInfo
        {
            Path = Path.GetFullPath(path),
            Kind = kind,
            Duration = duration,
            Video = video,
            Audio = audio,
            Container = input.FormatName,
            FileSize = file.Length,
            LastWriteTimeUtc = file.LastWriteTimeUtc,
            DateTaken = taken,
        };
    }

    private static MediaTime StreamDuration(InputFile input, int vi, int ai)
    {
        long best = 0;
        foreach (int i in new[] { vi, ai })
        {
            if (i < 0)
            {
                continue;
            }

            AVStream* s = input.Stream(i);
            if (s->duration > 0 && s->duration != ffmpeg.AV_NOPTS_VALUE)
            {
                best = Math.Max(best, MediaTime.FromTimeBase(s->duration, FFmpegUtil.ToRational(s->time_base)).Ticks);
            }
        }

        if (best == 0 && input.Context->duration > 0)
        {
            best = MediaTime.FromTimeBase(input.Context->duration, new Rational(1, ffmpeg.AV_TIME_BASE)).Ticks;
        }

        return new MediaTime(best);
    }
}
