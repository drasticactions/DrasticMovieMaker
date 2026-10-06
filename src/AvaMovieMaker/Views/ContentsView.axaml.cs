using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using AvaMovieMaker.ViewModels.Contents;

namespace AvaMovieMaker.Views;

public partial class ContentsView : UserControl
{
    private Avalonia.Point? _dragStart;
    private PointerPressedEventArgs? _pressed;
    private ContentsViewModel? _revealSource;

    public ContentsView()
    {
        AvaloniaXamlLoader.Load(this);
        foreach (string name in new[] { "Thumbs", "Details", "Catalog" })
        {
            Control list = this.FindControl<Control>(name)!;
            list.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            list.AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
            list.GotFocus += (_, _) => Vm?.MarkActive();
            list.KeyDown += OnListKeyDown;
        }

        DataContextChanged += (_, _) =>
        {
            if (_revealSource is { } old)
            {
                old.RevealRequested -= OnRevealRequested;
            }

            _revealSource = Vm;
            if (_revealSource is { } vm)
            {
                vm.RevealRequested += OnRevealRequested;
            }
        };
    }

    private void OnRevealRequested(object? sender, ContentItemViewModel item)
    {
        foreach (string name in new[] { "Thumbs", "Details" })
        {
            if (this.FindControl<ListBox>(name) is { } list)
            {
                list.SelectedItems?.Clear();
                list.SelectedItems?.Add(item);
                if (list.IsEffectivelyVisible)
                {
                    list.ScrollIntoView(item);
                }
            }
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || Vm is not { } vm)
        {
            return;
        }

        if (vm.IsMediaView)
        {
            vm.PreviewCommand.Execute(null);
        }
        else
        {
            vm.PreviewCatalogCommand.Execute(null);
        }

        e.Handled = true;
    }

    private ContentsViewModel? Vm => DataContext as ContentsViewModel;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is { } vm && sender is SelectingItemsControl list)
        {
            var selected = (list is ListBox lb ? lb.SelectedItems?.OfType<ContentItemViewModel>() : null) ?? [];
            vm.Select(selected.ToList());
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Vm is { } vm && e.Source is Control { DataContext: ContentItemViewModel item })
        {
            vm.PreviewCommand.Execute(item);
        }
    }

    private void OnCatalogDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Vm is { } vm && e.Source is Control { DataContext: CatalogItemViewModel item })
        {
            vm.PreviewCatalogCommand.Execute(item);
        }
    }

    private void OnThumbnails(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.IsDetails = false;
        }
    }

    private void OnDetails(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.IsDetails = true;
        }
    }

    private void OnRenameKey(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox { DataContext: ContentItemViewModel item } && Vm is { } vm)
        {
            if (e.Key == Key.Enter)
            {
                vm.CommitRename(item);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                item.IsRenaming = false;
                e.Handled = true;
            }
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ContentItemViewModel { IsRenaming: true } item } && Vm is { } vm)
        {
            vm.CommitRename(item);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _pressed = e;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is not { } start || _pressed is not { } pressed || e.GetCurrentPoint(this).Properties is { IsLeftButtonPressed: false, IsRightButtonPressed: false })
        {
            return;
        }

        Avalonia.Point p = e.GetPosition(this);
        if (Math.Abs(p.X - start.X) < 4 && Math.Abs(p.Y - start.Y) < 4)
        {
            return;
        }

        _dragStart = null;
        DragPayload? payload = null;
        if (e.Source is Control { DataContext: ContentItemViewModel item } && Vm is { } vm)
        {
            if (!vm.SelectedItems.Contains(item))
            {
                vm.Select([item]);
            }

            payload = DragPayload.ForMedia(vm.Selection);
        }
        else if (e.Source is Control { DataContext: CatalogItemViewModel cat })
        {
            payload = cat.IsTransition ? DragPayload.ForTransition(cat.Id) : DragPayload.ForEffect(cat.Id);
        }

        if (payload is not null)
        {
            if ((e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault() is { } container)
            {
                payload = payload.WithImage(DragPayload.Snapshot(container), pressed.GetPosition(container));
            }

            await DragPayload.DoDragAsync(pressed, payload);
        }
    }
}
