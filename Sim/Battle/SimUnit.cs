namespace DraconicWars.Sim.Battle;

using DraconicWars.Sim.Units;

public sealed class SimUnit
{
    public required int InstanceId { get; init; }

    public required UnitDef Def { get; init; }

    public required PlayerSide Side { get; init; }

    public float X { get; set; }

    public int Hp { get; set; }

    public AttackPhase AttackPhase { get; set; }

    public int PhaseTicksLeft { get; set; }

    public int KbIndex { get; set; }

    public int IFrameTicks { get; set; }

    public int SlowTicks { get; set; }

    public float SlowPct { get; set; }

    /// <summary>Self-suppression timer (freeze): while &gt;0 the unit neither attacks nor
    /// moves; ticks down like SlowTicks. Set by stun-dive / freeze kits and by the
    /// teleport+self-stun relocate pattern (Roc grab, Cloudwhale ferry) — no cross-unit
    /// ownership link.</summary>
    public int StunTicks { get; set; }

    /// <summary>Per-instance movement-speed override (units/sec); null falls back to
    /// Def.MoveSpeed. UnitDef is an immutable shared record, so kits that change one
    /// unit's pace (e.g. plaguecharger's dismount) write it here.</summary>
    public float? MoveSpeedOverride { get; set; }

    /// <summary>False while burrowed/submerged (the_tithe): invisible to all enemy
    /// targeting and immune to damage, zone-slows, shoves, and projectiles. Default true.</summary>
    public bool Targetable { get; set; } = true;

    /// <summary>Counts down to the next deferred spawn (spawner units only).</summary>
    public int SpawnTimer { get; set; }

    /// <summary>Position within the phase-shift cycle (0..PhaseCadenceTicks-1) for units
    /// that periodically go untargetable (spore_wisp). Drives the Targetable toggle.</summary>
    public int PhaseClock { get; set; }

    /// <summary>Accumulated ticks of dwell in enemy freeze fields (Glacereth); resets when not
    /// in any freeze zone and on freeze. At the zone's threshold the unit is frozen.</summary>
    public int FreezeDwellTicks { get; set; }

    // Contagion poison (Sythraal). The recipe rides on the unit so a death can spawn the next
    // chain generation even after the dragon that started it is gone.
    public int PoisonTicks { get; set; }

    public int PoisonBaseDamage { get; set; }

    public int PoisonContagionDepth { get; set; }

    public float PoisonSpreadRadius { get; set; }

    public float PoisonDepthBonus { get; set; }

    public int PoisonDurationTicks { get; set; }

    /// <summary>Temporary defensive-element override (mossmite's mark); null = use
    /// Def.Element. Read by the counter lookup in ScaledDamage.</summary>
    public Element? ElementOverride { get; set; }

    public int ElementOverrideTicks { get; set; }

    public bool IsAlive => Hp > 0;

    public Stratum Stratum => Def.Stratum;

    // Signature-kit runtime state (all zero/false unless the def carries the kit).

    public bool RevivedOnce { get; set; }

    public int VigilTicks { get; set; }

    public int TollCount { get; set; }

    public bool HasStruck { get; set; }

    /// <summary>One-tick pulse: true only during the tick the unit's foreswing
    /// resolves (the damage moment). The view layer reads it to spawn a cosmetic
    /// attack form; it does not affect hitscan resolution. See
    /// arch-attack-archetypes.md §6.</summary>
    public bool ContactTriggered { get; set; }
}

public enum AttackPhase
{
    None,
    Foreswing,
    Backswing,
}
