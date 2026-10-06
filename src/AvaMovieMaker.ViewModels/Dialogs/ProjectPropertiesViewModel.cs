using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaMovieMaker.ViewModels.Dialogs;

public sealed partial class ProjectPropertiesViewModel : ObservableObject
{
    public const int TitleMaxLength = 128;
    public const int AuthorMaxLength = 128;
    public const int RatingMaxLength = 20;
    public const int CommentsMaxLength = 512;

    private readonly string _shownTitle;

    public ProjectPropertiesViewModel(ProjectProperties p, MediaTime duration, string defaultAuthor, string? projectName = null)
    {
        _shownTitle = string.IsNullOrEmpty(p.Title) ? Cut(projectName ?? string.Empty, TitleMaxLength) : string.Empty;
        Title = string.IsNullOrEmpty(p.Title) ? _shownTitle : Cut(p.Title, TitleMaxLength);
        Author = Cut(string.IsNullOrEmpty(p.Author) ? defaultAuthor : p.Author, AuthorMaxLength);
        Copyright = p.Copyright;
        Rating = Cut(p.Rating, RatingMaxLength);
        Description = Cut(p.Description, CommentsMaxLength);
        TotalTime = TimeFormat.Format(duration);
    }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Author { get; set; }

    [ObservableProperty]
    public partial string Copyright { get; set; }

    [ObservableProperty]
    public partial string Rating { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    public string TotalTime { get; }

    public event EventHandler? ApplyRequested;

    public void Apply() => ApplyRequested?.Invoke(this, EventArgs.Empty);

    public ProjectProperties ToProperties() => new()
    {
        Title = _shownTitle.Length > 0 && Title == _shownTitle ? string.Empty : Cut(Title, TitleMaxLength),
        Author = Cut(Author, AuthorMaxLength),
        Copyright = Copyright,
        Rating = Cut(Rating, RatingMaxLength),
        Description = Cut(Description, CommentsMaxLength),
    };

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}
