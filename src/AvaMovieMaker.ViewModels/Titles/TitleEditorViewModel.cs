using System.Collections.ObjectModel;
using System.ComponentModel;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Effects.Titles;
using AvaMovieMaker.Time;
using AvaMovieMaker.Timeline.Editing;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.Timeline.Planning;
using AvaMovieMaker.ViewModels.Preview;
using AvaMovieMaker.ViewModels.Services;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Titles;

public sealed partial class TitleEditorViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly MonitorViewModel _monitor;
    private readonly IDialogService _dialogs;
    private Guid? _editing;
    private Guid? _targetClip;

    public TitleEditorViewModel(ProjectSession session, MonitorViewModel monitor, IDialogService dialogs)
    {
        _session = session;
        _monitor = monitor;
        _dialogs = dialogs;
        Credits.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (CreditRowViewModel r in e.NewItems)
                {
                    r.PropertyChanged += OnContentChanged;
                }
            }

            AddCommand.NotifyCanExecuteChanged();
            UpdatePreview();
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(IsPlacementPage), nameof(IsTextPage), nameof(IsAnimationPage), nameof(IsFontPage))]
    public partial TitlePage Page { get; set; }

    public bool IsPlacementPage => Page == TitlePage.Placement;

    public bool IsTextPage => Page == TitlePage.Text;

    public bool IsAnimationPage => Page == TitlePage.Animation;

    public bool IsFontPage => Page == TitlePage.Font;

    public string AddText => IsEditing ? Strings.TitleDone : Strings.TitleAdd;

    public bool AlignLeft
    {
        get => Alignment == TitleAlignment.Left;
        set { if (value) Alignment = TitleAlignment.Left; }
    }

    public bool AlignCenter
    {
        get => Alignment == TitleAlignment.Center;
        set { if (value) Alignment = TitleAlignment.Center; }
    }

    public bool AlignRight
    {
        get => Alignment == TitleAlignment.Right;
        set { if (value) Alignment = TitleAlignment.Right; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCredits), nameof(Heading))]
    public partial TitlePlacement Placement { get; set; }

    [ObservableProperty]
    public partial string Line1 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Line2 { get; set; } = string.Empty;

    public ObservableCollection<CreditRowViewModel> Credits { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSecondLine))]
    public partial AnimationOption? Animation { get; set; }

    [ObservableProperty]
    public partial string FontFamily { get; set; } = TitleFonts.DefaultFamily;

    [ObservableProperty]
    public partial bool Bold { get; set; }

    [ObservableProperty]
    public partial bool Italic { get; set; }

    [ObservableProperty]
    public partial bool Underline { get; set; }

    [ObservableProperty]
    public partial int SizeStep { get; set; }

    [ObservableProperty]
    public partial uint TextColor { get; set; } = 0xFFF0F0F0;

    [ObservableProperty]
    public partial uint BackgroundColor { get; set; } = 0xFF416FA6;

    [ObservableProperty]
    public partial int Transparency { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlignLeft), nameof(AlignCenter), nameof(AlignRight))]
    public partial TitleAlignment Alignment { get; set; } = TitleAlignment.Center;

    public IReadOnlyList<string> Fonts { get; } = TitleFonts.Families;

    public IReadOnlyList<PlacementOption> Placements { get; private set; } = [];

    public IReadOnlyList<AnimationOption> Animations { get; private set; } = [];

    public IReadOnlyList<object> AnimationRows { get; private set; } = [];

    public bool IsCredits => Placement == TitlePlacement.CreditsAtEnd;

    public bool ShowSecondLine => Animation is { Group: TitleGroup.TwoLines };

    public bool IsFullFrame => Placement != TitlePlacement.OnClip;

    public bool IsEditing => _editing is not null;

    public string Heading => Page switch
    {
        TitlePage.Placement => Strings.TitleHeadingPlacement,
        TitlePage.Animation => Strings.TitleHeadingAnimation,
        TitlePage.Font => Strings.TitleHeadingFont,
        _ => Strings.EnterTitleText,
    };

    public event EventHandler? Closed;

    public event EventHandler? TimelineNeeded;

    public void Start(TitlePlacement? placement = null)
    {
        _editing = null;
        _targetClip = _session.SelectedVideo?.Id;
        bool hasClip = _targetClip is not null;
        Placements =
        [
            new(TitlePlacement.AtBeginning, Strings.PlacementAtBeginning, true),
            new(TitlePlacement.BeforeClip, Strings.PlacementBeforeClip, hasClip),
            new(TitlePlacement.OnClip, Strings.PlacementOnClip, hasClip),
            new(TitlePlacement.CreditsAtEnd, Strings.PlacementCreditsAtEnd, true),
        ];
        OnPropertyChanged(nameof(Placements));
        Line1 = string.Empty;
        Line2 = string.Empty;
        Credits.Clear();
        Load(new TitleContent());
        Page = TitlePage.Placement;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddText));
        if (placement is { } p)
        {
            Choose(p);
        }
    }

    public void Edit(Guid clipId)
    {
        TitleContent? content = _session.Project.VideoTrack.FirstOrDefault(c => c.Id == clipId)?.Title
            ?? _session.Project.TitleOverlayTrack.FirstOrDefault(t => t.Id == clipId)?.Content;
        if (content is null)
        {
            return;
        }

        _editing = clipId;
        Load(content);
        Page = TitlePage.Text;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddText));
        UpdatePreview();
    }

    private void Load(TitleContent c)
    {
        Placement = c.Placement;
        Line1 = c.Lines.Count > 0 ? c.Lines[0] : string.Empty;
        Line2 = c.Lines.Count > 1 ? c.Lines[1] : string.Empty;
        Credits.Clear();
        IEnumerable<CreditRow> rows = c.Credits;
        if (c.Placement == TitlePlacement.CreditsAtEnd && c.Credits.Count > 0 && c.Credits[0].Names.Length == 0)
        {
            Line1 = c.Credits[0].Heading;
            rows = c.Credits.Skip(1);
        }

        foreach (CreditRow r in rows)
        {
            Credits.Add(new CreditRowViewModel { Heading = r.Heading, Names = r.Names });
        }

        FontFamily = TitleFonts.DisplayFamily(c.Font.Family);
        Bold = c.Font.Bold;
        Italic = c.Font.Italic;
        Underline = c.Font.Underline;
        SizeStep = c.Font.SizeStep;
        TextColor = c.TextColor;
        BackgroundColor = c.BackgroundColor;
        Transparency = c.Transparency;
        Alignment = c.Alignment;
        BuildAnimations();
        Animation = Animations.FirstOrDefault(a => a.Id == c.AnimationId) ?? Animations.FirstOrDefault();
    }

    private void BuildAnimations()
    {
        IEnumerable<TitleAnimationInfo> list = TitleAnimationCatalog.All.Where(a => IsCredits ? a.IsCredits : !a.IsCredits);
        Animations = list.OrderBy(a => a.Group).ThenBy(TitleAnimationCatalog.ListOrder)
            .Select(a => new AnimationOption(a.Id, a.Name, a.Description, a.Group)).ToList();
        var rows = new List<object>();
        foreach (IGrouping<TitleGroup, AnimationOption> g in Animations.GroupBy(a => a.Group))
        {
            rows.Add(new AnimationGroupHeader(g.First().GroupName));
            rows.AddRange(g);
        }

        AnimationRows = rows;
        OnPropertyChanged(nameof(Animations));
        OnPropertyChanged(nameof(AnimationRows));
    }

    [RelayCommand]
    private void Choose(TitlePlacement placement)
    {
        Placement = placement;
        BuildAnimations();
        string want = IsCredits ? TitleAnimationCatalog.DefaultCredits : TitleAnimationCatalog.DefaultTitle;
        Animation = Animations.FirstOrDefault(a => a.Id == want) ?? Animations.FirstOrDefault();
        if (IsCredits && Credits.Count == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                Credits.Add(new CreditRowViewModel());
            }
        }

        Page = TitlePage.Text;
        UpdatePreview();
    }

    [RelayCommand]
    private void ShowAnimations() => Page = TitlePage.Animation;

    [RelayCommand]
    private void ShowFonts() => Page = TitlePage.Font;

    [RelayCommand]
    private void ShowText() => Page = TitlePage.Text;

    [RelayCommand]
    private void IncreaseSize() => SizeStep = Math.Min(10, SizeStep + 1);

    [RelayCommand]
    private void DecreaseSize() => SizeStep = Math.Max(-6, SizeStep - 1);

    [RelayCommand]
    private void AddCreditRow() => Credits.Add(new CreditRowViewModel());

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PickTextColorAsync()
    {
        uint original = TextColor;
        TextColor = await _dialogs.PickColorAsync(original, c => TextColor = c) ?? original;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PickBackgroundColorAsync()
    {
        uint original = BackgroundColor;
        BackgroundColor = await _dialogs.PickColorAsync(original, c => BackgroundColor = c) ?? original;
    }

    public TitleContent ToContent() => new()
    {
        Placement = Placement,
        AnimationId = Animation?.Id ?? (IsCredits ? TitleAnimationCatalog.DefaultCredits : TitleAnimationCatalog.DefaultTitle),
        Lines = IsCredits ? [] : ShowSecondLine || !string.IsNullOrEmpty(Line2) ? [Line1, Line2] : [Line1],

        Credits = IsCredits
            ? (string.IsNullOrWhiteSpace(Line1) ? Enumerable.Empty<CreditRow>() : [new CreditRow(Line1, string.Empty)])
                .Concat(Credits.Where(r => !string.IsNullOrWhiteSpace(r.Heading) || !string.IsNullOrWhiteSpace(r.Names)).Select(r => new CreditRow(r.Heading, r.Names))).ToList()
            : [],
        Font = new TitleFont { Family = FontFamily, Bold = Bold, Italic = Italic, Underline = Underline, SizeStep = SizeStep },
        TextColor = TextColor,
        BackgroundColor = BackgroundColor,
        Transparency = Transparency,
        Alignment = Alignment,
    };

    public bool HasText => !string.IsNullOrWhiteSpace(Line1) || !string.IsNullOrWhiteSpace(Line2)
        || IsCredits && Credits.Any(r => !string.IsNullOrWhiteSpace(r.Heading) || !string.IsNullOrWhiteSpace(r.Names));

    [RelayCommand(CanExecute = nameof(HasText))]
    private void Add()
    {
        if (_editing is null && Placement == TitlePlacement.OnClip)
        {
            TimelineNeeded?.Invoke(this, EventArgs.Empty);
        }

        TitleContent content = ToContent();
        if (_editing is { } id)
        {
            _session.Editor.EditTitle(id, content);
        }
        else
        {
            _session.Editor.AddTitle(content, _targetClip, _monitor.ProjectPosition);
        }

        Close();
    }

    [RelayCommand]
    private void Cancel() => Close();

    private void Close()
    {
        _monitor.ShowProject();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnLine1Changed(string value)
    {
        AddCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }

    partial void OnLine2Changed(string value)
    {
        AddCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }

    partial void OnAnimationChanged(AnimationOption? value) => UpdatePreview();

    partial void OnFontFamilyChanged(string oldValue, string newValue)
    {
        if (newValue is null)
        {
            Restore(oldValue);
            return;
        }

        UpdatePreview();
    }

    private void Restore(string family)
    {
        if (SynchronizationContext.Current is { } ui)
        {
            ui.Post(_ => FontFamily = family, null);
        }
        else
        {
            FontFamily = family;
        }
    }

    partial void OnBoldChanged(bool value) => UpdatePreview();

    partial void OnItalicChanged(bool value) => UpdatePreview();

    partial void OnUnderlineChanged(bool value) => UpdatePreview();

    partial void OnSizeStepChanged(int value) => UpdatePreview();

    partial void OnTextColorChanged(uint value) => UpdatePreview();

    partial void OnBackgroundColorChanged(uint value) => UpdatePreview();

    partial void OnTransparencyChanged(int value) => UpdatePreview();

    partial void OnAlignmentChanged(TitleAlignment value) => UpdatePreview();

    private void OnContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        AddCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (Page == TitlePage.Placement)
        {
            return;
        }

        TitleContent content = ToContent();
        var scratch = new Project { Settings = _session.Project.Settings };
        var editor = new TimelineEditor(scratch, new Undo.UndoStack());
        MediaTime d = MediaTime.FromSeconds(TitleAnimationCatalog.DefaultDuration(content));
        if (content.Placement == TitlePlacement.OnClip && _targetClip is { } target && _session.Project.VideoTrack.FirstOrDefault(c => c.Id == target) is { } clip)
        {
            scratch.Media.AddRange(_session.Project.Media);
            scratch.VideoTrack.Add(clip with { TransitionIn = null });
            scratch.TitleOverlayTrack.Add(new TitleClip { Start = MediaTime.Zero, Duration = d, Content = content });
        }
        else
        {
            scratch.VideoTrack.Add(editor.NewTitleClip(content));
        }

        _monitor.ShowPlan(RenderPlanner.Build(scratch), Animation?.Name ?? string.Empty, play: true);
    }
}
