namespace AvaMovieMaker.Settings;

public interface ISettingsStore
{
    AppSettings Settings { get; }

    void Save();
}
