using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaMovieMaker.Settings;

public sealed class AppSettings
{
    public const int DefaultRecentProjectCount = 4;
    public const int MinRecentProjectCount = 2;
    public const int MaxRecentProjectCount = 16;

    public string DefaultAuthor { get; set; } = string.Empty;

    public int RecentProjectCount { get; set; } = DefaultRecentProjectCount;

    public double PictureDurationSeconds { get; set; } = 5;

    public double TransitionDurationSeconds { get; set; } = 1.25;

    public bool PalVideo { get; set; }

    public bool WidescreenVideo { get; set; }

    public bool PlayPublishedMovie { get; set; } = true;

    public string TemporaryFolder { get; set; } = string.Empty;

    public bool OpenLastProjectOnStartup { get; set; }

    public bool OmitPublishedMetadata { get; set; }

    public bool AutoRecoveryEnabled { get; set; } = true;

    public int AutoRecoveryMinutes { get; set; } = 10;

    public List<string> DismissedWarnings { get; set; } = [];

    public List<string> RecentProjects { get; set; } = [];

    public string LastImportFolder { get; set; } = string.Empty;

    public string ImportFolderAllMedia { get; set; } = string.Empty;

    public string ImportFolderVideo { get; set; } = string.Empty;

    public string ImportFolderPictures { get; set; } = string.Empty;

    public string ImportFolderAudio { get; set; } = string.Empty;

    public string LastPublishFolder { get; set; } = string.Empty;

    public string LastPictureFolder { get; set; } = string.Empty;

    public WindowPlacement Window { get; set; } = new();

    public string PlaybackDevice { get; set; } = string.Empty;

    public string CaptureDevice { get; set; } = string.Empty;

    public string NarrationFolder { get; set; } = string.Empty;

    public int NarrationInputLevel { get; set; } = 32768;

    public bool NarrationShowOptions { get; set; }

    public bool NarrationLimitToFreeSpace { get; set; } = true;

    public bool HardwareDecode { get; set; } = true;

    public bool HardwareEncode { get; set; }

    public string Theme { get; set; } = "System";

    public List<uint> CustomColors { get; set; } = [];

    public bool CreateClipsOnImport { get; set; }

    public bool ShowTimeline { get; set; }

    public bool ShowTasksPane { get; set; } = true;

    public bool ShowCollectionsPane { get; set; }

    public bool ShowStatusBar { get; set; }

    public bool ContentsDetails { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public void AddRecent(string path)
    {
        RecentProjects.RemoveAll(p => string.Equals(p, path, StringComparison.Ordinal));
        RecentProjects.Insert(0, path);
        TrimRecent();
    }

    [JsonIgnore]
    public int RecentLimit => Math.Clamp(RecentProjectCount, MinRecentProjectCount, MaxRecentProjectCount);

    public void TrimRecent()
    {
        if (RecentProjects.Count > RecentLimit)
        {
            RecentProjects.RemoveRange(RecentLimit, RecentProjects.Count - RecentLimit);
        }
    }
}
