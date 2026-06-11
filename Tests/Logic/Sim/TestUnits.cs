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

    internal static UnitDef Mage(string id = "mage") => Grunt(id) with
    {
        DisplayName = "Test Mage",
        TypeClass = TypeClass.Ranged,
        Range = 5.0f,
        IsArea = true,
        Damage = 25,
    };

    internal static UnitDef Sniper(string id = "sniper") => Grunt(id) with
    {
        DisplayName = "Test Sniper",
        TypeClass = TypeClass.Sniper,
        Range = 8.0f,
        RangeMin = 3.0f,
        Damage = 30,
    };

    internal static UnitDef FastBiter(string id = "biter") => Grunt(id) with
    {
        DisplayName = "Test Fast Biter",
        ForeswingTicks = 2,
        BackswingTicks = 4,
        Damage = 40,
    };

    internal static UnitDef Elite(string id = "elite") => Grunt(id) with
    {
        DisplayName = "Test Elite",
        Tier = 2,
        DeployCost = 150,
    };

    internal static UnitDef Dragon(string id = "dragon") => Grunt(id) with
    {
        DisplayName = "Test Dragon",
        Tier = 4,
        MaxHp = 1200,
        Damage = 80,
        Range = 2.5f,
        IsArea = true,
        Stratum = Stratum.Air,
        CanTargetAir = true,
        DeployCost = 0,
    };
}
