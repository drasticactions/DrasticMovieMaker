using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaWpf;
using AvaMovieMaker.ViewModels.Dialogs;

namespace AvaMovieMaker.Dialogs;

public partial class ProjectPropertiesWindow : UserControl
{
    public ProjectPropertiesWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ProjectPropertiesViewModel vm)
            {
                vm.PropertyChanged += (_, _) => ApplyButton.IsEnabled = true;
            }
        };
        WindowHost.AddOpenedHandler(this, (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        });
    }

    private void OnOk(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, true);

    private void OnCancel(object? sender, RoutedEventArgs e) => WindowHost.CloseDialog(this, false);

    private void OnApply(object? sender, RoutedEventArgs e)
    {
        (DataContext as ProjectPropertiesViewModel)?.Apply();
        ApplyButton.IsEnabled = false;
    }
}
