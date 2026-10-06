using System.Diagnostics;
using System.Globalization;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: gen-testmedia OUTDIR");
    Environment.ExitCode = 2;
    return;
}

string outDir = args[0];
string guest = Path.Combine(outDir, "guest");
string host = Path.Combine(outDir, "host");
Directory.CreateDirectory(guest);
Directory.CreateDirectory(host);

string[] names = ["A", "B", "C", "D", "E", "F", "G", "H"];
for (int i = 0; i < names.Length; i++)
{
    double hue = i * 45;
    Card($"card{names[i]}-4x3", 640, 480, 10, hue);
    if (i < 2)
    {
        Card($"card{names[i]}-16x9", 854, 480, 10, hue);
    }
}

foreach ((string n, int w, int h, string ext) in new[] { ("pic-4x3", 1024, 768, "jpg"), ("pic-16x9", 1280, 720, "jpg"), ("pic-portrait", 600, 900, "jpg"), ("pic-odd", 333, 517, "png"), ("pic-square", 512, 512, "png") })
{
    Run($"-f lavfi -i \"testsrc2=s={w}x{h}:d=1,drawtext=text='{n}':fontcolor=white:fontsize=48:x=20:y=20\" -frames:v 1 -update 1 \"{Path.Combine(guest, n + "." + ext)}\"");
}

Audio("tone-1k", 10, "0.25*sin(2*PI*1000*t)");
Audio("sweep", 10, "0.25*sin(2*PI*(100+1900*t/10)*t)");
Audio("click-500ms", 30, "if(lt(mod(t,0.5),0.005),0.8,0)");
Run($"-f lavfi -i \"anoisesrc=color=pink:amplitude=0.25:d=10:r=48000\" -ac 2 -c:a pcm_s16le \"{Path.Combine(guest, "pink.wav")}\"");

Audio("music-120bpm", 90, "if(lt(mod(t,0.5),0.02),0.7*sin(2*PI*880*t),0)+0.1*sin(2*PI*220*t)*(0.6+0.4*sin(2*PI*0.25*t))");

int[] cuts = [150, 420, 600, 960, 1200, 1500, 1620];
var parts = new List<string>();
int prev = 0, k = 0;
foreach (int c in cuts.Append(1798))
{
    parts.Add($"color=c=0x{(k * 37 % 200 + 40):X2}{(k * 91 % 200 + 40):X2}{(k * 53 % 200 + 40):X2}:s=640x480:r=30000/1001:d={Inv((c - prev) * 1001.0 / 30000)}");
    prev = c;
    k++;
}

string concat = string.Join(';', parts.Select((p, i) => $"{p},format=yuv420p[v{i}]")) + ";" +
    string.Concat(parts.Select((_, i) => $"[v{i}]")) + $"concat=n={parts.Count}:v=1:a=0[out]";
Run($"-filter_complex \"{concat}\" -map \"[out]\" -c:v wmv2 -b:v 8M \"{Path.Combine(guest, "scenes.wmv")}\"");
File.WriteAllText(Path.Combine(outDir, "scenes-cuts.txt"), string.Join('\n', cuts) + "\n");

Console.WriteLine($"test media in {outDir}");

void Card(string name, int w, int h, double seconds, double hue)
{
    string bg = HueColor(hue);
    string lum = $"if(lt(Y,16),if(bitand(floor(N/pow(2,floor(X*16/W))),1),235,16),if(between(X,mod(N*4,W),mod(N*4,W)+12)*lt(Y,{h / 2}),235,lum(X,Y)))";
    string graph =
        $"color=c={bg}:s={w}x{h}:r=30000/1001:d={Inv(seconds)}," +
        $"drawbox=x=0:y={h * 3 / 4}:w={w}:h={h / 8}:c=white@0:t=fill," +
        $"drawtext=text='{name}':fontcolor=white:fontsize={h / 10}:x=20:y=30," +
        $"drawtext=timecode='00\\:00\\:00\\:00':rate=30000/1001:fontcolor=white:fontsize={h / 14}:x=20:y={h / 5}," +
        $"format=yuv420p,geq=lum='{lum}':cb='if(gt(Y,{h * 7 / 8}),128,cb(X,Y))':cr='if(gt(Y,{h * 7 / 8}),128,cr(X,Y))'";
    string ramp = $"[0:v][1:v]overlay=0:{h * 7 / 8}";
    string rampSrc = $"color=c=black:s={w}x{h / 8}:r=30000/1001:d={Inv(seconds)},format=yuv420p,geq=lum='16+X*219/W':cb=128:cr=128";
    string audio = $"aevalsrc='0.2*sin(2*PI*{Inv(300 + hue)}*t)|0.2*sin(2*PI*{Inv(300 + hue)}*t)':s=48000:d={Inv(seconds)}";
    Run($"-f lavfi -i \"{graph}\" -f lavfi -i \"{rampSrc}\" -f lavfi -i \"{audio}\" -filter_complex \"{ramp}\" -c:v wmv2 -b:v 12M -c:a wmav2 -b:a 192k -shortest \"{Path.Combine(guest, name + ".wmv")}\"");
    string par = w * 3 == h * 4 ? "8/9" : "32/27";
    Directory.CreateDirectory(Path.Combine(guest, "dv"));
    Run($"-f lavfi -i \"{graph}\" -f lavfi -i \"{rampSrc}\" -f lavfi -i \"{audio}\" -filter_complex \"{ramp},scale=720:480,setsar={par}\" -c:v dvvideo -pix_fmt yuv411p -c:a pcm_s16le -ar 48000 -shortest \"{Path.Combine(guest, "dv", name + ".avi")}\"");
    string seq = Path.Combine(host, name);
    Directory.CreateDirectory(seq);
    Run($"-f lavfi -i \"{graph}\" -f lavfi -i \"{rampSrc}\" -filter_complex \"{ramp}\" \"{Path.Combine(seq, "%05d.png")}\"");
}

void Audio(string name, double seconds, string expr) =>
    Run($"-f lavfi -i \"aevalsrc='{expr}|{expr}':s=48000:d={Inv(seconds)}\" -c:a pcm_s16le \"{Path.Combine(guest, name + ".wav")}\"");

static void Run(string args)
{
    var psi = new ProcessStartInfo("ffmpeg", "-hide_banner -loglevel error -y " + args) { RedirectStandardError = true, UseShellExecute = false };
    using Process p = Process.Start(psi)!;
    string err = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0)
    {
        throw new InvalidOperationException($"ffmpeg {args}\n{err}");
    }
}

static string Inv(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

static string HueColor(double hue)
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
    return $"0x{(int)(64 + r * 128):X2}{(int)(64 + g * 128):X2}{(int)(64 + b * 128):X2}";
}
