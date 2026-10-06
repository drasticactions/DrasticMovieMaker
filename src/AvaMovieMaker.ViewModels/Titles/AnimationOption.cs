using AvaMovieMaker.Effects.Catalog;

namespace AvaMovieMaker.ViewModels.Titles;

public sealed record AnimationOption(string Id, string Name, string Description, TitleGroup Group)
{
    public string GroupName => Group switch
    {
        TitleGroup.OneLine => Strings.TitleGroupOneLine,
        TitleGroup.TwoLines => Strings.TitleGroupTwoLines,
        _ => Strings.TitleGroupCredits,
    };
}
