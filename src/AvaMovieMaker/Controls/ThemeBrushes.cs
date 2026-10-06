using Avalonia.Controls;
using Avalonia.Media;

namespace AvaMovieMaker.Controls;

internal static class ThemeBrushes
{
    public static IBrush Brush(this Control control, string key, IBrush fallback) =>
        control.TryFindResource(key, control.ActualThemeVariant, out object? value) && value is IBrush b ? b : fallback;
}
