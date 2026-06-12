namespace DraconicWars.Sim.Units;

/// <summary>
/// Immutable unit definition — the sim's single source of truth for stats AND combat
/// timing (art manifests are validated against ForeswingTicks/BackswingTicks at import).
/// </summary>
public sealed record UnitDef(
    string Id,
    string DisplayName,
    int Tier,
    TypeClass TypeClass,
    Element Element,
    int MaxHp,
    int Damage,
    int ForeswingTicks,
    int BackswingTicks,
    float Range,
    float RangeMin,
    bool IsArea,
    float MoveSpeed,
    int KnockbackCount,
    int DeployCost,
    int DeployCooldownTicks,
    Stratum Stratum,
    bool CanTargetGround,
    bool CanTargetAir)
{
    /// <summary>Set when this def is an in-battle Rebreathing variant: the element the
    /// company was originally sworn to. Null means fighting under its native Breath.</summary>
    public Element? NativeElement { get; init; }
}
