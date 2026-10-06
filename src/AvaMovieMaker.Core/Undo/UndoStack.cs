namespace AvaMovieMaker.Undo;

public sealed class UndoStack
{
    private readonly List<IUndoableCommand> _undo = [];
    private readonly List<IUndoableCommand> _redo = [];
    private List<IUndoableCommand>? _gesture;
    private string? _gestureName;

    private int _savePoint;

    public event EventHandler? Changed;

    public event EventHandler<IUndoableCommand>? Applied;

    public bool CanUndo => _undo.Count > 0 && _gesture is null;

    public bool CanRedo => _redo.Count > 0 && _gesture is null;

    public string? UndoName => _undo.Count > 0 ? _undo[^1].Name : null;

    public string? RedoName => _redo.Count > 0 ? _redo[^1].Name : null;

    public int Count => _undo.Count;

    public IReadOnlyList<string> UndoNames => Enumerable.Range(0, _undo.Count).Select(i => _undo[_undo.Count - 1 - i].Name).ToList();

    public IReadOnlyList<string> RedoNames => Enumerable.Range(0, _redo.Count).Select(i => _redo[_redo.Count - 1 - i].Name).ToList();

    public bool IsDirty => _savePoint != _undo.Count;

    public bool InGesture => _gesture is not null;

    public void Execute(IUndoableCommand command)
    {
        command.Do();
        if (_gesture is not null)
        {
            if (_gesture.Count == 0 || !_gesture[^1].TryMerge(command))
            {
                _gesture.Add(command);
            }
        }
        else
        {
            Push(command);
        }

        Applied?.Invoke(this, command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Push(IUndoableCommand command)
    {
        if (_savePoint > _undo.Count)
        {
            _savePoint = -1;
        }

        if (_undo.Count == 0 || _undo.Count == _savePoint || !_undo[^1].TryMerge(command))
        {
            _undo.Add(command);
        }

        _redo.Clear();
    }

    public void BeginGesture(string name)
    {
        if (_gesture is not null)
        {
            throw new InvalidOperationException("A gesture is already open.");
        }

        _gesture = [];
        _gestureName = name;
    }

    public void EndGesture()
    {
        List<IUndoableCommand>? g = _gesture ?? throw new InvalidOperationException("No gesture is open.");
        _gesture = null;
        if (g.Count == 1)
        {
            Push(g[0]);
        }
        else if (g.Count > 1)
        {
            Push(new CompositeCommand(_gestureName ?? g[0].Name, g));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CancelGesture()
    {
        List<IUndoableCommand>? g = _gesture ?? throw new InvalidOperationException("No gesture is open.");
        _gesture = null;
        for (int i = g.Count - 1; i >= 0; i--)
        {
            g[i].Undo();
            Applied?.Invoke(this, g[i]);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        IUndoableCommand c = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        c.Undo();
        _redo.Add(c);
        Applied?.Invoke(this, c);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        IUndoableCommand c = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        c.Do();
        _undo.Add(c);
        Applied?.Invoke(this, c);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkSaved()
    {
        _savePoint = _undo.Count;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkDirty()
    {
        _savePoint = -1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _gesture = null;
        _savePoint = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
