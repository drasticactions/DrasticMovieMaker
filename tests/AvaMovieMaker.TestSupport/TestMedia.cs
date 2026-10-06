using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AvaMovieMaker.TestSupport;

public static class TestMedia
{
    private static readonly Lock Gate = new();

    public static string Folder { get; } = Path.Combine(Path.GetTempPath(), "AvaMovieMaker-testmedia");

    public static bool FFmpegCliAvailable { get; } = Which("ffmpeg");

    public static string CounterCard(double seconds = 4, int width = 320, int height = 240, string rate = "30000/1001",
        string codec = "libx264", int gop = 250, double hue = 0, string ext = ".mp4", bool audio = false)
    {
        string bg = HueColor(hue);
        string lum = $"if(lt(Y,{FrameCounter.StripHeight}),if(bitand(floor(N/pow(2,floor(X*{FrameCounter.Bits}/W))),1),235,16),if(between(X,mod(N*4,W),mod(N*4,W)+8),235,lum(X,Y)))";
        string vf = $"color=c={bg}:s={width}x{height}:r={rate}:d={Inv(seconds)},format=yuv420p,geq=lum='{lum}':cb='cb(X,Y)':cr='cr(X,Y)'";
        string codecArgs = codec switch
        {
            "libx264" => $"-c:v libx264 -preset veryfast -crf 12 -g {gop} -bf 3 -pix_fmt yuv420p",
            "wmv2" => "-c:v wmv2 -b:v 8M",
            "ffv1" => "-c:v ffv1",
            _ => $"-c:v {codec}",
        };
        string audioArgs = audio ? $"-f lavfi -i \"aevalsrc={Channels("0.25*sin(2*PI*1000*t)", 2)}:s=48000:d={Inv(seconds)}\"" : string.Empty;
        string audioCodec = audio ? (ext == ".mp4" ? "-c:a aac -b:a 192k" : "-c:a pcm_s16le") : string.Empty;
        return Make($"card-{width}x{height}-{seconds}-{rate.Replace('/', '_')}-{codec}-{gop}-{hue}-{audio}{ext}",
            $"-f lavfi -i \"{vf}\" {audioArgs} {codecArgs} {audioCodec} -shortest");
    }

    public static string Tone(double seconds = 2, double frequency = 1000, int sampleRate = 48000, int channels = 2) =>
        Make($"tone-{frequency}-{seconds}-{sampleRate}-{channels}.wav",
            $"-f lavfi -i \"aevalsrc={Channels("0.25*sin(2*PI*" + Inv(frequency) + "*t)", channels)}:s={sampleRate}:d={Inv(seconds)}\" -c:a pcm_f32le");

    public static string Ramp(double seconds = 2) =>
        Make($"ramp-{seconds}.wav", $"-f lavfi -i \"aevalsrc='{Channels("n/" + Inv(seconds * 48000), 2)}':s=48000:d={Inv(seconds)}\" -c:a pcm_f32le");

    public static string ClickTrack(double seconds = 4) =>
        Make($"click-{seconds}.wav", $"-f lavfi -i \"aevalsrc='{Channels("if(lt(mod(t,0.5),0.005),0.8,0)", 2)}':s=48000:d={Inv(seconds)}\" -c:a pcm_s16le");

    public static string BeatTrack(double seconds = 8) =>
        Make($"beats-{seconds}.wav", $"-f lavfi -i \"aevalsrc='{Channels("if(lt(t,1),0,if(lt(mod(t-1,0.5),0.005),0.8,0)+0.05*sin(2*PI*220*t))", 2)}':s=48000:d={Inv(seconds + 1)}\" -c:a pcm_s16le");

    public static string StillThenPan(double still = 3, double pan = 3) =>
        Make($"pan-{still}-{pan}.mp4", $"-f lavfi -i \"testsrc2=s=1280x720:r=30:d={Inv(still + pan)},crop=320:240:x='if(lt(t,{Inv(still)}),100,100+(t-{Inv(still)})*60)':y=200\" -c:v libx264 -pix_fmt yuv420p");

