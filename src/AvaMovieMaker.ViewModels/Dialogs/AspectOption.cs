using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed record AspectOption(AspectRatio Value, string Name)
{
    public static IReadOnlyList<AspectOption> All { get; } = [.. AspectRatios.All.Select(a => new AspectOption(a, NameOf(a)))];

    public static AspectOption For(AspectRatio aspect) => All.FirstOrDefault(o => o.Value == aspect) ?? All[0];

    public static string NameOf(AspectRatio aspect) => aspect switch
    {
        AspectRatio.Widescreen16x9 => Strings.AspectWidescreen,
        AspectRatio.Vertical9x16 => Strings.AspectVertical,
        AspectRatio.Square1x1 => Strings.AspectSquare,
        AspectRatio.Portrait4x5 => Strings.AspectPortrait,
        _ => Strings.AspectStandard,
    };

    public override string ToString() => Name;
}
