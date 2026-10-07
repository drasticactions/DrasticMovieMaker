using Avalonia.Controls;
using Avalonia.Data;
using AvaMovieMaker.Controls;

namespace AvaMovieMaker.Menus;

public sealed class InWindowMenuHost : IMenuHost
{
    public static readonly Avalonia.AttachedProperty<string?> PromptProperty =
        Avalonia.AvaloniaProperty.RegisterAttached<InWindowMenuHost, MenuItem, string?>("Prompt");

    public static string? GetPrompt(MenuItem item) => item.GetValue(PromptProperty);

    public static void SetPrompt(MenuItem item, string? prompt) => item.SetValue(PromptProperty, prompt);

    public void Show(Menu inWindow, IReadOnlyList<MenuItemEntry> menus)
    {
        inWindow.Items.Clear();
        foreach (MenuItemEntry menu in menus)
        {
            inWindow.Items.Add(Create(menu));
        }

        inWindow.IsVisible = true;
    }

    public static Control Create(MenuEntry entry)
    {
        if (entry is MenuSeparator separator)
        {
            return new Separator { Name = separator.Name };
        }

        if (entry is MenuDynamic)
        {
            return new Separator { IsVisible = false, Tag = entry };
        }

        var e = (MenuItemEntry)entry;
        var item = new MenuItem
        {
            Name = e.Name,
            Header = e.Header,
            CommandParameter = e.CommandParameter,
            Command = e.Command,
            InputGesture = e.Shortcut?.Gesture,
            IsEnabled = e.IsEnabled,
            ToggleType = e.Toggle switch
            {
                MenuToggle.CheckBox => MenuItemToggleType.CheckBox,
                MenuToggle.Radio => MenuItemToggleType.Radio,
                _ => MenuItemToggleType.None,
            },
            GroupName = e.Group,
        };
        SetPrompt(item, e.Prompt);
        if (e.Icon is { } icon)
        {
            item.Icon = new Viewbox { Width = 16, Height = 16, Child = new Glyph { Kind = icon } };
        }

        if (e.ToolTip is { } tip)
        {
            ToolTip.SetTip(item, tip);
        }

        Bind(item, MenuItem.HeaderProperty, e.HeaderBinding);
        Bind(item, MenuItem.IsCheckedProperty, e.IsChecked);
        Bind(item, Avalonia.Visual.IsVisibleProperty, e.IsVisible);
        Bind(item, Avalonia.Input.InputElement.IsEnabledProperty, e.IsEnabledBinding);
        foreach (MenuEntry child in e.Items)
        {
            item.Items.Add(Create(child));
        }

        if (e.Items.Any(c => c is MenuDynamic))
        {
            item.SubmenuOpened += (_, args) =>
            {
                if (ReferenceEquals(args.Source, item))
                {
                    FillDynamic(item);
                }
            };
        }

        return item;
    }

    private static void FillDynamic(MenuItem menu)
    {
        foreach (object? old in menu.Items.OfType<Control>().Where(c => c.Tag is DynamicItem).ToList())
        {
            menu.Items.Remove(old);
        }

        foreach (Separator marker in menu.Items.OfType<Separator>().Where(s => s.Tag is MenuDynamic).ToList())
        {
            int at = menu.Items.IndexOf(marker) + 1;
            foreach (MenuEntry made in ((MenuDynamic)marker.Tag!).Items())
            {
                Control control = Create(made);
                control.Tag = DynamicItem.Instance;
                menu.Items.Insert(at++, control);
            }
        }
    }

    private static void Bind(MenuItem item, Avalonia.AvaloniaProperty property, BindingBase? binding)
    {
        if (binding is not null)
        {
            item.Bind(property, binding);
        }
    }

    private sealed class DynamicItem
    {
        public static readonly DynamicItem Instance = new();
    }
}
