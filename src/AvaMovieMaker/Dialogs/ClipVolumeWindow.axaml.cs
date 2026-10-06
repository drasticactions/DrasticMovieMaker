using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaWpf;

namespace AvaMovieMaker.Dialogs;

public partial class ClipVolumeWindow : UserControl
{
    public ClipVolumeWindow() => InitializeComponent();

    private void OnOk(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, true);

    private void OnCancel(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, false);
}
