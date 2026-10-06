using Avalonia.Controls;
using Avalonia.Input;
using AvaWpf;
using AvaMovieMaker.ViewModels.Publish;

namespace AvaMovieMaker.Dialogs;

public partial class PublishWizardWindow : UserControl
{
    public PublishWizardWindow()
    {
        InitializeComponent();
        WindowHost.AddOpenedHandler(this, (_, _) => FocusPage());
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PublishWizardViewModel vm)
            {
                vm.Closed += (_, _) => WindowHost.CloseDialog(this);
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(PublishWizardViewModel.Page))
                    {
                        FocusPage();
                    }
                };
            }
        };
        WindowHost.AddClosingHandler(this, (_, e) =>
        {
            if (DataContext is PublishWizardViewModel { IsProgressPage: true } vm)
            {
                vm.CancelCommand.Execute(null);
                e.Cancel = true;
            }
        });
    }

    private void FocusPage()
    {
        if (DataContext is PublishWizardViewModel { IsNamePage: true })
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }
        else if (DataContext is PublishWizardViewModel { IsWherePage: true })
        {
            Destinations.Focus();
        }
    }

    private void OnDestinationDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is PublishWizardViewModel vm)
        {
            vm.NextCommand.Execute(null);
        }
    }
}