    public static string Picture(int width = 640, int height = 480, string ext = ".png", double hue = 120) =>
        Make($"pic-{width}x{height}-{hue}{ext}",
            $"-f lavfi -i \"color=c={HueColor(hue)}:s={width}x{height}:d=1,drawbox=x=iw/4:y=ih/4:w=iw/2:h=ih/2:c=white:t=fill\" -frames:v 1 -update 1");

    public static string SceneCuts(double[] cutsSeconds, double total, int width = 320, int height = 240)
    {
        var sb = new StringBuilder();
        double prev = 0;
        int i = 0;
        var labels = new List<string>();
        foreach (double c in cutsSeconds.Append(total))
        {
            double d = c - prev;
            sb.Append(CultureInfo.InvariantCulture, $"-f lavfi -i \"testsrc2=s={width}x{height}:r=30:d={Inv(d)},hue=h={i * 70}:s={(i % 2 == 0 ? 1 : 3)},eq=brightness={(i % 3 - 1) * 0.3}\" ");
            labels.Add($"[{i}:v]");
            prev = c;
            i++;
        }

        string filter = $"{string.Concat(labels)}concat=n={labels.Count}:v=1:a=0[v]";
        return Make($"scenes-{string.Join('_', cutsSeconds.Select(Inv))}-{total}.mp4",
            $"{sb}-filter_complex \"{filter}\" -map \"[v]\" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p");
    }

    public static string DvRecordings(double partSeconds, params string[] startTimes)
    {
        var parts = startTimes.Select(t => Make($"dv-{partSeconds}-{t.Replace(':', '-')}.dv",
            $"-f lavfi -i \"smptebars=s=720x480:r=30000/1001:d={Inv(partSeconds)}\" -c:v dvvideo -pix_fmt yuv411p -metadata creation_time=2020-01-02T{t}Z")).ToList();
        string path = Path.Combine(Folder, $"dv-{partSeconds}-{string.Join('_', startTimes).Replace(':', '-')}.dv");
        lock (Gate)
        {
            if (!File.Exists(path))
            {
                string tmp = path + ".tmp";
                using (FileStream o = File.Create(tmp))
                {
                    foreach (string part in parts)
                    {
                        using FileStream i = File.OpenRead(part);
                        i.CopyTo(o);
                    }
                }

                File.Move(tmp, path, overwrite: true);
            }
        }

        return path;
    }

    public static string Transcode(string source, string ext, string codecArgs) =>
        Make(Path.GetFileNameWithoutExtension(source) + ext, $"-i \"{source}\" {codecArgs}");

    private static string Make(string name, string args)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(args)).AsSpan(0, 6));
        string path = Path.Combine(Folder, $"{Path.GetFileNameWithoutExtension(name)}-{hash}{Path.GetExtension(name)}");
        lock (Gate)
        {
            if (File.Exists(path))
            {
                return path;
            }

            Directory.CreateDirectory(Folder);
            string tmp = Path.Combine(Folder, $"tmp-{Guid.NewGuid():N}{Path.GetExtension(name)}");
            Run("ffmpeg", $"-hide_banner -loglevel error -y {args} \"{tmp}\"");
            File.Move(tmp, path, overwrite: true);
            return path;
        }
    }

    public static void Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using Process p = Process.Start(psi)!;
        string err = p.StandardError.ReadToEnd();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"{exe} {args} failed ({p.ExitCode}): {err}");
        }
    }

    private static string HueColor(double hue)
    {
        double h = (hue % 360 + 360) % 360 / 60.0;
        double x = 1 - Math.Abs(h % 2 - 1);
        (double r, double g, double b) = (int)h switch
        {
            0 => (1.0, x, 0.0),
            1 => (x, 1.0, 0.0),
            2 => (0.0, 1.0, x),
            3 => (0.0, x, 1.0),
            4 => (x, 0.0, 1.0),
            _ => (1.0, 0.0, x),
        };
        int R = (int)(64 + r * 128), G = (int)(64 + g * 128), B = (int)(64 + b * 128);
        return $"0x{R:X2}{G:X2}{B:X2}";
    }

    private static string Channels(string expr, int count) => string.Join('|', Enumerable.Repeat(expr, count));

    private static string Inv(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    private static bool Which(string exe)
    {
        string file = OperatingSystem.IsWindows() ? exe + ".exe" : exe;
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(d => File.Exists(Path.Combine(d, file)));
    }
}
