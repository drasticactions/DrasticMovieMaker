using System.Collections.ObjectModel;
using AvaMovieMaker.Effects.Catalog;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed partial class ClipEffectsViewModel : ObservableObject
{
    public ClipEffectsViewModel(IEnumerable<string> current)
    {
        Available = [.. EffectCatalog.All.OrderBy(e => e.Name, StringComparer.CurrentCulture).Select(e => new EffectListItem(e.Id, e.Name))];
        Displayed = [.. current.Select(EffectCatalog.Find).Where(e => e is not null).Select(e => new EffectListItem(e!.Id, e.Name))];
        Displayed.CollectionChanged += (_, _) => AddCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<EffectListItem> Available { get; }

    public ObservableCollection<EffectListItem> Displayed { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial EffectListItem? SelectedAvailable { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial EffectListItem? SelectedDisplayed { get; set; }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        EffectListItem added = SelectedAvailable!;
        int framing = FramingIndex();
        if (EffectCatalog.IsFraming(added.Id) && framing >= 0)
        {
            Displayed[framing] = added;
            SelectedDisplayed = Displayed[framing];
            return;
        }

        Displayed.Add(added);
        SelectedDisplayed = Displayed[^1];
    }

    private int FramingIndex()
    {
        for (int i = 0; i < Displayed.Count; i++)
        {
            if (EffectCatalog.IsFraming(Displayed[i].Id))
            {
                return i;
            }
        }

        return -1;
    }

    private bool CanAdd() => SelectedAvailable is not null
        && (Displayed.Count < AvaMovieMaker.Timeline.Editing.TimelineEditor.MaxEffects || (EffectCatalog.IsFraming(SelectedAvailable.Id) && FramingIndex() >= 0));

    [RelayCommand(CanExecute = nameof(HasDisplayed))]
    private void Remove()
    {
        int i = Displayed.IndexOf(SelectedDisplayed!);
        Displayed.RemoveAt(i);
        SelectedDisplayed = Displayed.Count == 0 ? null : Displayed[Math.Min(i, Displayed.Count - 1)];
    }

    private bool HasDisplayed() => SelectedDisplayed is not null;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        int i = Displayed.IndexOf(SelectedDisplayed!);
        EffectListItem item = SelectedDisplayed!;
        Displayed.Move(i, i - 1);
        SelectedDisplayed = item;
    }

    private bool CanMoveUp() => SelectedDisplayed is not null && Displayed.IndexOf(SelectedDisplayed) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        int i = Displayed.IndexOf(SelectedDisplayed!);
        EffectListItem item = SelectedDisplayed!;
        Displayed.Move(i, i + 1);
        SelectedDisplayed = item;
    }

    private bool CanMoveDown() => SelectedDisplayed is not null && Displayed.IndexOf(SelectedDisplayed) < Displayed.Count - 1;

    public IReadOnlyList<string> Result => Displayed.Select(d => d.Id).ToList();
}
