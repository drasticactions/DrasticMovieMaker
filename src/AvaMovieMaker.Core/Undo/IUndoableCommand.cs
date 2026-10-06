namespace AvaMovieMaker.Undo;

public interface IUndoableCommand
{
    string Name { get; }

    void Do();

    void Undo();

    bool TryMerge(IUndoableCommand next) => false;
}
