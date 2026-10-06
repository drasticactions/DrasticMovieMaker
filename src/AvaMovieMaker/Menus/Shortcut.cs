using Avalonia.Input;

namespace AvaMovieMaker.Menus;

public readonly record struct Shortcut(Key Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    public static Shortcut Parse(string text)
    {
        KeyGesture g = KeyGesture.Parse(text);
        return new Shortcut(g.Key, g.KeyModifiers);
    }

    public KeyGesture Gesture => new(Key, CommandKeys.ToPlatform(Modifiers));

    public bool Matches(Key key, KeyModifiers pressed) => key == Key && CommandKeys.ToLogical(pressed) == Modifiers;
}
