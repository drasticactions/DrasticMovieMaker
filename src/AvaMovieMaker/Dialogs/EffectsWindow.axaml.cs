using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaWpf;
using AvaMovieMaker.ViewModels.Dialogs;

namespace AvaMovieMaker.Dialogs;

public partial class EffectsWindow : UserControl
{
    public EffectsWindow() => InitializeComponent();

    private void OnAvailableDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ClipEffectsViewModel vm && vm.AddCommand.CanExecute(null))
        {
            vm.AddCommand.Execute(null);
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, true);

    private void OnCancel(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, false);
}
