using Avalonia;
using Avalonia.Platform.Storage;
using AvaMovieMaker.Dialogs;
using AvaMovieMaker.Menus;
using AvaMovieMaker.Services;
using AvaMovieMaker.ViewModels.Services;

namespace AvaMovieMaker;

public sealed record AppPlatform
{
    public static AppPlatform ForDesktop() => new()
    {
        FileDialogs = owner => new FileDialogs(owner),
        FullScreen = new FullScreenWindowHost(),
        Shell = new DesktopShellOpener(),
        MenuHost = OperatingSystem.IsMacOS() ? new NativeMenuHost() : new InWindowMenuHost(),
        ComposePreviewOnInterfaceGpu = OperatingSystem.IsMacOS() || OperatingSystem.IsWindows(),
    };

    public required Func<Visual, IFileDialogs> FileDialogs { get; init; }

    public required IFullScreenHost FullScreen { get; init; }

    public required IShellOpener Shell { get; init; }

    public IMenuHost MenuHost { get; init; } = new InWindowMenuHost();

    public bool ComposePreviewOnInterfaceGpu { get; init; }

    public Func<IReadOnlyList<IStorageItem>, Task<IReadOnlyList<string>>> DroppedFiles { get; init; } = items =>
        Task.FromResult<IReadOnlyList<string>>([.. items.OfType<IStorageFile>().Select(f => f.TryGetLocalPath()).OfType<string>()]);
}
