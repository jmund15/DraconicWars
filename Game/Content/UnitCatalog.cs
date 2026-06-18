namespace DraconicWars.Game.Content;

using System.Collections.Generic;
using DraconicWars.Sim.Units;

/// <summary>
/// First-playable unit roster. Foreswing/backswing ticks are the source of truth the
/// art generator consumes; UnitSpriteLibrary fails loading on any manifest drift.
/// Stats are v1 provisional pending the balance pass.
/// </summary>
public static class UnitCatalog
{
    /// <summary>Collection rarity per unit (roster-expansion-40.md §1) — decoupled from
    /// Tier, applied to the rosters below. Ids absent here default to Common.</summary>
    private static readonly Dictionary<string, Rarity> Rarities = new()
    {
        ["elder_drake"] = Rarity.Draconic,
        ["pyraxis"] = Rarity.Draconic,
        ["voltherax"] = Rarity.Draconic,
        ["glacereth"] = Rarity.Draconic,
        ["sythraal"] = Rarity.Draconic,
        ["terravossk"] = Rarity.Draconic,
        ["stone_ram"] = Rarity.Uncommon,
        ["storm_gryphon"] = Rarity.Uncommon,
        ["cinder_wyrmling"] = Rarity.Uncommon,
        ["pyre_ogre"] = Rarity.Uncommon,
        ["rime_sentry"] = Rarity.Uncommon,
        ["quarry_slinger"] = Rarity.Uncommon,
        ["spark_courier"] = Rarity.Uncommon,
        ["skylance_emberknight"] = Rarity.Uncommon,
        ["cinder_acolyte"] = Rarity.Rare,
        ["ash_revenant"] = Rarity.Rare,
        ["glacier_adept"] = Rarity.Rare,
        ["bog_stalker"] = Rarity.Rare,
        ["plague_bell"] = Rarity.Rare,
        ["gale_harrier"] = Rarity.Rare,
        ["mossmite"] = Rarity.Rare,
        ["boreal_colossus"] = Rarity.Epic,
        ["deepway_bulwark"] = Rarity.Epic,
        ["crag_tyrant"] = Rarity.Epic,
        ["tempest_choir"] = Rarity.Epic,
    };

    private static IReadOnlyList<UnitDef> ApplyRarity(IReadOnlyList<UnitDef> defs)
    {
        var result = new List<UnitDef>(defs.Count);
        foreach (var def in defs)
        {
            result.Add(def with { Rarity = Rarities.GetValueOrDefault(def.Id, Rarity.Common) });
        }
        return result;
    }

