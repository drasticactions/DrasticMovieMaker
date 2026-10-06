using Avalonia;
using Avalonia.Input;

namespace AvaMovieMaker.Menus;

public static class CommandKeys
{
    private static KeyModifiers? _command;

    public static KeyModifiers Command
    {
        get => _command ??= Detect();
        set => _command = value;
    }

    public static KeyModifiers ToPlatform(KeyModifiers logical) => Swap(logical, Command);

    public static KeyModifiers ToLogical(KeyModifiers pressed) => Swap(pressed, Command);

    public static bool HasCommand(KeyModifiers pressed) => (pressed & Command) != 0;

    internal static KeyModifiers Swap(KeyModifiers m, KeyModifiers command)
    {
        if (command == KeyModifiers.Control)
        {
            return m;
        }

        bool control = (m & KeyModifiers.Control) != 0;
        bool other = (m & command) != 0;
        m &= ~(KeyModifiers.Control | command);
        return m | (control ? command : 0) | (other ? KeyModifiers.Control : 0);
    }

    private static KeyModifiers Detect()
    {
        try
        {
            if (Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers is { } m && m != KeyModifiers.None)
            {
                return m;
            }
        }
        catch (InvalidOperationException)
        {
        }

        return OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
    }
}
