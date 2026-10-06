namespace AvaMovieMaker.Undo;

public sealed class CompositeCommand(string name, IReadOnlyList<IUndoableCommand> commands) : IUndoableCommand
{
    public string Name { get; } = name;

    public IReadOnlyList<IUndoableCommand> Commands { get; } = commands;

    public void Do()
    {
        foreach (IUndoableCommand c in Commands)
        {
            c.Do();
        }
    }

    public void Undo()
    {
        for (int i = Commands.Count - 1; i >= 0; i--)
        {
            Commands[i].Undo();
        }
    }
}
