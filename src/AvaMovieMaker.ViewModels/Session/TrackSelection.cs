namespace AvaMovieMaker.ViewModels.Session;

public enum SelectionKey
{
    Next,
    Previous,
    First,
    Last,

    SelectActive,

    ToggleActive,

    ExtendToActive,

    Clear,
}

public static class TrackSelection
{
    public readonly record struct State(IReadOnlyList<Guid> Selected, Guid? Anchor, Guid? Active);

    public static State Click(IReadOnlyList<Guid> order, State s, Guid id, bool ctrl, bool shift)
    {
        if (shift)
        {
            Guid anchor = s.Anchor is { } a && order.Contains(a) ? a : id;
            IEnumerable<Guid> range = Range(order, anchor, id);
            return new State(ctrl ? [.. s.Selected.Union(range)] : [.. range], anchor, id);
        }

        if (ctrl)
        {
            var next = s.Selected.ToList();
            if (!next.Remove(id))
            {
                next.Add(id);
            }

            return new State(next, id, id);
        }

        return s.Selected.Contains(id) ? s with { Active = id, Anchor = id } : new State([id], id, id);
    }

    public static State Collapse(State s, Guid id) => new([id], id, id);

    public static State Key(IReadOnlyList<Guid> order, State s, SelectionKey key, bool shift, bool ctrl, Guid? atIndicator, out Guid? moved)
    {
        moved = null;
        if (order.Count == 0)
        {
            return s;
        }

        Guid? active = s.Active is { } a && order.Contains(a) ? a : null;
        switch (key)
        {
            case SelectionKey.Clear:
                return new State([], s.Anchor, active);
            case SelectionKey.SelectActive:
                if (active is not { } sa || s.Selected.Contains(sa))
                {
                    return s;
                }

                moved = sa;
                return new State([sa], sa, sa);
            case SelectionKey.ToggleActive:
                return active is { } ta ? Click(order, s, ta, ctrl: true, shift: false) : s;
            case SelectionKey.ExtendToActive:
                return active is { } ea ? Click(order, s, ea, ctrl: false, shift: true) : s;
        }

        int from = active is { } f ? IndexOf(order, f) : atIndicator is { } ai && order.Contains(ai) ? IndexOf(order, ai) : -1;
        int to = key switch
        {
            SelectionKey.First => 0,
            SelectionKey.Last => order.Count - 1,
            SelectionKey.Next => from < 0 ? 0 : Math.Min(order.Count - 1, from + (active is null ? 0 : 1)),
            _ => from < 0 ? order.Count - 1 : Math.Max(0, from - (active is null ? 0 : 1)),
        };
        Guid target = order[to];
        if (ctrl && !shift)
        {
            return s with { Active = target };
        }

        if (shift)
        {
            Guid anchor = s.Anchor is { } an && order.Contains(an) ? an : active ?? target;
            return new State([.. Range(order, anchor, target)], anchor, target);
        }

        moved = target;
        return new State([target], target, target);
    }

    public static IEnumerable<Guid> Range(IReadOnlyList<Guid> order, Guid a, Guid b)
    {
        int i = IndexOf(order, a), j = IndexOf(order, b);
        if (i < 0 || j < 0)
        {
            return [b];
        }

        return order.Skip(Math.Min(i, j)).Take(Math.Abs(i - j) + 1);
    }

    private static int IndexOf(IReadOnlyList<Guid> order, Guid id)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == id)
            {
                return i;
            }
        }

        return -1;
    }
}
