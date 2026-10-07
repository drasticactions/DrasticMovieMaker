namespace AvaMovieMaker.Effects.Titles;

public sealed record TitleContent
{
    public TitlePlacement Placement { get; init; } = TitlePlacement.AtBeginning;

    public string AnimationId { get; init => field = value ?? Catalog.TitleAnimationCatalog.DefaultTitle; } = Catalog.TitleAnimationCatalog.DefaultTitle;

    public IReadOnlyList<string> Lines { get; init => field = value ?? []; } = [];

    public IReadOnlyList<CreditRow> Credits { get; init => field = value ?? []; } = [];

    public TitleFont Font { get; init => field = value ?? new(); } = new();

    public uint TextColor { get; init; } = 0xFFF0F0F0;

    public int Transparency { get; init; }

    public TitleAlignment Alignment { get; init; } = TitleAlignment.Center;

    public uint BackgroundColor { get; init; } = 0xFF416FA6;

    public uint? BannerColor { get; init; }

    public bool IsFullFrame => Placement != TitlePlacement.OnClip;

    public string Summary =>
        Lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
        ?? Credits.Select(c => string.IsNullOrWhiteSpace(c.Heading) ? c.Names : c.Heading).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
        ?? string.Empty;
}
