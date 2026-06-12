namespace DraconicWars.Sim.Pacts;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Units;

/// <summary>
/// V1 pact pool (design.md §9) — the terms the Wyrmcourt's Broker may offer at a
/// parley. Ember terms are small and free; Drake terms are the pure-upside ceiling;
/// every Wyrm term carries a Price (blood from the spire, or an ongoing mana tithe).
/// Balance values provisional.
/// </summary>
public static class PactCatalog
{
    public static readonly IReadOnlyList<PactDef> All = new[]
    {
        // Ember — kindling terms, freely given
        new PactDef("ley_tap", "Ley Tap", PactTier.Ember, PactCategory.Economy,
            Lore: "A straw driven into the world's vein.",
            DripBonusPerSecond: 3f),
        new PactDef("deep_pockets", "Hoardkeeper's Purse", PactTier.Ember, PactCategory.Economy,
            Lore: "Sewn from wyrm-gullet leather. It does not tear.",
            WalletCapBonus: 150f),
        new PactDef("quick_quarters", "Mustering Bells", PactTier.Ember, PactCategory.Deployment,
            Lore: "The bells ring once; the warband is already moving.",
            DeployCooldownPct: 0.15f),
        new PactDef("ember_gland", "Ember Gland", PactTier.Ember, PactCategory.Breath,
            Lore: "Grafted from a whelp that never learned restraint.",
            RelevantElement: Element.Fire, BreathDamagePct: 0.25f),
        new PactDef("tempered_scales", "Tempered Scales", PactTier.Ember, PactCategory.Combat,
            Lore: "Quenched in cold ley-water, struck thrice.",
            RelevantElement: Element.Stone, DamagePct: 0.08f),
        new PactDef("fleet_footed", "Tailwind Oath", PactTier.Ember, PactCategory.Combat,
            Lore: "The storm owes a favor. It pays in shoves.",
            RelevantElement: Element.Storm, SpeedPct: 0.08f),
        new PactDef("iron_rations", "Iron Rations", PactTier.Ember, PactCategory.Economy,
            Lore: "An army that chews slower marches longer.",
            WalletCapBonus: 100f, DripBonusPerSecond: 1f),
        new PactDef("war_drums", "War Drums", PactTier.Ember, PactCategory.Combat,
            Lore: "Stretched hide of something that should have run faster.",
            SpeedPct: 0.06f, DamagePct: 0.04f),
        new PactDef("swift_supply", "Swift Supply", PactTier.Ember, PactCategory.Deployment,
            Lore: "The quartermaster has stopped asking questions.",
            DeployCooldownPct: 0.1f, DripBonusPerSecond: 1f),
        new PactDef("stoic_banner", "Stoic Banner", PactTier.Ember, PactCategory.Combat,
            Lore: "It has never been lowered. The pole has been replaced twice.",
            RelevantElement: Element.Stone, DamagePct: 0.05f, WalletCapBonus: 50f),
        new PactDef("frost_lensing", "Frost Lensing", PactTier.Ember, PactCategory.Breath,
            Lore: "Cold air carries fire further. Ask the burned.",
            RelevantElement: Element.Frost, BreathRegenPct: 0.25f),
        new PactDef("venom_coffers", "Venom Coffers", PactTier.Ember, PactCategory.Economy,
            Lore: "Every corpse owes the Marches a toll.",
            RelevantElement: Element.Venom, KillBountyPct: 0.2f),

        // Drake — strong terms, still freely given
        new PactDef("ley_geyser", "Ley Geyser", PactTier.Drake, PactCategory.Economy,
            Lore: "The vein remembers being an artery.",
            DripBonusPerSecond: 6f),
        new PactDef("bounty_hunters", "Reaver Tithe", PactTier.Drake, PactCategory.Economy,
            Lore: "The reavers keep the boots. You keep everything else.",
            KillBountyPct: 0.4f),
        new PactDef("war_chant", "War Chant", PactTier.Drake, PactCategory.Combat,
            Lore: "Old words. The kind mountains repeat back.",
            RelevantElement: Element.Fire, DamagePct: 0.15f),
        new PactDef("gale_wings", "Gale Wings", PactTier.Drake, PactCategory.Combat,
            Lore: "March-order rewritten by the wind itself.",
            RelevantElement: Element.Storm, SpeedPct: 0.15f),
        new PactDef("dragon_lungs", "Dragon Lungs", PactTier.Drake, PactCategory.Breath,
            Lore: "Bellows-work copied from the living article.",
            BreathRegenPct: 0.5f),
        new PactDef("drillmaster", "Drillmaster's Whistle", PactTier.Drake, PactCategory.Deployment,
            Lore: "Shrill enough to be heard over dying.",
            DeployCooldownPct: 0.3f),
        new PactDef("midas_claw", "Gilded Talon", PactTier.Drake, PactCategory.Economy,
            Lore: "A dragon's claw, snapped off mid-grasp. Still grasping.",
            KillBountyPct: 0.25f, WalletCapBonus: 200f),
        new PactDef("zephyr_call", "Zephyr Call", PactTier.Drake, PactCategory.Deployment,
            Lore: "Whistle, and the sky lends its haste.",
            RelevantElement: Element.Storm, SpeedPct: 0.1f, DeployCooldownPct: 0.15f),
        new PactDef("molten_core", "Molten Gullet", PactTier.Drake, PactCategory.Breath,
            Lore: "Swallowed embers, kept warm out of spite.",
            RelevantElement: Element.Fire, DamagePct: 0.1f, BreathDamagePct: 0.2f),
        new PactDef("rising_wings", "Rising Wings", PactTier.Drake, PactCategory.Deployment,
            Lore: "The spire leans toward the sky, listening.",
            AscensionTricklePct: 0.2f),
        new PactDef("glacial_patience", "Glacial Patience", PactTier.Drake, PactCategory.Economy,
            Lore: "The glacier also wanted things, once.",
            RelevantElement: Element.Frost, DripBonusPerSecond: 4f, WalletCapBonus: 100f),

        // Wyrm — mythic terms; the Broker always names a Price
        new PactDef("avatar_of_war", "Avatar of War", PactTier.Wyrm, PactCategory.Combat,
            Lore: "\"Your soldiers will burn brighter. Feeding them is your concern.\"",
            DamagePct: 0.3f, SpeedPct: 0.12f,
            PriceDripPerSecond: 3f),
        new PactDef("ley_singularity", "Ley Singularity", PactTier.Wyrm, PactCategory.Economy,
            Lore: "\"Crack the spire's heart and drink what pours out.\"",
            DripBonusPerSecond: 14f,
            PriceSpireHpPct: 0.2f),
        new PactDef("imminent_wings", "Imminent Wings", PactTier.Wyrm, PactCategory.Deployment,
            Lore: "\"The crossing toll can be haggled. The ferryman still eats.\"",
            SummonCostPct: 0.35f, AscensionTricklePct: 0.2f,
            PriceDripPerSecond: 2f),
        new PactDef("living_tempest", "Living Tempest", PactTier.Wyrm, PactCategory.Breath,
            Lore: "\"Borrow the storm's own throat. Mind the teeth marks.\"",
            RelevantElement: Element.Storm, BreathDamagePct: 0.4f, BreathRegenPct: 0.5f,
            WrathCooldownPct: 0.25f,
            PriceSpireHpPct: 0.12f),
        new PactDef("heart_of_the_spire", "Heart of the Spire", PactTier.Wyrm, PactCategory.Economy,
            Lore: "\"Carve out the heart. It beats harder in my hand.\"",
            WrathCooldownPct: 0.4f, DripBonusPerSecond: 4f,
            PriceSpireHpPct: 0.25f),
        new PactDef("draconic_covenant", "Draconic Covenant", PactTier.Wyrm, PactCategory.Deployment,
            Lore: "\"Sign in blood AND coin. The Court is thorough.\"",
            SummonCostPct: 0.25f, AscensionTricklePct: 0.2f, DamagePct: 0.1f,
            PriceSpireHpPct: 0.1f, PriceDripPerSecond: 2f),
    };

    private static readonly Dictionary<string, PactDef> Index = All.ToDictionary(def => def.Id);

    public static PactDef ById(string id)
    {
        return Index[id];
    }
}
