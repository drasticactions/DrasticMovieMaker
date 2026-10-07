using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AvaMovieMaker.IO;
using AvaMovieMaker.ViewModels;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker.Services;

internal sealed class FileDialogs(Visual owner) : IFileDialogs
{
    private IStorageProvider Storage => (TopLevel.GetTopLevel(owner) ?? throw new InvalidOperationException("The shell is not shown.")).StorageProvider;

    private static FilePickerFileType Type(string name, IEnumerable<string> extensions) =>
        new(name) { Patterns = extensions.Select(e => "*" + e).ToList() };

    private async Task<IStorageFolder?> Folder(string? path) =>
        string.IsNullOrEmpty(path) || !Directory.Exists(path) ? null : await Storage.TryGetFolderFromPathAsync(path);

    public async Task<IReadOnlyList<string>> ImportMediaAsync(MediaFilter filter, string? startFolder)
    {
        var types = ImportFilters.All.Select(f => Type(f.Name, f.Extensions)).ToList();
        IReadOnlyList<IStorageFile> files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.ImportDialogTitle,
            AllowMultiple = true,
            FileTypeFilter = types,
            SuggestedFileType = types[ImportFilters.SelectedIndex(filter)],
            SuggestedStartLocation = await Folder(startFolder),
        });
        return files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Cast<string>().ToList();
    }

    public async Task<string?> OpenProjectAsync(string? startFolder)
    {
        IReadOnlyList<IStorageFile> files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.OpenProjectTitle,
            FileTypeFilter = [Type(Strings.FilterProjects, [".dmmproj"]), Type(Strings.FilterPackages, [".dmmpkg"]), Type(Strings.FilterMswmm, [".MSWMM", ".mswmm"])],
            SuggestedStartLocation = await Folder(startFolder),
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> OpenMswmmAsync(string? startFolder)
    {
        IReadOnlyList<IStorageFile> files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.ImportMswmmTitle,
            FileTypeFilter = [Type(Strings.FilterMswmm, [".MSWMM", ".mswmm"])],
            SuggestedStartLocation = await Folder(startFolder),
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SavePackageAsync(string suggestedName, string? startFolder)
    {
        IStorageFile? file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.ExportPackageTitle,
            SuggestedFileName = suggestedName + ".dmmpkg",
            DefaultExtension = "dmmpkg",
            FileTypeChoices = [Type(Strings.FilterPackages, [".dmmpkg"])],
            SuggestedStartLocation = await Folder(startFolder),
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PackageMediaFolderAsync(string projectName) =>
        await PickFolderAsync(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.PickPackageMediaFolder, projectName), AppPaths.VideosDir) is { } parent
            ? Path.Combine(parent, projectName)
            : null;

    public async Task<string?> SaveProjectAsync(string suggestedName, string? startFolder)
    {
        IStorageFile? file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.SaveProjectAsTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = "dmmproj",
            FileTypeChoices = [Type(Strings.FilterProjects, [".dmmproj"])],
            SuggestedStartLocation = await Folder(startFolder),
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> SavePictureAsync(string suggestedName, string? startFolder)
    {
        IStorageFile? file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.SavePictureAsTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = "jpg",
            FileTypeChoices = [Type(Strings.FilterPictures, [".jpg", ".jpeg", ".jpe"])],
            ShowOverwritePrompt = true,
            SuggestedStartLocation = await Folder(startFolder),
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> SaveNarrationAsync(string suggestedName, string? startFolder)
    {
        IStorageFile? file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.SaveNarrationTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = "flac",
            FileTypeChoices = [Type(Strings.FilterFlac, [".flac"])],
            SuggestedStartLocation = await Folder(startFolder),
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string title, string? startFolder)
    {
        IReadOnlyList<IStorageFolder> folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = await Folder(startFolder),
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> OpenFileAsync(string title, string? startFolder)
    {
        IReadOnlyList<IStorageFile> files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = await Folder(startFolder),
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
