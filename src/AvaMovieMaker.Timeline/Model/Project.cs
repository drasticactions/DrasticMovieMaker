using System.Text.Json;
using System.Text.Json.Serialization;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Model;

public sealed class Project
{
    public const string FileFormat = "ammproj";
    public const int FileVersion = 2;

    public string Format { get; set; } = FileFormat;

    public int Version { get; set; } = FileVersion;

    public ProjectSettings Settings { get; set; } = new();

    public ProjectProperties Properties { get; set; } = new();

    public List<MediaItem> Media { get; set; } = [];

    public List<VideoClip> VideoTrack { get; set; } = [];

    public List<AudioClip> AudioMusicTrack { get; set; } = [];

    public List<TitleClip> TitleOverlayTrack { get; set; } = [];

    public double AudioLevels { get; set; }

    public int AutoMovieSeed { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string? FilePath { get; set; }

    [JsonIgnore]
    public string DisplayName => FilePath is null ? Strings.ProjectUntitled : System.IO.Path.GetFileNameWithoutExtension(FilePath);

    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public MediaItem? FindMedia(Guid id) => Media.FirstOrDefault(m => m.Id == id);

    public int IndexOfVideo(Guid id) => VideoTrack.FindIndex(c => c.Id == id);

    public bool IsEmpty => VideoTrack.Count == 0 && AudioMusicTrack.Count == 0 && TitleOverlayTrack.Count == 0;

    public MediaTime Duration
    {
        get
        {
            MediaTime end = TimelineLayout.Compute(this).VideoEnd;
            foreach (AudioClip a in AudioMusicTrack)
            {
                end = MediaTime.Max(end, a.End);
            }

            foreach (TitleClip t in TitleOverlayTrack)
            {
                end = MediaTime.Max(end, t.End);
            }

            return end;
        }
    }
}
