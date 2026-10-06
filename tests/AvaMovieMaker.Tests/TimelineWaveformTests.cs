using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using AvaMovieMaker.Controls;
using AvaMovieMaker.Views;
using AvaMovieMaker.Media;
using AvaMovieMaker.TestSupport;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Shell;
using AvaMovieMaker.ViewModels.Timeline;

namespace AvaMovieMaker.Tests;

public sealed class TimelineWaveformTests
{
    private static ShellViewModel Shell => TestApp.Current.Shell!;

    private static double WaveShare(MainWindow w, TimelineControl tl, double clipWidth, double rowCenter)
    {
        using WriteableBitmap frame = w.CaptureRenderedFrame()!;
        using ILockedFramebuffer fb = frame.Lock();
        Point origin = tl.TranslatePoint(new Point(TimelineControl.HeaderWidth, rowCenter), w)!.Value;
        int count = 0, total = 0;
        unsafe
        {
            for (int y = (int)origin.Y - 10; y <= (int)origin.Y + 10; y++)
            {
                uint* row = (uint*)(fb.Address + y * fb.RowBytes);
                for (int x = (int)origin.X + 2; x < origin.X + clipWidth - 2; x++)
                {
                    uint v = row[x];
                    total++;

                    (uint r, uint b) = fb.Format == PixelFormat.Rgba8888 ? (v & 0xFF, (v >> 16) & 0xFF) : ((v >> 16) & 0xFF, v & 0xFF);
                    if (Near(r, 0xB5) && Near((v >> 8) & 0xFF, 0xC0) && Near(b, 0xD4))
                    {
                        count++;
                    }
                }
            }
        }

        return (double)count / Math.Max(1, total);
    }

    private static bool Near(uint value, uint target) => Math.Abs((int)value - (int)target) <= 8;

    [AvaloniaFact]
    public void A_selected_audio_clip_shows_its_waveform_in_both_layouts()
    {
        MainWindow w = TestApp.OpenMainWindow();
        try
        {
            if (!Shell.IsTimeline)
            {
                Shell.ToggleStoryboardTimelineCommand.Execute(null);
            }

            string path = TestMedia.Tone(10);
            var tone = new MediaItem { Kind = MediaKind.Audio, Path = path, Name = "tone", Duration = MediaTime.FromSeconds(10), Clips = [new SourceClip { Name = "tone", End = MediaTime.FromSeconds(10) }] };
            Shell.Session.Editor.ImportMedia([tone]);
            Assert.True(Shell.Timeline.DropMedia(TimelineTrack.AudioMusic, MediaTime.Zero, [(tone, null)]));
            Guid clip = Shell.Project.AudioMusicTrack[0].Id;
            Shell.Timeline.Click(TimelineTrack.AudioMusic, clip, ctrl: false, shift: false);
            Assert.True(Shell.Timeline.IsSelected(clip, TimelineTrack.AudioMusic), $"{Shell.Session.SelectionTrack} [{string.Join(",", Shell.Session.SelectedClips)}] clip={clip} n={Shell.Project.AudioMusicTrack.Count}");

            for (int i = 0; i < 200 && Shell.Timeline.WaveformFor(tone.Id) is null; i++)
            {
                Thread.Sleep(20);
                TestApp.Pump();
            }

            Assert.NotNull(Shell.Timeline.WaveformFor(tone.Id));
            TestApp.Pump();
            TimelineControl tl = w.GetVisualDescendants().OfType<TimelineControl>().First(t => t.IsEffectivelyVisible);
            while (Shell.Timeline.ToPixels(MediaTime.FromSeconds(10)) - Shell.Timeline.ToPixels(MediaTime.Zero) < 200)
            {
                Shell.Timeline.ZoomStep++;
            }

            TestApp.Pump();
            double width = Shell.Timeline.ToPixels(MediaTime.FromSeconds(10)) - Shell.Timeline.ToPixels(MediaTime.Zero);
            double full = WaveShare(w, tl, width, tl.RowCenter(TimelineTrack.AudioMusic));
            Assert.InRange(full, 0.1, 0.7);

            AudioClip a = Shell.Project.AudioMusicTrack[0];
            Shell.Session.Editor.SetAudio(clip, a.Audio with { Volume = 0.5, FadeIn = true, FadeOut = true });
            TestApp.Pump();
            double half = WaveShare(w, tl, width, tl.RowCenter(TimelineTrack.AudioMusic));
            Assert.InRange(half, full * 0.3, full * 0.7);
            Shell.Session.Editor.SetAudio(clip, a.Audio with { Mute = true });
            TestApp.Pump();
            Assert.Equal(0, WaveShare(w, tl, width, tl.RowCenter(TimelineTrack.AudioMusic)));
            Shell.Session.Editor.SetAudio(clip, a.Audio);
            TestApp.Pump();

            Shell.Timeline.IsVideoExpanded = true;
            w.Height = 1100;
            TestApp.Pump();
            Assert.InRange(WaveShare(w, tl, width, tl.RowCenter(TimelineTrack.AudioMusic)), 0.1, 0.7);
        }
        finally
        {
            TestApp.Close(w);
        }
    }
}
