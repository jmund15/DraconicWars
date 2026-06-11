namespace DraconicWars.Sim.Augments;

using DraconicWars.Sim.Units;

public enum AugmentTier
{
    Silver,
    Gold,
    Prismatic,
}

public enum AugmentCategory
{
    Economy,
    Combat,
    Breath,
    Deployment,
}

/// <summary>
/// One draftable augment. Effects are typed per-pick fields summed into PlayerBuffs —
/// the same data-not-switches shape as ConduitDef. RelevantElement feeds offer
/// tailoring (fielded-element offers are preferred).
/// </summary>
public sealed record AugmentDef(
    string Id,
    string DisplayName,
    AugmentTier Tier,
    AugmentCategory Category,
    Element? RelevantElement = null,
    float DripBonusPerSecond = 0f,
    float WalletCapBonus = 0f,
    float KillBountyPct = 0f,
    float DamagePct = 0f,
    float SpeedPct = 0f,
    float BreathRegenPct = 0f,
    float BreathDamagePct = 0f,
    float DeployCooldownPct = 0f,
    float AscensionTricklePct = 0f,
    float SummonCostPct = 0f,
    float WrathCooldownPct = 0f);
