using AvaMovieMaker.Settings;
using AvaMovieMaker.TestSupport;

namespace AvaMovieMaker.ViewModels.Tests;

public sealed class SettingsMigrationTests
{
    [Theory]
    [InlineData("""{ "widescreenVideo": true }""", "16:9")]
    [InlineData("""{ "widescreenVideo": false }""", "4:3")]
    [InlineData("""{ "widescreenVideo": true, "defaultAspect": "9:16" }""", "9:16")]
    [InlineData("""{ "defaultAspect": "1:1" }""", "1:1")]
    [InlineData("""{ }""", "4:3")]
    public void Widescreen_setting_migrates_to_default_aspect(string json, string expected)
    {
        using var temp = new TempFolder();
        string path = temp.File("settings.json");
        File.WriteAllText(path, json);
        var store = new JsonSettingsStore(path);
        Assert.Equal(expected, store.Settings.DefaultAspect);
        Assert.False(store.Settings.WidescreenVideo);

        store.Save();
        string saved = File.ReadAllText(path);
        Assert.DoesNotContain("widescreenVideo", saved, StringComparison.Ordinal);
        Assert.Contains($"\"defaultAspect\": \"{expected}\"", saved, StringComparison.Ordinal);
        Assert.Equal(expected, new JsonSettingsStore(path).Settings.DefaultAspect);
    }
}
