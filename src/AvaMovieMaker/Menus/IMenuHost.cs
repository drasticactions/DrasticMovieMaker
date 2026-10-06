using Avalonia.Controls;

namespace AvaMovieMaker.Menus;

public interface IMenuHost
{
    void Show(Menu inWindow, IReadOnlyList<MenuItemEntry> menus);
}
