namespace AvaMovieMaker.Effects.Catalog;

public sealed record TransitionInfo
{
    public required string Id { get; init; }

    public required string NameKey { get; init; }

    public string Name => Strings.ResourceManager.GetString(NameKey, Strings.Culture) ?? NameKey;

    public string NeutralName => Strings.ResourceManager.GetString(NameKey, System.Globalization.CultureInfo.InvariantCulture) ?? NameKey;

    public required string MswmmId { get; init; }

    public required TransitionFamily Family { get; init; }

    public WipeShape Shape { get; init; }

    public float Softness { get; init; }

    public bool Reverse { get; init; }

    public bool FlipX { get; init; }

    public bool FlipY { get; init; }

    public int Count { get; init; }

    public int Columns { get; init; }

    public int Rows { get; init; }

    public string Variant { get; init; } = string.Empty;

    public float MaxBlock { get; init; }

    public int Particles { get; init; }
}
