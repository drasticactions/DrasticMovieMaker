using Avalonia.Controls;
using Avalonia.Threading;
using AvaMovieMaker.ViewModels.Titles;

namespace AvaMovieMaker.Views;

public partial class TitleEditorView : UserControl
{
    public TitleEditorView()
    {
        InitializeComponent();

        AnimationList.ContainerPrepared += (_, e) => e.Container.IsEnabled = AnimationList.Items[e.Index] is not AnimationGroupHeader;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
            {
                FocusFirst();
            }
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TitleEditorViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TitleEditorViewModel.Page))
                    {
                        FocusFirst();
                    }
                };
            }
        };
    }

    private void FocusFirst() => Dispatcher.UIThread.Post(() =>
    {
        if (DataContext is TitleEditorViewModel { IsTextPage: true, IsCredits: false })
        {
            Line1Box.Focus();
        }
        else if (DataContext is TitleEditorViewModel { IsAnimationPage: true } && AnimationList.SelectedItem is { } item)
        {
            AnimationList.ScrollIntoView(item);
        }
    }, DispatcherPriority.Background);
}
