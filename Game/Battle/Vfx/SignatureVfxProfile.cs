namespace DraconicWars.Game.Battle.Vfx;

using Godot;

/// <summary>
/// Per-element signature-VFX config — one <c>.tres</c> per element, loaded by
/// <see cref="SignatureVfxProfiles"/>. Holds palette-anchored emissive colors plus the
/// aura/light data; <see cref="SignatureVfxResolver"/> scales the scalars by unit tier.
/// Config-only: zero per-unit state (the per-unit runtime is the nodes UnitView attaches).
/// </summary>
[GlobalClass, Tool]
public partial class SignatureVfxProfile : Resource
{
    [Export] public AuraKind Aura { get; set; } = AuraKind.Embers;
    [Export] public Color[] EmissiveColors { get; set; } = System.Array.Empty<Color>();
    [Export] public Color LightColor { get; set; } = Colors.White;
    [Export] public float BaseLightEnergy { get; set; } = 0.85f;
    [Export] public float BaseAuraDensity { get; set; } = 6f;
    [Export] public float BaseEmissiveBoost { get; set; } = 1.5f;
    [Export] public float PerTierGain { get; set; } = 0.25f;
    [Export] public int MinTierForLight { get; set; } = 3;

    public SignatureVfxResolver.ResolverConfig ToResolverConfig()
    {
        return new SignatureVfxResolver.ResolverConfig(
            this.BaseLightEnergy, this.BaseAuraDensity, this.BaseEmissiveBoost,
            this.PerTierGain, this.MinTierForLight);
    }
}
