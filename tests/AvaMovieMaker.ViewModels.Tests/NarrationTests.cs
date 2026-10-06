using AvaMovieMaker.Audio.Capture;
using AvaMovieMaker.Media;
using AvaMovieMaker.Media.Encoders;
using AvaMovieMaker.Settings;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Narration;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class NarrationTests
{
    private static MediaTime S(double s) => MediaTime.FromSeconds(s);

    private sealed class FakeRecording(string path, double seconds) : INarrationRecording
    {
        public string Path { get; } = path;

        public MediaTime Duration => S(seconds);

        public float Level => 0.5f;

        public float Gain { get; set; } = 1;

        public event EventHandler? LimitReached;

        public void ReachLimit() => LimitReached?.Invoke(this, EventArgs.Empty);

        public MediaTime Stop()
        {
            using var writer = new AudioFileWriter(Path, NarrationRecorder.Channels, NarrationRecorder.SampleRate);
            float[] block = new float[NarrationRecorder.SampleRate * NarrationRecorder.Channels];
            for (int i = 0; i < block.Length; i++)
            {
                block[i] = 0.1f * MathF.Sin(i * 0.05f);
            }

            for (int s = 0; s < (int)Math.Round(seconds); s++)
            {
                writer.Write(block);
            }

            writer.Finish();
            return Duration;
        }

        public void Dispose()
        {
        }
    }

    private sealed class Setup : IDisposable
    {
        private readonly TempFolder _temp = new();

        public Setup(double seconds)
        {
            Harness.RequireFFmpeg();
            H = new Harness(new AppSettings { AutoRecoveryEnabled = false, TemporaryFolder = _temp.File("tmp") });
            Folder = Directory.CreateDirectory(_temp.File("narrations")).FullName;
            H.Shell.Settings.NarrationFolder = Folder;
            H.Shell.OpenNarration(["Mic"], (device, path, limit) =>
            {
                Recording = new FakeRecording(path, seconds);
                Limit = limit;
                return Recording;
            });
        }

        public Harness H { get; }

        public string Folder { get; }

        public FakeRecording? Recording { get; private set; }

        public MediaTime? Limit { get; private set; }

        public NarrationViewModel N => H.Shell.Narration!;

        public Project P => H.Session.Project;

        public AudioClip Song(double start, double seconds)
        {
            var m = new MediaItem
            {
                Kind = MediaKind.Audio,
                Path = "/media/song.wav",
                Name = "song",
                Duration = S(seconds),
                Audio = new AudioProperties { SampleRate = 48000, Channels = 2 },
                Clips = [new SourceClip { Name = "song", End = S(seconds) }],
            };
            H.Session.Editor.ImportMedia([m]);
            H.Session.Editor.AddAudioClip(m, null, S(start));
            return P.AudioMusicTrack.Single(a => a.MediaId == m.Id);
        }

        public void Seek(double seconds)
        {
            H.Shell.Monitor.ShowProject();
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (H.Shell.Monitor.Playback.Duration < S(seconds) && wait.ElapsedMilliseconds < 5000)
            {
                Thread.Sleep(10);
                H.Shell.Monitor.ShowProject();
            }

            H.Shell.Monitor.Playback.Seek(S(seconds));
            Assert.Equal(S(seconds), H.Shell.Monitor.Playback.Position);
            N.Refresh();
        }

        public void Dispose()
        {
            H.Dispose();
            _temp.Dispose();
        }
    }

    [Fact]
    public void Starts_only_on_an_empty_point_of_the_audio_track()
    {
        using var s = new Setup(1);
        s.Song(5, 10);
        s.Seek(7);
        Assert.False(s.N.StartCommand.CanExecute(null));
        Assert.Equal(Strings.NarrationStartTip, s.N.StartTip);
        Assert.Equal("0:00:00", s.N.AvailableTime);

        s.Seek(2);
        Assert.True(s.N.StartCommand.CanExecute(null));
        Assert.Null(s.N.StartTip);
        Assert.Equal("0:00:03", s.N.AvailableTime);
        s.N.LimitToFreeSpace = false;
        Assert.Equal("--:--:--", s.N.AvailableTime);
        s.N.LimitToFreeSpace = true;

        Assert.True(s.N.LimitToFreeSpace);
        Assert.False(s.N.ShowOptions);
        Assert.False(s.N.MuteSpeakers);
        Assert.Equal(32768, s.N.InputLevel);
    }

    [Fact]
    public async Task Saved_narration_is_trimmed_to_the_free_space()
    {
        using var s = new Setup(8);
        s.Song(20, 10);
        s.Seek(15);
        Assert.Equal("0:00:05", s.N.AvailableTime);
        string? suggested = null, folder = null;
        s.H.Files.NarrationSave = (name, start) =>
        {
            (suggested, folder) = (name, start);
            return Path.Combine(s.Folder, "My narration.flac");
        };

        await s.N.StartCommand.ExecuteAsync(null);
        Assert.True(s.N.IsRecording);
        Assert.Equal(S(5), s.Limit);
        string temp = s.Recording!.Path;
        Assert.EndsWith(".flac", temp, StringComparison.Ordinal);
        await s.N.StopCommand.ExecuteAsync(null);

        Assert.Equal(("Untitled Narration.flac", s.Folder), (suggested, folder));
        Assert.False(File.Exists(temp));
        Assert.True(File.Exists(Path.Combine(s.Folder, "My narration.flac")));
        AudioClip n = s.P.AudioMusicTrack.Single(a => a.Start == S(15));
        Assert.InRange(n.Length.Seconds, 4.99, 5.01);
        Assert.Equal(S(20), s.P.AudioMusicTrack.Single(a => a != n).Start);
        Assert.Contains(s.P.Media, m => m.Path.EndsWith("My narration.flac", StringComparison.Ordinal));
        Assert.Equal("Add Clip", s.H.Session.Undo.UndoName);
        Assert.Equal(s.Folder, s.H.Shell.Settings.NarrationFolder);
    }

    [Fact]
    public async Task Without_the_limit_later_clips_move_along()
    {
        using var s = new Setup(8);
        s.Song(20, 10);
        s.N.LimitToFreeSpace = false;
        s.Seek(15);
        s.H.Files.NarrationSave = (_, _) => Path.Combine(s.Folder, "long.flac");
        await s.N.StartCommand.ExecuteAsync(null);
        Assert.Null(s.Limit);
        await s.N.StopCommand.ExecuteAsync(null);

        AudioClip n = s.P.AudioMusicTrack.Single(a => a.Start == S(15));
        Assert.InRange(n.Length.Seconds, 7.9, 8.1);
        Assert.InRange(s.P.AudioMusicTrack.Single(a => a != n).Start.Seconds, n.End.Seconds - 0.01, n.End.Seconds + 0.01);
        Assert.False(s.H.Shell.Settings.NarrationLimitToFreeSpace);
    }

    [Fact]
    public async Task Cancel_in_the_save_dialog_discards_the_recording()
    {
        using var s = new Setup(2);
        s.Seek(0);
        await s.N.StartCommand.ExecuteAsync(null);
        string temp = s.Recording!.Path;
        await s.N.StopCommand.ExecuteAsync(null);
        Assert.False(File.Exists(temp));
        Assert.Empty(s.P.AudioMusicTrack);
        Assert.Equal("0:00:02", s.N.Captured);
    }

    [Fact]
    public async Task Reaching_the_next_clip_stops_and_a_taken_name_gets_a_number()
    {
        using var s = new Setup(3);
        s.Song(10, 5);
        s.Seek(7);
        File.WriteAllBytes(Path.Combine(s.Folder, "Untitled Narration.flac"), [0]);
        string? suggested = null;
        s.H.Files.NarrationSave = (name, _) =>
        {
            suggested = name;
            return null;
        };

        await s.N.StartCommand.ExecuteAsync(null);
        s.Recording!.ReachLimit();
        Assert.False(s.N.IsRecording);
        Assert.Equal("Untitled Narration_0001.flac", suggested);
    }

    [Fact]
    public async Task Closing_the_pane_saves_a_narration_in_progress()
    {
        using var s = new Setup(2);
        s.Seek(0);
        s.H.Files.NarrationSave = (_, _) => Path.Combine(s.Folder, "closed.flac");
        await s.N.StartCommand.ExecuteAsync(null);
        await s.N.DoneCommand.ExecuteAsync(null);
        Assert.Single(s.P.AudioMusicTrack);
        Assert.NotEqual(UpperPane.Narration, s.H.Shell.Pane);
    }
}
