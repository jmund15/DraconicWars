namespace DraconicWars.Tests.Logic.Sim;

using DraconicWars.Sim.Units;

internal static class TestUnits
{
    internal static UnitDef Grunt(
        string id = "grunt",
        int deployCost = 50,
        int deployCooldownTicks = 60,
        float moveSpeed = 2.0f,
        float range = 0.8f) => new(
        Id: id,
        DisplayName: "Test Grunt",
        Tier: 1,
        TypeClass: TypeClass.Melee,
        Element: Element.Stone,
        MaxHp: 100,
        Damage: 10,
        ForeswingTicks: 8,
        BackswingTicks: 10,
        Range: range,
        RangeMin: 0f,
        IsArea: false,
        MoveSpeed: moveSpeed,
        KnockbackCount: 3,
        DeployCost: deployCost,
        DeployCooldownTicks: deployCooldownTicks,
        Stratum: Stratum.Ground,
        CanTargetGround: true,
        CanTargetAir: false);

    internal static UnitDef Archer(string id = "archer", float range = 6.0f) => Grunt(id) with
    {
        DisplayName = "Test Archer",
        TypeClass = TypeClass.Ranged,
        Range = range,
        CanTargetAir = true,
    };

    internal static UnitDef Whelp(string id = "whelp") => Grunt(id) with
    {
        DisplayName = "Test Whelp",
        TypeClass = TypeClass.Aerial,
        Stratum = Stratum.Air,
        Range = 1.2f,
        CanTargetGround = true,
        CanTargetAir = true,
    };
}
