using AvaMovieMaker.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace AvaMovieMaker.Menus;

public sealed class NativeMenuHost : IMenuHost
{
    public static readonly KeyGesture SettingsGesture = new(Key.OemComma, KeyModifiers.Meta);

    private static readonly KeyGesture[] Reserved =
    [
        new(Key.Q, KeyModifiers.Meta),
        new(Key.H, KeyModifiers.Meta),
        new(Key.Q, KeyModifiers.Meta | KeyModifiers.Alt),
        SettingsGesture,
    ];

    private readonly NativeMenuItem _about = new(Strings.MacAbout);
    private readonly NativeMenuItem _settings = new(Strings.MacSettings) { Gesture = SettingsGesture };

    public void InstallApplicationMenu(Application app)
    {
        var menu = new NativeMenu();
        menu.Add(_about);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_settings);
        NativeMenu.SetMenu(app, menu);
    }

    public void Show(Menu inWindow, IReadOnlyList<MenuItemEntry> menus)
    {
        inWindow.Items.Clear();
        inWindow.IsVisible = false;
        if (inWindow.Parent is Control band)
        {
            band.IsVisible = false;
        }

        foreach (MenuItemEntry e in All(menus))
        {
            NativeMenuItem? target = e.Role switch
            {
                MenuRole.About => _about,
                MenuRole.Options => _settings,
                MenuRole.Exit => QuitItem(),
                _ => null,
            };
            if (target is not null)
            {
                target.Command = e.Command;
                target.CommandParameter = e.CommandParameter;
            }
        }

        if (QuitItem() is { } quit)
        {
            quit.Header = Strings.MacQuit;
        }

        NativeMenu bar = Build(menus);
        if (TopLevel.GetTopLevel(inWindow) is Window window)
        {
            NativeMenu.SetMenu(window, bar);
        }
        else
        {
            inWindow.AttachedToVisualTree += Attach;

            void Attach(object? sender, VisualTreeAttachmentEventArgs args)
            {
                inWindow.AttachedToVisualTree -= Attach;
                if (TopLevel.GetTopLevel(inWindow) is Window w)
                {
                    NativeMenu.SetMenu(w, bar);
                }
            }
        }
    }

    public static NativeMenu Build(IReadOnlyList<MenuItemEntry> menus)
    {
        IReadOnlyList<MenuEntry> all = [.. menus.SelectMany(m => All(m.Items).Prepend(m))];
        HashSet<KeyGesture> shared = [.. all.OfType<MenuItemEntry>()
            .Where(e => e.Shortcut is not null)
            .GroupBy(e => e.Shortcut!.Value.Gesture)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];
        var bar = new NativeMenu();
        foreach (MenuItemEntry menu in menus)
        {
            bar.Add(Item(menu, shared));
        }

        return bar;
    }

    public static string WithoutAccessKey(string header) =>
        header.Replace("__", "\u0001", StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace('\u0001', '_');

    private static NativeMenuItem Item(MenuItemEntry e, HashSet<KeyGesture> shared)
    {
        KeyGesture? gesture = e.Shortcut?.Gesture;
        var item = new NativeMenuItem(WithoutAccessKey(e.Header))
        {
            CommandParameter = e.CommandParameter,
            Command = e.Command,
            Gesture = gesture is null || shared.Contains(gesture) || Reserved.Contains(gesture) ? null : gesture,
            ToolTip = e.ToolTip,
            ToggleType = e.Toggle switch
            {
                MenuToggle.CheckBox => MenuItemToggleType.CheckBox,
                MenuToggle.Radio => MenuItemToggleType.Radio,
                _ => MenuItemToggleType.None,
            },
        };
        if (!e.IsEnabled)
        {
            item.IsEnabled = false;
        }

        if (e.HeaderBinding is CompiledBinding header)
        {
            item.Bind(NativeMenuItem.HeaderProperty, new CompiledBinding(header.Path!)
            {
                Source = header.Source,
                Mode = BindingMode.OneWay,
                Converter = new FuncValueConverter<string?, string?>(h => h is null ? null : WithoutAccessKey(h)),
            });
        }
        else
        {
            Bind(item, NativeMenuItem.HeaderProperty, e.HeaderBinding);
        }

        Bind(item, NativeMenuItem.IsCheckedProperty, e.IsChecked);
        Bind(item, NativeMenuItem.IsVisibleProperty, e.IsVisible);
        Bind(item, NativeMenuItem.IsEnabledProperty, e.IsEnabledBinding);
        if (e.Items.Count > 0)
        {
            item.Menu = Children(e.Items, shared);
        }

        return item;
    }

    private static NativeMenu Children(IReadOnlyList<MenuEntry> entries, HashSet<KeyGesture> shared)
    {
        var menu = new NativeMenu();
        var kept = entries.Where(c => c is not MenuItemEntry { Role: not MenuRole.None }).ToList();
        while (kept.Count > 0 && kept[^1] is MenuSeparator)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        foreach (MenuEntry child in kept)
        {
            switch (child)
            {
                case MenuSeparator:
                    menu.Add(new NativeMenuItemSeparator());
                    break;
                case MenuDynamic dynamic:
                    AddDynamic(menu, dynamic, shared);
                    break;
                case MenuItemEntry item:
                    menu.Add(Item(item, shared));
                    break;
            }
        }

        return menu;
    }

    private static void AddDynamic(NativeMenu menu, MenuDynamic dynamic, HashSet<KeyGesture> shared)
    {
        NativeMenuItemBase? before = menu.Items.Count == 0 ? null : menu.Items[^1];
        List<NativeMenuItemBase> made = [];

        void Fill()
        {
            foreach (NativeMenuItemBase old in made)
            {
                menu.Items.Remove(old);
            }

            made.Clear();
            int at = before is null ? 0 : menu.Items.IndexOf(before) + 1;
            foreach (MenuEntry entry in dynamic.Items())
            {
                NativeMenuItemBase item = entry is MenuItemEntry e ? Item(e, shared) : new NativeMenuItemSeparator();
                menu.Items.Insert(at++, item);
                made.Add(item);
            }
        }

        Fill();
        menu.Opening += (_, _) => Fill();
        if (dynamic.Changed is { } changed)
        {
            changed.CollectionChanged += (_, _) => Fill();
        }
    }

    private static IEnumerable<MenuItemEntry> All(IEnumerable<MenuEntry> entries) =>
        entries.OfType<MenuItemEntry>().SelectMany(e => All(e.Items).Prepend(e));

    private static NativeMenuItem? QuitItem() =>
        Application.Current is { } app && NativeMenu.GetMenu(app) is { } menu
            ? menu.Items.OfType<NativeMenuItem>().FirstOrDefault(i => Equals(i.Gesture, Reserved[0]))
            : null;

    private static void Bind(NativeMenuItem item, AvaloniaProperty property, BindingBase? binding)
    {
        if (binding is not null)
        {
            item.Bind(property, binding);
        }
    }
}
