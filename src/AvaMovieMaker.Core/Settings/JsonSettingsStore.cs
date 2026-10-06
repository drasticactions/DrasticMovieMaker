using System.Text.Json;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.IO;

namespace AvaMovieMaker.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;

    public JsonSettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFile;
        Settings = Load(_path);
    }

    public AppSettings Settings { get; }

    public static AppSettings Load(string path)
    {
        try
        {
            if (FileStore.Current.Exists(path))
            {
                using Stream s = FileStore.Current.OpenRead(path);
                return JsonSerializer.Deserialize(s, CoreJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warn("settings", $"Could not read {path}: {e.Message}");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            AtomicFile.Write(_path, s => JsonSerializer.Serialize(s, Settings, CoreJsonContext.Default.AppSettings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn("settings", $"Could not write {_path}: {e.Message}");
        }
    }
}
