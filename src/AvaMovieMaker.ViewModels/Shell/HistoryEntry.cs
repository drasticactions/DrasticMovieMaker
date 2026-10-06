using System.Globalization;
using System.Windows.Input;

namespace AvaMovieMaker.ViewModels.Shell;

public sealed record HistoryEntry(string Name, int Count, ICommand Command, bool IsRedo = false)
{
    public const int MaxRows = 16;

    public string Footer => string.Format(CultureInfo.CurrentCulture, (IsRedo, Count == 1) switch
    {
        (false, true) => Strings.UndoActionsOne,
        (false, false) => Strings.UndoActionsMany,
        (true, true) => Strings.RedoActionsOne,
        (true, false) => Strings.RedoActionsMany,
    }, Count);
}
