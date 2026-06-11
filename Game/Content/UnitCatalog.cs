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
    public static readonly IReadOnlyList<UnitDef> FirstPlayable = new[]
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
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true),
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
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true),
        new UnitDef(
            Id: "dune_marksman", DisplayName: "Dune Marksman", Tier: 2,
            TypeClass: TypeClass.Sniper, Element: Element.Storm,
            MaxHp: 80, Damage: 30, ForeswingTicks: 14, BackswingTicks: 22,
            Range: 9.0f, RangeMin: 3.0f, IsArea: false, MoveSpeed: 1.5f,
            KnockbackCount: 1, DeployCost: 140, DeployCooldownTicks: 210,
            Stratum: Stratum.Ground, CanTargetGround: true, CanTargetAir: true),
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
    };

    /// <summary>The rental dragon every loadout fields until a real dragon is bonded.</summary>
    public const string RentalDragonId = "elder_drake";
}
