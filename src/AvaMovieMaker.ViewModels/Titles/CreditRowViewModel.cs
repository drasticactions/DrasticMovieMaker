using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaMovieMaker.ViewModels.Titles;

public sealed partial class CreditRowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Heading { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Names { get; set; } = string.Empty;
}
