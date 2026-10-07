using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia.Data;

namespace AvaMovieMaker.Menus;

public abstract record MenuEntry;

public sealed record MenuSeparator(string? Name = null) : MenuEntry;

public sealed record MenuDynamic(Func<IReadOnlyList<MenuEntry>> Items, INotifyCollectionChanged? Changed = null) : MenuEntry;

public enum MenuToggle
{
    None,
    CheckBox,
    Radio,
}

public enum MenuRole
{
    None,
    About,
    Options,
    Exit,
}

public sealed record MenuItemEntry(string Header) : MenuEntry
{
    public string? Name { get; init; }

    public BindingBase? HeaderBinding { get; init; }

    public ICommand? Command { get; init; }

    public object? CommandParameter { get; init; }

    public Shortcut? Shortcut { get; init; }

    public string? Icon { get; init; }

    public MenuToggle Toggle { get; init; }

    public string? Group { get; init; }

    public BindingBase? IsChecked { get; init; }

    public BindingBase? IsVisible { get; init; }

    public bool IsEnabled { get; init; } = true;

    public BindingBase? IsEnabledBinding { get; init; }

    public string? ToolTip { get; init; }

    public string? Prompt { get; init; }

    public MenuRole Role { get; init; }

    public IReadOnlyList<MenuEntry> Items { get; init; } = [];
}
