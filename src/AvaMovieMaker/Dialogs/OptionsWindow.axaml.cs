using AvaMovieMaker.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaWpf;
using AvaMovieMaker.ViewModels.Dialogs;

namespace AvaMovieMaker.Dialogs;

public partial class OptionsWindow : UserControl
{
    public OptionsWindow() => InitializeComponent();

    private void OnOk(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, true);

    private void OnCancel(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, false);

    private async void OnBrowseTemp(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OptionsViewModel vm)
        {
            return;
        }

        IReadOnlyList<IStorageFolder> picked = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Strings.OptionsTemporaryStorageName });
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
        {
            vm.TemporaryFolder = path;
        }
    }
}
