using System.Windows.Input;

namespace AvaMovieMaker.ViewModels.Shell;

public sealed record TaskEntry(string Text, ICommand Command, string Automation)
{
    public static TaskEntry Unavailable(string text) => new(text, NeverCommand.Instance, string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.NotAvailable, text));

    private sealed class NeverCommand : ICommand
    {
        public static readonly NeverCommand Instance = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => false;

        public void Execute(object? parameter)
        {
        }
    }
}
