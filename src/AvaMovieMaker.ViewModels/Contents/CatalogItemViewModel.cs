namespace AvaMovieMaker.ViewModels.Contents;

public sealed record CatalogItemViewModel(string Id, string Name, bool IsTransition)
{
    public string ToolTip => Name;
}
