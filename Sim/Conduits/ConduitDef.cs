namespace DraconicWars.Sim.Conduits;

using System;

/// <summary>
/// A buildable Dragonspire structure. Effects are typed per-tier fields summed by
/// tier — data, not behavior switches — so new conduits are catalog rows.
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
    float BreathRegenPctPerTier = 0f)
{
    public const int MaxTier = 3;
    public const float CostScalePerTier = 1.6f;

    public int CostForTier(int tier)
    {
        return (int)(BaseCost * MathF.Pow(CostScalePerTier, tier - 1));
    }
}
