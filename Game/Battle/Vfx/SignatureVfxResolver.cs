namespace DraconicWars.Game.Battle.Vfx;

/// <summary>
/// Pure tier-scaling for the per-element signature VFX layer. No Godot types: the view
/// layer feeds base values from a loaded profile and applies the scaled result. Intensity
/// rises continuously with tier, so the top tier (dragons) is loudest by construction —
/// no "is-dragon" branch.
/// </summary>
public static class SignatureVfxResolver
{
    public readonly record struct ResolverConfig(
        float BaseLightEnergy,
        float BaseAuraDensity,
        float BaseEmissiveBoost,
        float PerTierGain,
        int MinTierForLight);

    public readonly record struct ResolvedVfx(
        float LightEnergy,
        float AuraDensity,
        float EmissiveBoost,
        bool SpawnLight);

    public static ResolvedVfx Resolve(int tier, ResolverConfig cfg)
    {
        var clampedTier = tier < 1 ? 1 : tier;
        var multiplier = 1f + (clampedTier - 1) * cfg.PerTierGain;
        return new ResolvedVfx(
            LightEnergy: cfg.BaseLightEnergy * multiplier,
            AuraDensity: cfg.BaseAuraDensity * multiplier,
            EmissiveBoost: cfg.BaseEmissiveBoost * multiplier,
            SpawnLight: tier >= cfg.MinTierForLight);
    }
}
