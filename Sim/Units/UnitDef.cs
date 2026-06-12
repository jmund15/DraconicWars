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

    // Signature kits (Full Roster Batch): ONE data-driven mechanic per unit, all
    // defaulting off. The sim reads these; no per-unit code exists anywhere.

    /// <summary>Lobbed shots strike the FARTHEST enemy in band, arcing over screens.</summary>
    public bool PrefersFarthestTarget { get; init; }

    /// <summary>&gt;0: rises once per life at this fraction of MaxHp. Every death —
    /// including the one it stands back up from — pays the killer in full.</summary>
    public float ReviveHpPct { get; init; }

    public int OnDeathBlastDamage { get; init; }

    public float OnDeathBlastRadius { get; init; }

    /// <summary>Per-tick damage to every enemy (both strata) within AuraRadius.</summary>
    public int AuraDamagePerTick { get; init; }

    public float AuraRadius { get; init; }

    /// <summary>Damage reduction accrued per second while in an attack phase; any
    /// forced displacement (knockback, shove, Wrath) wipes the accrual.</summary>
    public float VigilDrPerSecond { get; init; }

    public float VigilDrMaxPct { get; init; }

    /// <summary>&gt;0: contacts seed a lingering lane zone at the impact point.</summary>
    public float ZoneRadius { get; init; }

    public float ZoneSlowPct { get; init; }

    public int ZoneDurationTicks { get; init; }

    public int ZoneDamagePerTick { get; init; }

    /// <summary>&gt;0: each contact force-shoves all non-Unstaggerable enemies within
    /// ShoveRadius ahead of it backward (Wrath-style: no i-frames, interrupts).</summary>
    public float ShoveDistance { get; init; }

    public float ShoveRadius { get; init; }

    /// <summary>Heals for this fraction of damage dealt, capped at the victim's
    /// remaining HP (overkill pays nothing).</summary>
    public float LifestealPct { get; init; }

    /// <summary>Each resolved contact rings the next one this much harder (additive,
    /// capped at TollRampCap); a threshold knockback resets the accrual.</summary>
    public float TollRampPct { get; init; }

    public float TollRampCap { get; init; }

    /// <summary>Bonus damage fraction against targets at or above HighHpThreshold.</summary>
    public float BonusVsHighHpPct { get; init; }

    public int HighHpThreshold { get; init; }

    /// <summary>Combat knockback never moves or interrupts it; Wrath still does.</summary>
    public bool Unstaggerable { get; init; }

    /// <summary>First contact per life skips the wind-up and hits this much harder.</summary>
    public float FirstStrikeBonusPct { get; init; }

    /// <summary>Translates this far PAST its target when an attack cycle completes.</summary>
    public float StrafeDistance { get; init; }
}
