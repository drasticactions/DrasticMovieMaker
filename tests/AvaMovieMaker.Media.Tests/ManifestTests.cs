namespace AvaMovieMaker.Media.Tests;

public class ManifestTests
{
    private static readonly Dictionary<string, string[]> ImportNeeds = new(StringComparer.Ordinal)
    {
        [".asf"] = ["demuxer asf", "decoder wmv2", "decoder wmav2"],
        [".wm"] = ["demuxer asf"],
        [".wmv"] = ["demuxer asf", "decoder wmv1", "decoder wmv2", "decoder wmv3", "decoder vc1"],
        [".wma"] = ["demuxer asf", "decoder wmav1", "decoder wmav2", "decoder wmapro", "decoder wmalossless"],
        [".dvr-ms"] = ["demuxer asf", "decoder mpeg2video"],
        [".avi"] = ["demuxer avi", "demuxer dv", "decoder dvvideo", "decoder mpeg4", "decoder msmpeg4v3", "decoder mjpeg"],
        [".m1v"] = ["demuxer mpegvideo", "decoder mpeg1video"],
        [".mp2v"] = ["demuxer mpegvideo", "decoder mpeg2video"],
        [".mpv2"] = ["demuxer mpegvideo", "decoder mpeg2video"],
        [".mpe"] = ["demuxer mpegps", "decoder mpeg1video"],
        [".mpeg"] = ["demuxer mpegps", "decoder mpeg1video", "decoder mpeg2video", "decoder mp2"],
        [".mpg"] = ["demuxer mpegps", "decoder mpeg1video", "decoder mpeg2video", "decoder mp2"],
        [".mp2"] = ["demuxer mp3", "decoder mp2"],
        [".mpa"] = ["demuxer mp3", "decoder mp2"],
        [".mp3"] = ["demuxer mp3", "decoder mp3"],
        [".mp4"] = ["demuxer mov", "decoder h264", "decoder hevc", "decoder aac"],
        [".m4v"] = ["demuxer mov", "decoder h264"],
        [".m4a"] = ["demuxer mov", "decoder aac", "decoder alac"],
        [".mov"] = ["demuxer mov", "decoder h264", "decoder prores"],
        [".3gp"] = ["demuxer mov", "decoder h263", "decoder aac"],
        [".mkv"] = ["demuxer matroska", "decoder h264", "decoder hevc", "decoder vp9"],
        [".webm"] = ["demuxer matroska", "decoder vp8", "decoder vp9", "decoder libdav1d", "decoder opus", "decoder vorbis"],
        [".ogv"] = ["demuxer ogg", "decoder theora", "decoder vorbis"],
        [".ogg"] = ["demuxer ogg", "decoder vorbis"],
        [".oga"] = ["demuxer ogg", "decoder vorbis", "decoder flac"],
        [".opus"] = ["demuxer ogg", "decoder opus"],
        [".ts"] = ["demuxer mpegts", "decoder h264", "decoder mpeg2video", "decoder ac3"],
        [".mts"] = ["demuxer mpegts", "decoder h264", "decoder ac3"],
        [".m2ts"] = ["demuxer mpegts", "decoder h264", "decoder ac3"],
        [".flv"] = ["demuxer flv", "decoder flv", "decoder h264"],
        [".bmp"] = ["demuxer image_bmp_pipe", "decoder bmp"],
        [".dib"] = ["demuxer image_bmp_pipe", "decoder bmp"],
        [".gif"] = ["demuxer gif", "decoder gif"],
        [".jfif"] = ["demuxer image_jpeg_pipe", "decoder mjpeg"],
        [".jpe"] = ["demuxer image_jpeg_pipe", "decoder mjpeg"],
        [".jpeg"] = ["demuxer image_jpeg_pipe", "decoder mjpeg"],
        [".jpg"] = ["demuxer image_jpeg_pipe", "decoder mjpeg"],
        [".png"] = ["demuxer image_png_pipe", "decoder png"],
        [".tif"] = ["demuxer image_tiff_pipe", "decoder tiff"],
        [".tiff"] = ["demuxer image_tiff_pipe", "decoder tiff"],
        [".webp"] = ["demuxer image_webp_pipe", "decoder webp"],
        [".heic"] = ["demuxer mov", "decoder hevc"],
        [".heif"] = ["demuxer mov", "decoder hevc"],
        [".avif"] = ["demuxer mov", "decoder libdav1d"],
        [".aif"] = ["demuxer aiff", "decoder pcm_s16be"],
        [".aifc"] = ["demuxer aiff", "decoder pcm_s16be"],
        [".aiff"] = ["demuxer aiff", "decoder pcm_s16be"],
        [".au"] = ["demuxer au", "decoder pcm_mulaw", "decoder pcm_s16be"],
        [".snd"] = ["demuxer au", "decoder pcm_mulaw"],
        [".wav"] = ["demuxer wav", "decoder pcm_s16le", "decoder adpcm_ms", "decoder adpcm_ima_wav"],
        [".aac"] = ["demuxer aac", "decoder aac"],
        [".flac"] = ["demuxer flac", "decoder flac"],
    };

    private static readonly string[] NotFFmpeg = [".emf", ".wmf"];

    private static readonly string[] PublishNeeds =
    [
        "encoder libx264", "encoder aac", "muxer mp4",
        "encoder libvpx_vp9", "encoder libopus", "muxer webm",
        "encoder ffv1", "encoder pcm_s16le", "muxer matroska",
        "encoder flac", "muxer flac", "muxer wav", "encoder png",
    ];

    private static readonly Dictionary<string, string[]> HardwareNeeds = new(StringComparer.Ordinal)
    {
        ["linux-x64"] = ["hwaccel h264_vaapi", "hwaccel hevc_vaapi", "hwaccel vp9_vaapi", "encoder h264_vaapi"],
        ["osx-arm64"] = ["hwaccel h264_videotoolbox", "hwaccel hevc_videotoolbox", "encoder h264_videotoolbox"],
        ["win-x64"] = ["hwaccel h264_d3d11va", "hwaccel hevc_d3d11va", "encoder h264_mf"],
    };

    public static TheoryData<string> Targets => ["linux-x64", "osx-arm64", "win-x64"];

    [Fact]
    public void EveryImportExtensionIsMapped()
    {
        string[] unmapped = [.. MediaFormats.All.Where(e => !ImportNeeds.ContainsKey(e) && !NotFFmpeg.Contains(e))];
        Assert.Empty(unmapped);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public void BuildEnablesImportAndPublish(string target)
    {
        HashSet<string> manifest = Load(target);
        string[] missing =
        [
            .. ImportNeeds.SelectMany(e => e.Value.Select(need => (e.Key, need)))
                .Where(x => !manifest.Contains(x.need))
                .Select(x => $"{x.Key}: {x.need}"),
            .. PublishNeeds.Concat(HardwareNeeds[target]).Where(need => !manifest.Contains(need)),
        ];
        Assert.Empty(missing);
    }

    private static HashSet<string> Load(string target)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "AvaMovieMaker.slnx")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("AvaMovieMaker.slnx not found");
        }

        string file = Path.Combine(dir, "tools", "ffmpeg", "out", target, "manifest.txt");
        if (!File.Exists(file))
        {
            Assert.Skip($"FFmpeg for {target} is not built here (tools/ffmpeg/out/{target}).");
        }

        return [.. File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0)];
    }
}
