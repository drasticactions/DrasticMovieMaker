namespace AvaMovieMaker.Effects.Catalog;

public sealed record EffectInfo
{
    public required string Id { get; init; }

    public required string NameKey { get; init; }

    public string Name => Strings.ResourceManager.GetString(NameKey, Strings.Culture) ?? NameKey;

    public string NeutralName => Strings.ResourceManager.GetString(NameKey, System.Globalization.CultureInfo.InvariantCulture) ?? NameKey;

    // Empty for effects the legacy project format has no equivalent for.
    public string MswmmId { get; init; } = string.Empty;

    public required EffectFamily Family { get; init; }

    public required string Operation { get; init; }

    public float[] Values { get; init; } = [];

    public double Speed { get; init; } = 1.0;
}
