namespace DraconicWars.Sim.Conduits;

using System;

/// <summary>
/// A buildable Dragonspire structure. Effects are typed per-tier fields summed by
/// tier — data, not behavior switches — so new conduits are catalog rows.
/// IsArmament rows mount on the spire's single Crownmount instead of a utility
/// socket and auto-fire per their turret fields (deterministic, cadence-gated);
/// mounting one silences the innate Dragon's Breath until it is sold.
/// </summary>
public sealed record ConduitDef(
    string Id,
    string DisplayName,
    int BaseCost,
    float DripBonusPerTier = 0f,
    float WalletCapPerTier = 0f,
    float KillBountyPctPerTier = 0f,
    float DamagePctPerTier = 0f,
    float SpeedPctPerTier = 0f,
    float SpireShieldPerTier = 0f,
    float SlowAuraPctPerTier = 0f,
    float BreathRegenPctPerTier = 0f,
    float TurretCadencePctPerTier = 0f,
    bool IsArmament = false,
    bool TargetsAir = false,
    bool TargetsGround = false,
    float TurretRange = 0f,
    float TurretRangeMin = 0f,
    int TurretDamagePerTier = 0,
    int TurretCadenceTicks = 0,
    float TurretAoeRadius = 0f,
    float OnHitSlowPct = 0f,
    int OnHitSlowTicks = 0)
{
    public const int MaxTier = 3;
    public const float CostScalePerTier = 1.6f;

    public int CostForTier(int tier)
    {
        return (int)(BaseCost * MathF.Pow(CostScalePerTier, tier - 1));
    }
}