    public static readonly IReadOnlyList<UnitDef> FirstPlayable = ApplyRarity(new[]
    {
        new UnitDef(
            Id: "kobold_spearman", DisplayName: "Kobold Spearman", Tier: 1,
            TypeClass: TypeClass.Melee, Element: Element.Fire,
            MaxHp: 110, Damage: 12, ForeswingTicks: 4, BackswingTicks: 8,
            Range: 0.9f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.2f,
            KnockbackCount: 3, DeployCost: 40, DeployCooldownTicks: 60,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false),
        new UnitDef(
            Id: "forest_archer", DisplayName: "Forest Archer", Tier: 1,
            TypeClass: TypeClass.Ranged, Element: Element.Venom,
            MaxHp: 70, Damage: 10, ForeswingTicks: 6, BackswingTicks: 12,
            Range: 6.5f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.8f,
            KnockbackCount: 2, DeployCost: 60, DeployCooldownTicks: 90,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            Attack = new AttackArchetype(AttackClass.Physical, AttackPose.Shoot, AttackForm.Arrow),
        },
        new UnitDef(
            Id: "frost_whelp", DisplayName: "Frost Whelp", Tier: 1,
            TypeClass: TypeClass.Aerial, Element: Element.Frost,
            MaxHp: 130, Damage: 9, ForeswingTicks: 6, BackswingTicks: 6,
            Range: 1.3f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.6f,
            KnockbackCount: 2, DeployCost: 75, DeployCooldownTicks: 120,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true),
        new UnitDef(
            Id: "stone_warden", DisplayName: "Stone Warden", Tier: 1,
            TypeClass: TypeClass.Melee, Element: Element.Stone,
            MaxHp: 220, Damage: 6, ForeswingTicks: 10, BackswingTicks: 14,
            Range: 0.8f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.4f,
            KnockbackCount: 4, DeployCost: 55, DeployCooldownTicks: 90,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false),
        new UnitDef(
            Id: "storm_monk", DisplayName: "Storm Monk", Tier: 2,
            TypeClass: TypeClass.Melee, Element: Element.Storm,
            MaxHp: 140, Damage: 16, ForeswingTicks: 4, BackswingTicks: 6,
            Range: 0.9f, RangeMin: 0f, IsArea: false, MoveSpeed: 3.2f,
            KnockbackCount: 2, DeployCost: 110, DeployCooldownTicks: 150,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false),
        new UnitDef(
            Id: "vale_chanter", DisplayName: "Vale Chanter", Tier: 2,
            TypeClass: TypeClass.Support, Element: Element.Venom,
            MaxHp: 90, Damage: 8, ForeswingTicks: 10, BackswingTicks: 20,
            Range: 5.0f, RangeMin: 0f, IsArea: true, MoveSpeed: 1.6f,
            KnockbackCount: 2, DeployCost: 130, DeployCooldownTicks: 180,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Channel, AttackForm.Ball),
        },
        new UnitDef(
            Id: "dune_marksman", DisplayName: "Dune Marksman", Tier: 2,
            TypeClass: TypeClass.Sniper, Element: Element.Storm,
            MaxHp: 80, Damage: 30, ForeswingTicks: 14, BackswingTicks: 22,
            Range: 9.0f, RangeMin: 3.0f, IsArea: false, MoveSpeed: 1.5f,
            KnockbackCount: 1, DeployCost: 140, DeployCooldownTicks: 210,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            Attack = new AttackArchetype(AttackClass.Physical, AttackPose.Shoot, AttackForm.Arrow),
        },
        new UnitDef(
            Id: "stone_ram", DisplayName: "Stone Ram", Tier: 3,
            TypeClass: TypeClass.Siege, Element: Element.Stone,
            MaxHp: 450, Damage: 35, ForeswingTicks: 12, BackswingTicks: 24,
            Range: 1.0f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.2f,
            KnockbackCount: 1, DeployCost: 260, DeployCooldownTicks: 400,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false),
        new UnitDef(
            Id: "storm_gryphon", DisplayName: "Storm Gryphon", Tier: 3,
            TypeClass: TypeClass.Aerial, Element: Element.Storm,
            MaxHp: 320, Damage: 22, ForeswingTicks: 8, BackswingTicks: 10,
            Range: 1.5f, RangeMin: 0f, IsArea: false, MoveSpeed: 3.0f,
            KnockbackCount: 2, DeployCost: 240, DeployCooldownTicks: 360,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true),
        new UnitDef(
            Id: "elder_drake", DisplayName: "Elder Drake", Tier: 4,
            TypeClass: TypeClass.Aerial, Element: Element.Stone,
            MaxHp: 1500, Damage: 60, ForeswingTicks: 12, BackswingTicks: 18,
            Range: 2.5f, RangeMin: 0f, IsArea: true, MoveSpeed: 1.6f,
            KnockbackCount: 5, DeployCost: 0, DeployCooldownTicks: 0,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true),
        new UnitDef(
            Id: "pyraxis", DisplayName: "Pyraxis, the Cinder Tyrant", Tier: 4,
            TypeClass: TypeClass.Aerial, Element: Element.Fire,
            MaxHp: 1600, Damage: 90, ForeswingTicks: 10, BackswingTicks: 14,
            Range: 2.8f, RangeMin: 0f, IsArea: true, MoveSpeed: 1.8f,
            KnockbackCount: 6, DeployCost: 0, DeployCooldownTicks: 0,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true),
    });

    /// <summary>The rental dragon every loadout fields until a real dragon is bonded.</summary>
    public const string RentalDragonId = "elder_drake";

    /// <summary>
    /// V1 roster expansion (Full Roster Batch): 13 units lifting every element to 4+
    /// distinct types so both Resonance thresholds are reachable for all five Breaths.
    /// Distributed across the campaign by the unlock schedule (Campaign Full) — not
    /// part of the first-playable loadout.
    /// </summary>
    public static readonly IReadOnlyList<UnitDef> RosterExpansion = ApplyRarity(new[]
    {
        // Fire — 4 total with the kobold (5 with the ogre).
        new UnitDef(
            Id: "cinder_acolyte", DisplayName: "Cinder Acolyte", Tier: 1,
            TypeClass: TypeClass.Ranged, Element: Element.Fire,
            MaxHp: 80, Damage: 12, ForeswingTicks: 8, BackswingTicks: 12,
            Range: 5.5f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.7f,
            KnockbackCount: 2, DeployCost: 70, DeployCooldownTicks: 90,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            PrefersFarthestTarget = true,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Ball),
        },
        new UnitDef(
            Id: "ash_revenant", DisplayName: "Ash Revenant", Tier: 2,
            TypeClass: TypeClass.Melee, Element: Element.Fire,
            MaxHp: 240, Damage: 22, ForeswingTicks: 5, BackswingTicks: 7,
            Range: 0.9f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.6f,
            KnockbackCount: 2, DeployCost: 150, DeployCooldownTicks: 150,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            ReviveHpPct = 0.5f,
        },
        new UnitDef(
            Id: "cinder_wyrmling", DisplayName: "Cinder Wyrmling", Tier: 2,
            TypeClass: TypeClass.Aerial, Element: Element.Fire,
            MaxHp: 160, Damage: 20, ForeswingTicks: 8, BackswingTicks: 10,
            Range: 1.3f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.4f,
            KnockbackCount: 3, DeployCost: 190, DeployCooldownTicks: 260,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            OnDeathBlastDamage = 40,
            OnDeathBlastRadius = 2.5f,
        },
        new UnitDef(
            Id: "pyre_ogre", DisplayName: "Pyre Ogre", Tier: 3,
            TypeClass: TypeClass.Melee, Element: Element.Fire,
            MaxHp: 700, Damage: 45, ForeswingTicks: 14, BackswingTicks: 20,
            Range: 1.4f, RangeMin: 0f, IsArea: true, MoveSpeed: 1.1f,
            KnockbackCount: 4, DeployCost: 320, DeployCooldownTicks: 300,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            AuraDamagePerTick = 2,
            AuraRadius = 2.5f,
        },

        // Frost — 4 total with the whelp.
        new UnitDef(
            Id: "rime_sentry", DisplayName: "Rime Sentry", Tier: 1,
            TypeClass: TypeClass.Melee, Element: Element.Frost,
            MaxHp: 95, Damage: 9, ForeswingTicks: 7, BackswingTicks: 13,
            Range: 0.8f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.0f,
            KnockbackCount: 3, DeployCost: 45, DeployCooldownTicks: 70,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            VigilDrPerSecond = 0.04f,
            VigilDrMaxPct = 0.4f,
        },
        new UnitDef(
            Id: "glacier_adept", DisplayName: "Glacier Adept", Tier: 2,
            TypeClass: TypeClass.Ranged, Element: Element.Frost,
            MaxHp: 150, Damage: 16, ForeswingTicks: 10, BackswingTicks: 16,
            Range: 6f, RangeMin: 0f, IsArea: true, MoveSpeed: 1.6f,
            KnockbackCount: 2, DeployCost: 160, DeployCooldownTicks: 240,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            ZoneRadius = 2f,
            ZoneSlowPct = 0.4f,
            ZoneDurationTicks = 150,
            ZoneDamagePerTick = 1,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Shard),
        },
        new UnitDef(
            Id: "boreal_colossus", DisplayName: "Boreal Colossus", Tier: 3,
            TypeClass: TypeClass.Melee, Element: Element.Frost,
            MaxHp: 950, Damage: 30, ForeswingTicks: 12, BackswingTicks: 18,
            Range: 1.2f, RangeMin: 0f, IsArea: false, MoveSpeed: 0.9f,
            KnockbackCount: 5, DeployCost: 380, DeployCooldownTicks: 360,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            ShoveDistance = 2.5f,
            ShoveRadius = 2f,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Shard),
        },

        // Venom — 4 total with the archer and chanter.
        new UnitDef(
            Id: "bog_stalker", DisplayName: "Bog Stalker", Tier: 1,
            TypeClass: TypeClass.Melee, Element: Element.Venom,
            MaxHp: 85, Damage: 11, ForeswingTicks: 5, BackswingTicks: 9,
            Range: 0.9f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.8f,
            KnockbackCount: 2, DeployCost: 55, DeployCooldownTicks: 80,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            LifestealPct = 0.5f,
        },
        new UnitDef(
            Id: "plague_bell", DisplayName: "Plague Bell", Tier: 3,
            TypeClass: TypeClass.Siege, Element: Element.Venom,
            MaxHp: 300, Damage: 28, ForeswingTicks: 12, BackswingTicks: 22,
            Range: 7f, RangeMin: 2f, IsArea: true, MoveSpeed: 1.0f,
            KnockbackCount: 3, DeployCost: 340, DeployCooldownTicks: 330,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            TollRampPct = 0.12f,
            TollRampCap = 1f,
        },

        // Stone — 4 total with the warden and ram.
        new UnitDef(
            Id: "quarry_slinger", DisplayName: "Quarry Slinger", Tier: 2,
            TypeClass: TypeClass.Sniper, Element: Element.Stone,
            MaxHp: 130, Damage: 34, ForeswingTicks: 13, BackswingTicks: 20,
            Range: 8.5f, RangeMin: 2.5f, IsArea: false, MoveSpeed: 1.5f,
            KnockbackCount: 2, DeployCost: 170, DeployCooldownTicks: 240,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true)
        {
            BonusVsHighHpPct = 0.5f,
            HighHpThreshold = 300,
            Attack = new AttackArchetype(AttackClass.Physical, AttackPose.Shoot, AttackForm.Arrow),
        },
        new UnitDef(
            Id: "deepway_bulwark", DisplayName: "Deepway Bulwark", Tier: 2,
            TypeClass: TypeClass.Melee, Element: Element.Stone,
            MaxHp: 420, Damage: 12, ForeswingTicks: 10, BackswingTicks: 16,
            Range: 0.9f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.2f,
            KnockbackCount: 4, DeployCost: 200, DeployCooldownTicks: 280,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            Unstaggerable = true,
        },

        // Storm — 5 total with the monk, marksman, and gryphon.
        new UnitDef(
            Id: "spark_courier", DisplayName: "Spark Courier", Tier: 1,
            TypeClass: TypeClass.Melee, Element: Element.Storm,
            MaxHp: 75, Damage: 10, ForeswingTicks: 4, BackswingTicks: 8,
            Range: 0.8f, RangeMin: 0f, IsArea: false, MoveSpeed: 3.0f,
            KnockbackCount: 2, DeployCost: 50, DeployCooldownTicks: 75,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            FirstStrikeBonusPct = 1.5f,
        },
        new UnitDef(
            Id: "gale_harrier", DisplayName: "Gale Harrier", Tier: 2,
            TypeClass: TypeClass.Aerial, Element: Element.Storm,
            MaxHp: 200, Damage: 18, ForeswingTicks: 7, BackswingTicks: 9,
            Range: 1.3f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.9f,
            KnockbackCount: 3, DeployCost: 180, DeployCooldownTicks: 240,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            StrafeDistance = 3.5f,
        },
    });

    /// <summary>Roster expansion to 40 (roster-expansion-40.md) — NEW units beyond the
    /// original 24, authored on the complete sim-primitive foundation. Grows as their art
    /// renders land (run_expansion_batch.py); ticks here mirror the rendered manifests.</summary>
    public static readonly IReadOnlyList<UnitDef> RosterExpansionTo40 = ApplyRarity(new[]
    {
        // Graduated demo form (FlyingMount): a Dragonlord lieutenant whose lance is a real
        // projectile (ProjectileSpeed → the sim's interceptable-projectile seam).
        new UnitDef(
            Id: "skylance_emberknight", DisplayName: "Skylance Emberknight", Tier: 2,
            TypeClass: TypeClass.Aerial, Element: Element.Fire,
            MaxHp: 210, Damage: 26, ForeswingTicks: 8, BackswingTicks: 12,
            Range: 6f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.7f,
            KnockbackCount: 2, DeployCost: 175, DeployCooldownTicks: 230,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            ProjectileSpeed = 3f,
        },

        // Proven sniper_biped rig (same as quarry_slinger/dune_marksman): a Common
        // glass-cannon Fire sniper. Big single bolt, slow cycle, no AA, sniper dead-zone
        // (RangeMin) — anti-tank burst that dies if rushed. No new sim mechanic.
        new UnitDef(
            Id: "ember_arbalest", DisplayName: "Ember Arbalest", Tier: 1,
            TypeClass: TypeClass.Sniper, Element: Element.Fire,
            MaxHp: 70, Damage: 42, ForeswingTicks: 14, BackswingTicks: 24,
            Range: 7.5f, RangeMin: 2.5f, IsArea: false, MoveSpeed: 1.5f,
            KnockbackCount: 1, DeployCost: 90, DeployCooldownTicks: 200,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: false)
        {
            Attack = new AttackArchetype(AttackClass.Physical, AttackPose.Shoot, AttackForm.Arrow),
        },

        // Graduated demo form (aerial_flyer manta): a Common Frost anti-air interceptor.
        // PrefersAirTarget makes it hunt enemy flyers first, falling back to ground when
        // the sky is clear. No strafe — it holds the lane's airspace.
        new UnitDef(
            Id: "glide_manta", DisplayName: "Glide Manta", Tier: 1,
            TypeClass: TypeClass.Aerial, Element: Element.Frost,
            MaxHp: 120, Damage: 12, ForeswingTicks: 6, BackswingTicks: 10,
            Range: 1.4f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.8f,
            KnockbackCount: 2, DeployCost: 70, DeployCooldownTicks: 110,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            PrefersAirTarget = true,
        },

        // Graduated demo form (aerial_flyer wisp): a Common Venom evasive harasser. Floats
        // in the air stratum (only AA reaches it) and periodically phases out for an i-frame
        // window (PhaseCadenceTicks/PhaseDurationTicks) — low damage, hard to pin down.
        new UnitDef(
            Id: "spore_wisp", DisplayName: "Spore Wisp", Tier: 1,
            TypeClass: TypeClass.Ranged, Element: Element.Venom,
            MaxHp: 60, Damage: 8, ForeswingTicks: 6, BackswingTicks: 10,
            Range: 5.5f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.0f,
            KnockbackCount: 2, DeployCost: 65, DeployCooldownTicks: 100,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            PhaseCadenceTicks = 90,
            PhaseDurationTicks = 18,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Ball),
        },

        // Colossal feather_wing preset (demo_phoenix rig family, ~2x scale): an Epic Stone
        // Roc. "Return To Sender" — each contact seizes the frontmost non-Unstaggerable
        // enemy and throws it back toward its own spire, stunned (GrabThrowDistance/Ticks).
        new UnitDef(
            Id: "crag_tyrant", DisplayName: "Crag Tyrant", Tier: 3,
            TypeClass: TypeClass.Aerial, Element: Element.Stone,
            MaxHp: 600, Damage: 28, ForeswingTicks: 12, BackswingTicks: 16,
            Range: 5f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.0f,
            KnockbackCount: 2, DeployCost: 300, DeployCooldownTicks: 360,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            GrabThrowDistance = 6f,
            GrabStunTicks = 24,
        },

        // Graduated demo form (aerial_flyer seraph): an Epic Storm support. "Pays For
        // Itself" — a living mana-conduit (+4/s, max 2 contributing) projecting a haste-halo
        // that speeds nearby allies' attacks (~foreswing -20%). Weak on its own.
        new UnitDef(
            Id: "tempest_choir", DisplayName: "Tempest Choir", Tier: 3,
            TypeClass: TypeClass.Support, Element: Element.Storm,
            MaxHp: 260, Damage: 10, ForeswingTicks: 10, BackswingTicks: 16,
            Range: 5f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.6f,
            KnockbackCount: 2, DeployCost: 280, DeployCooldownTicks: 330,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            ConduitManaPerSecond = 4f,
            ConduitContributeCap = 2,
            HasteHaloSpeedPct = 0.25f,
            HasteHaloRadius = 5f,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Channel, AttackForm.Ball),
        },

        // Graduated demo form (aerial_flyer insect_wing): a Rare Venom counter-flipper. Each
        // hit marks the target's DEFENSIVE element (OverrideTargetElement) for 90t so allied
        // counters land. Full payoff scales with how many allies declare counters (see the
        // roster counter-declaration pass) — the kit + primitive are complete here.
        new UnitDef(
            Id: "mossmite", DisplayName: "Mossmite", Tier: 2,
            TypeClass: TypeClass.Ranged, Element: Element.Venom,
            MaxHp: 90, Damage: 10, ForeswingTicks: 6, BackswingTicks: 12,
            Range: 5.5f, RangeMin: 0f, IsArea: false, MoveSpeed: 2.4f,
            KnockbackCount: 2, DeployCost: 150, DeployCooldownTicks: 180,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            OverrideTargetElement = Element.Fire,
            OverrideTargetTicks = 90,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Ball),
        },

        // --- The 4 new Draconic dragons (escrow-summoned crescendo, 1/element). DeployCost 0
        // (summoned, not deployed); Tier 4 (Dragon). Each on its own rendered boss silhouette.

        // Voltherax (Storm) — "You Heard It Late." Skip-windup piercing lightning bolt that
        // refunds mana per kill (capped per shot).
        new UnitDef(
            Id: "voltherax", DisplayName: "Voltherax, the Late Thunder", Tier: 4,
            TypeClass: TypeClass.Aerial, Element: Element.Storm,
            MaxHp: 1400, Damage: 70, ForeswingTicks: 8, BackswingTicks: 12,
            Range: 7f, RangeMin: 0f, IsArea: false, MoveSpeed: 1.8f,
            KnockbackCount: 4, DeployCost: 0, DeployCooldownTicks: 0,
            Stratum: Stratum.Air, CanTargetGround: true, CanTargetAir: true)
        {
            ProjectileSpeed = 5f,
            ProjectilePierces = true,
            ManaRefundPerKill = 8,
            ManaRefundCapPerShot = 24,
            Attack = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Ball),
        },
    });

    /// <summary>FirstPlayable + both expansions — the complete muster roll.</summary>
    public static readonly IReadOnlyList<UnitDef> FullRoster =
        System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.Concat(
                System.Linq.Enumerable.Concat(FirstPlayable, RosterExpansion),
                RosterExpansionTo40));
}
