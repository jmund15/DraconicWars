namespace DraconicWars.Sim.Augments;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Units;

/// <summary>V1 augment pool (design.md §9). Balance values provisional.</summary>
public static class AugmentCatalog
{
    public static readonly IReadOnlyList<AugmentDef> All = new[]
    {
        // Silver
        new AugmentDef("ley_tap", "Ley Tap", AugmentTier.Silver, AugmentCategory.Economy,
            DripBonusPerSecond: 3f),
        new AugmentDef("deep_pockets", "Deep Pockets", AugmentTier.Silver, AugmentCategory.Economy,
            WalletCapBonus: 150f),
        new AugmentDef("quick_quarters", "Quick Quarters", AugmentTier.Silver, AugmentCategory.Deployment,
            DeployCooldownPct: 0.15f),
        new AugmentDef("ember_gland", "Ember Gland", AugmentTier.Silver, AugmentCategory.Breath,
            RelevantElement: Element.Fire, BreathDamagePct: 0.25f),
        new AugmentDef("tempered_scales", "Tempered Scales", AugmentTier.Silver, AugmentCategory.Combat,
            RelevantElement: Element.Stone, DamagePct: 0.08f),
        new AugmentDef("fleet_footed", "Fleet Footed", AugmentTier.Silver, AugmentCategory.Combat,
            RelevantElement: Element.Storm, SpeedPct: 0.08f),

        // Gold
        new AugmentDef("ley_geyser", "Ley Geyser", AugmentTier.Gold, AugmentCategory.Economy,
            DripBonusPerSecond: 6f),
        new AugmentDef("bounty_hunters", "Bounty Hunters", AugmentTier.Gold, AugmentCategory.Economy,
            KillBountyPct: 0.4f),
        new AugmentDef("war_chant", "War Chant", AugmentTier.Gold, AugmentCategory.Combat,
            RelevantElement: Element.Fire, DamagePct: 0.15f),
        new AugmentDef("gale_wings", "Gale Wings", AugmentTier.Gold, AugmentCategory.Combat,
            RelevantElement: Element.Storm, SpeedPct: 0.15f),
        new AugmentDef("dragon_lungs", "Dragon Lungs", AugmentTier.Gold, AugmentCategory.Breath,
            BreathRegenPct: 0.5f),
        new AugmentDef("drillmaster", "Drillmaster", AugmentTier.Gold, AugmentCategory.Deployment,
            DeployCooldownPct: 0.3f),

        // Prismatic
        new AugmentDef("avatar_of_war", "Avatar of War", AugmentTier.Prismatic, AugmentCategory.Combat,
            DamagePct: 0.25f, SpeedPct: 0.1f),
        new AugmentDef("ley_singularity", "Ley Singularity", AugmentTier.Prismatic, AugmentCategory.Economy,
            DripBonusPerSecond: 12f),
        new AugmentDef("imminent_wings", "Imminent Wings", AugmentTier.Prismatic, AugmentCategory.Deployment,
            SummonCostPct: 0.35f, AscensionTricklePct: 0.15f),
        new AugmentDef("living_tempest", "Living Tempest", AugmentTier.Prismatic, AugmentCategory.Breath,
            RelevantElement: Element.Storm, BreathDamagePct: 0.4f, BreathRegenPct: 0.5f, WrathCooldownPct: 0.25f),
    };

    private static readonly Dictionary<string, AugmentDef> Index = All.ToDictionary(def => def.Id);

    public static AugmentDef ById(string id)
    {
        return Index[id];
    }
}
