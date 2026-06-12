namespace DraconicWars.Sim.Pacts;

using DraconicWars.Sim.Units;

/// <summary>
/// Pact magnitude rungs (design.md §9). Ember pacts are minor and price-free; Drake
/// pacts are the strong pure-upside ceiling; Wyrm pacts are mythic and ALWAYS carry
/// a visible Price — dragons do not give their deepest power away.
/// </summary>
public enum PactTier
{
    Ember,
    Drake,
    Wyrm,
}

public enum PactCategory
{
    Economy,
    Combat,
    Breath,
    Deployment,
}

/// <summary>
/// One of the Broker's terms. Effects are typed per-pick fields summed into
/// PlayerBuffs — the same data-not-switches shape as ConduitDef. RelevantElement
/// feeds offer tailoring (fielded-element offers are preferred). Prices:
/// PriceSpireHpPct is a one-time blood price taken from the sealing side's spire
/// (clamped so it cannot kill); PriceDripPerSecond is an ongoing mana tithe folded
/// into the drip (floored at BattleConfig.DripFloorPerSecond).
/// </summary>
public sealed record PactDef(
    string Id,
    string DisplayName,
    PactTier Tier,
    PactCategory Category,
    string Lore = "",
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
    float WrathCooldownPct = 0f,
    float ConduitRefundBonusPct = 0f,
    float PriceSpireHpPct = 0f,
    float PriceDripPerSecond = 0f);
