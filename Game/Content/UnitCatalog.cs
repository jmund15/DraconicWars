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
    };
}
