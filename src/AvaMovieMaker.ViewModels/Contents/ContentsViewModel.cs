using System.Collections.ObjectModel;
using AvaMovieMaker.Effects.Catalog;
using AvaMovieMaker.Media;
using AvaMovieMaker.Timeline.Model;
using AvaMovieMaker.ViewModels.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaMovieMaker.ViewModels.Contents;

public sealed partial class ContentsViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly ThumbnailService _thumbnails;

    public ContentsViewModel(ProjectSession session, ThumbnailService thumbnails)
    {
        _session = session;
        _thumbnails = thumbnails;
        Effects = [.. EffectCatalog.All.OrderBy(e => e.Name, StringComparer.CurrentCulture).Select(e => new CatalogItemViewModel(e.Id, e.Name, false))];
        Transitions = [.. TransitionCatalog.All.OrderBy(t => t.Name, StringComparer.CurrentCulture).Select(t => new CatalogItemViewModel(t.Id, t.Name, true))];
        session.Changed += (_, _) => Refresh();
        session.Replaced += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<ContentItemViewModel> Items { get; } = [];

    public IReadOnlyList<CatalogItemViewModel> Effects { get; }

    public IReadOnlyList<CatalogItemViewModel> Transitions { get; }

    public ObservableCollection<ContentItemViewModel> SelectedItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(IsMediaView), nameof(CatalogItems), nameof(ShowThumbnails), nameof(ShowDetails), nameof(ViewIndex))]
    public partial ContentsView View { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowThumbnails), nameof(ShowDetails))]
    public partial bool IsDetails { get; set; }

    public bool ShowThumbnails => IsMediaView && !IsDetails;

    public bool ShowDetails => IsMediaView && IsDetails;

    public int ViewIndex
    {
        get => (int)View;
        set => View = (ContentsView)Math.Clamp(value, 0, 2);
    }

    [ObservableProperty]
    public partial ContentsSort Sort { get; set; }

    [ObservableProperty]
    public partial CatalogItemViewModel? SelectedCatalogItem { get; set; }

    public int ThumbnailWidth { get; private set; } = 91;

    public int ThumbnailHeight { get; private set; } = 68;

    public void SetThumbnailSize(int width, int height)
    {
        if (width == ThumbnailWidth && height == ThumbnailHeight)
        {
            return;
        }

        ThumbnailWidth = width;
        ThumbnailHeight = height;
        foreach (ContentItemViewModel vm in Items)
        {
            _ = vm.LoadThumbnailAsync(_thumbnails, width, height, reload: true);
        }
    }

    public bool IsMediaView => View == ContentsView.ImportedMedia;

    public IReadOnlyList<CatalogItemViewModel> CatalogItems => View == ContentsView.Transitions ? Transitions : Effects;

    public string Title => View switch
    {
        ContentsView.Effects => Strings.TaskEffects,
        ContentsView.Transitions => Strings.TaskTransitions,
        _ => Strings.ImportedMedia,
    };

    public event EventHandler<ContentItemViewModel>? PreviewRequested;

    public event EventHandler<CatalogItemViewModel>? CatalogPreviewRequested;

    public event EventHandler<ContentItemViewModel>? RevealRequested;

    partial void OnSortChanged(ContentsSort value) => Refresh();

    public void MarkActive()
    {
        _session.ActiveSelection = SelectionKind.Contents;
        Activated?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Activated;

    public void Refresh()
    {
        var old = Items.ToDictionary(i => i.Key);
        var selectedKeys = SelectedItems.Select(s => s.Key).ToHashSet();
        var next = new List<ContentItemViewModel>();
        foreach (MediaItem m in _session.Project.Media)
        {
            IEnumerable<SourceClip?> clips = m.Clips.Count == 0 ? new SourceClip?[] { null } : m.Clips.Cast<SourceClip?>();
            foreach (SourceClip? c in clips)
            {
                string key = $"{m.Id}/{c?.Id}";
                next.Add(old.TryGetValue(key, out ContentItemViewModel? vm) && vm.Media == m && vm.Clip == c ? vm : new ContentItemViewModel(m, c));
            }
        }

        next = Sorted(next).ToList();
        if (next.SequenceEqual(Items))
        {
            return;
        }

        Items.Clear();
        foreach (ContentItemViewModel vm in next)
        {
            Items.Add(vm);
            _ = vm.LoadThumbnailAsync(_thumbnails, ThumbnailWidth, ThumbnailHeight);
        }

        SelectedItems.Clear();
        foreach (ContentItemViewModel vm in next.Where(v => selectedKeys.Contains(v.Key)))
        {
            SelectedItems.Add(vm);
        }
    }

    private IEnumerable<ContentItemViewModel> Sorted(List<ContentItemViewModel> items) => Sort switch
    {
        ContentsSort.ClipName => items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
        ContentsSort.Duration => items.OrderBy(i => i.Duration),
        ContentsSort.StartTime => items.OrderBy(i => i.Clip?.Start ?? default),
        ContentsSort.EndTime => items.OrderBy(i => i.Clip?.End ?? default),
        ContentsSort.Dimensions => items.OrderBy(i => (i.Media.Video?.Width ?? 0) * (i.Media.Video?.Height ?? 0)),
        ContentsSort.DateTaken => items.OrderBy(i => i.Media.DateTaken ?? DateTimeOffset.MaxValue),
        ContentsSort.FileName => items.OrderBy(i => i.FileName, StringComparer.CurrentCultureIgnoreCase),
        _ => items.OrderBy(i => i.Media.ImportBatch).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
    };

    public IReadOnlyList<(MediaItem Media, SourceClip? Clip)> Selection =>
        Items.Where(SelectedItems.Contains).Select(i => (i.Media, i.Clip)).ToList();

    public bool HasSelection => SelectedItems.Count > 0;

    public void Select(IEnumerable<ContentItemViewModel> items)
    {
        SelectedItems.Clear();
        foreach (ContentItemViewModel i in items)
        {
            SelectedItems.Add(i);
        }

        _session.ActiveSelection = SelectedItems.Count > 0 ? SelectionKind.Contents : _session.ActiveSelection;
        if (SelectedItems.Count > 0)
        {
            ItemSelected?.Invoke(this, SelectedItems[^1]);
        }
    }

    public void Reveal(ContentItemViewModel item)
    {
        Select([item]);
        RevealRequested?.Invoke(this, item);
    }

    public event EventHandler<ContentItemViewModel>? ItemSelected;

    [RelayCommand]
    private void Preview(ContentItemViewModel? item)
    {
        item ??= SelectedItems.LastOrDefault();
        if (item is not null)
        {
            PreviewRequested?.Invoke(this, item);
        }
    }

    [RelayCommand]
    private void PreviewCatalog(CatalogItemViewModel? item)
    {
        item ??= SelectedCatalogItem;
        if (item is not null)
        {
            CatalogPreviewRequested?.Invoke(this, item);
        }
    }

    [RelayCommand]
    public void AddToTimeline()
    {
        var sel = Selection.Where(s => !s.Media.Missing).ToList();
        if (sel.Count > 0)
        {
            _session.Editor.AddToTimeline(sel);
        }
    }

    [RelayCommand]
    public void RemoveSelected()
    {
        foreach (var group in Selection.GroupBy(s => s.Media.Id))
        {
            MediaItem m = group.First().Media;
            var clipIds = group.Where(g => g.Clip is not null).Select(g => g.Clip!.Id).ToList();
            if (clipIds.Count == 0 || clipIds.Count == m.Clips.Count)
            {
                _session.Editor.RemoveMedia([m.Id]);
            }
            else
            {
                _session.Editor.RemoveSourceClips(m.Id, clipIds);
            }
        }
    }

    [RelayCommand]
    public void BeginRename()
    {
        if (SelectedItems.FirstOrDefault() is { } item)
        {
            item.EditName = item.Name;
            item.IsRenaming = true;
        }
    }

    public void CommitRename(ContentItemViewModel item)
    {
        item.IsRenaming = false;
        if (!string.IsNullOrWhiteSpace(item.EditName) && item.EditName != item.Name)
        {
            _session.Editor.RenameMedia(item.Media.Id, item.Clip?.Id ?? Guid.Empty, item.EditName);
        }
    }

    public bool CombineSelected()
    {
        var sel = Selection.Where(s => s.Clip is not null).ToList();
        if (sel.Count < 2 || sel.Select(s => s.Media.Id).Distinct().Count() != 1)
        {
            return false;
        }

        return _session.Editor.CombineSourceClips(sel[0].Media.Id, sel.Select(s => s.Clip!.Id).ToList());
    }

    public void SelectAll() => Select(Items);

    public bool SelectionHasVideo => Selection.Any(s => s.Media.Kind == MediaKind.Video);
}
