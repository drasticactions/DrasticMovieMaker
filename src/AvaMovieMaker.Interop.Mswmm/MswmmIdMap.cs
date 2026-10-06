using AvaMovieMaker.Effects.Catalog;

namespace AvaMovieMaker.Interop.Mswmm;

public static class MswmmIdMap
{
    public static string? Transition(string mswmmId) => TransitionCatalog.FindMswmmId(mswmmId)?.Id;

    public static string? Effect(string mswmmId) => EffectCatalog.FindMswmmId(mswmmId)?.Id;

    public static string? TitleAnimation(string mswmmId) => TitleAnimationCatalog.FindMswmmId(mswmmId)?.Id;

    public static string? TransitionByName(string name) =>
        name.Length == 0 ? null : TransitionCatalog.All.FirstOrDefault(t => string.Equals(t.NeutralName, name.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

    public static string? EffectByName(string name) =>
        name.Length == 0 ? null : EffectCatalog.All.FirstOrDefault(e => string.Equals(e.NeutralName, name.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
}
