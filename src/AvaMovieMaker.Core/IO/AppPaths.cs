namespace AvaMovieMaker.IO;

public static class AppPaths
{
    public const string AppName = "AvaMovieMaker";

    public static string? RootOverride { get; set; }

    public static string ConfigDir => Combine("XDG_CONFIG_HOME", ".config",
        mac: Path.Combine("Library", "Application Support", AppName),
        windows: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName));

    public static string CacheDir => Combine("XDG_CACHE_HOME", ".cache",
        mac: Path.Combine("Library", "Caches", AppName),
        windows: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "Cache"));

    public static string StateDir => Combine("XDG_STATE_HOME", Path.Combine(".local", "state"),
        mac: Path.Combine("Library", "Logs", AppName),
        windows: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "Logs"));

    public static string SettingsFile => Path.Combine(ConfigDir, "settings.json");

    public static string LogFile => Path.Combine(StateDir, "log.txt");

    public static string VideosDir => UserDir("XDG_VIDEOS_DIR", "Videos", "Movies", Environment.SpecialFolder.MyVideos);

    public static string PicturesDir => UserDir("XDG_PICTURES_DIR", "Pictures", "Pictures", Environment.SpecialFolder.MyPictures);

    public static string MusicDir => UserDir("XDG_MUSIC_DIR", "Music", "Music", Environment.SpecialFolder.MyMusic);

    public static string HomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Combine(string variable, string fallback, string mac, string windows)
    {
        if (RootOverride is { } root)
        {
            return Path.Combine(root, variable.ToLowerInvariant(), AppName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(HomeDir, mac);
        }

        if (OperatingSystem.IsWindows())
        {
            return windows;
        }

        string? baseDir = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(baseDir) || !Path.IsPathRooted(baseDir))
        {
            baseDir = Path.Combine(HomeDir, fallback);
        }

        return Path.Combine(baseDir, AppName);
    }

    private static string UserDir(string key, string fallback, string mac, Environment.SpecialFolder windows)
    {
        if (RootOverride is { } root)
        {
            return Path.Combine(root, fallback);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(HomeDir, mac);
        }

        if (OperatingSystem.IsWindows())
        {
            return Environment.GetFolderPath(windows, Environment.SpecialFolderOption.DoNotVerify);
        }

        string? env = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }

        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } c ? c : Path.Combine(HomeDir, ".config");
        string file = Path.Combine(configHome, "user-dirs.dirs");
        try
        {
            if (File.Exists(file))
            {
                foreach (string raw in File.ReadLines(file))
                {
                    string line = raw.Trim();
                    if (!line.StartsWith(key + "=", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string value = line[(key.Length + 1)..].Trim('"');
                    return value.Replace("$HOME", HomeDir, StringComparison.Ordinal);
                }
            }
        }
        catch (IOException)
        {
        }

        return Path.Combine(HomeDir, fallback);
    }
}
