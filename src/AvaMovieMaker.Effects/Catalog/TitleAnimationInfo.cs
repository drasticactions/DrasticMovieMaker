namespace AvaMovieMaker.Effects.Catalog;

public sealed record TitleAnimationInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string MswmmId { get; init; }

    public required TitleGroup Group { get; init; }

    public required string Kind { get; init; }

    public required string Description { get; init; }

    public double Entrance { get; init; } = 1.0;

    public double Exit { get; init; } = 1.0;

    public double DurationMultiplier { get; init; } = 1.0;

    public bool TwoLines => Group != TitleGroup.OneLine;

    public bool IsCredits => Group == TitleGroup.Credits;

    public float[]? VideoRect { get; init; }

    public bool Shadow { get; init; } = true;

    public bool Outline { get; init; }

    public int MaxCharacters { get; init; }
}
