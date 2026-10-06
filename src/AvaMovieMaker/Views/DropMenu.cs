using AvaMovieMaker.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AvaMovieMaker.Views;

public static class DropMenu
{
    internal static ContextMenu? Current { get; private set; }

    public static Task<DragDropEffects> ChooseAsync(Control owner, bool canMove)
    {
        var done = new TaskCompletionSource<DragDropEffects>();
        MenuItem Item(string header, DragDropEffects result)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => done.TrySetResult(result);
            return item;
        }

        var menu = new ContextMenu { Placement = PlacementMode.Pointer };
        menu.Items.Add(Item(Strings.DropCopy, DragDropEffects.Copy));
        if (canMove)
        {
            menu.Items.Add(Item(Strings.DropMove, DragDropEffects.Move));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.ButtonCancel, DragDropEffects.None));

        menu.Closed += (_, _) =>
        {
            Current = null;
            Dispatcher.UIThread.Post(() => done.TrySetResult(DragDropEffects.None));
        };
        Current = menu;
        menu.Open(owner);
        return done.Task;
    }
}
