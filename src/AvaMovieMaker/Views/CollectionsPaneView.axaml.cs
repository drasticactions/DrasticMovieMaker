using Avalonia.Controls;
using Avalonia.Input;
using AvaMovieMaker.ViewModels.Shell;

namespace AvaMovieMaker.Views;

public partial class CollectionsPaneView : UserControl
{
    public CollectionsPaneView() => InitializeComponent();

    private void OnImportedMedia(object? sender, TappedEventArgs e)
    {
        if (DataContext is ShellViewModel vm && vm.ShowImportedMediaCommand.CanExecute(null))
        {
            vm.ShowImportedMediaCommand.Execute(null);
        }
    }
}
