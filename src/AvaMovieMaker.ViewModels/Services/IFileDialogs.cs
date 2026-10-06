using AvaMovieMaker.Media;

namespace AvaMovieMaker.ViewModels.Services;

public enum MediaFilter
{
    All,
    Video,
    Pictures,
    Audio,
}

public static class ImportFilters
{
    public static IReadOnlyList<(string Name, IReadOnlyList<string> Extensions)> All { get; } =
    [
        (Strings.FilterAllMedia, [.. MediaFormats.Audio.Concat(MediaFormats.Pictures).Concat(MediaFormats.Video).Distinct(StringComparer.Ordinal)]),
        (Strings.FilterAudio, MediaFormats.Audio),
        (Strings.FilterPicturesAndVideo, [.. MediaFormats.Pictures.Concat(MediaFormats.Video).Distinct(StringComparer.Ordinal)]),
        (Strings.FilterAllFiles, [".*"]),
    ];

    public static int SelectedIndex(MediaFilter filter) => filter switch
    {
        MediaFilter.Audio => 1,
        MediaFilter.Video or MediaFilter.Pictures => 2,
        _ => 0,
    };
}

public interface IFileDialogs
{
    Task<IReadOnlyList<string>> ImportMediaAsync(MediaFilter filter, string? startFolder);

    Task<string?> OpenProjectAsync(string? startFolder);

    Task<string?> OpenMswmmAsync(string? startFolder);

    Task<string?> SaveProjectAsync(string suggestedName, string? startFolder);

    Task<string?> SavePackageAsync(string suggestedName, string? startFolder);

    Task<string?> PackageMediaFolderAsync(string projectName);

    Task<string?> SavePictureAsync(string suggestedName, string? startFolder);

    Task<string?> SaveNarrationAsync(string suggestedName, string? startFolder);

    Task<string?> PickFolderAsync(string title, string? startFolder);

    Task<string?> OpenFileAsync(string title, string? startFolder);
}
