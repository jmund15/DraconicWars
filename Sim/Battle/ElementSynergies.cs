namespace DraconicWars.Sim.Battle;

using System.Collections.Generic;
using DraconicWars.Sim.Units;

/// <summary>
/// Element synergy thresholds (design.md §5): counting DISTINCT fielded unit types per
/// element; thresholds at 2 and 4 distinct types → tier 1 and 2. Effects are flat v1
/// placeholders pending the status-effect system (Burn/Freeze procs).
/// </summary>
public static class ElementSynergies
{
    public const int ThresholdTierOne = 2;
    public const int ThresholdTierTwo = 4;

    public static readonly float[] FireDamagePct = { 0f, 0.10f, 0.25f };
    public static readonly float[] StormAttackSpeedPct = { 0f, 0.12f, 0.30f };
    public static readonly float[] VenomExecutePct = { 0f, 0.10f, 0.25f };
    public static readonly float[] StoneDamageReductionPct = { 0f, 0.10f, 0.25f };
    public static readonly float[] FrostSlowPct = { 0f, 0.15f, 0.30f };
    public const int FrostSlowTicks = 30;
    public const float VenomExecuteHpThreshold = 0.5f;

    /// <summary>0 = inactive, 1 = threshold 2 reached, 2 = threshold 4 reached.</summary>
    public static int TierFor(BattleState state, PlayerSide side, Element element)
    {
        var distinct = new HashSet<string>();
        foreach (var unit in state.Units)
        {
            if (unit.Side == side && unit.IsAlive && unit.Def.Element == element)
            {
                distinct.Add(unit.Def.Id);
            }
        }
        if (distinct.Count >= ThresholdTierTwo)
        {
            return 2;
        }
        return distinct.Count >= ThresholdTierOne ? 1 : 0;
    }
}
