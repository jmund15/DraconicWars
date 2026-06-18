namespace DraconicWars.Sim.Battle;

using System.Collections.Generic;
using DraconicWars.Sim.Units;

/// <summary>A real, in-flight projectile (roster-expansion-40.md §5): travels the lane,
/// snapshots its damage + element at spawn (so it resolves correctly even after its caster
/// dies, like <see cref="LaneZone"/>), and hits the first enemy body it sweeps over —
/// body-blockable and dodgeable. Pierce hits every body in the path; splash applies an AoE
/// at the impact.</summary>
public sealed class SimProjectile
{
    public required int InstanceId { get; init; }

    public required PlayerSide Side { get; init; }

    public float X { get; set; }

    /// <summary>Signed toward the enemy spire (units per tick).</summary>
    public required float VelocityPerTick { get; init; }

    /// <summary>Damage snapshot at spawn (already element/buff-scaled) — outlives caster.</summary>
    public required int Damage { get; init; }

    public required Element Element { get; init; }

    public required bool Pierces { get; init; }

    public required float SplashRadius { get; init; }

    public required bool HitGround { get; init; }

    public required bool HitAir { get; init; }

    /// <summary>Mana returned to the owner per kill this shot scores, capped at
    /// ManaRefundCapPerShot for the shot's life (Voltherax). 0 = no refund.</summary>
    public int ManaRefundPerKill { get; init; }

    public int ManaRefundCapPerShot { get; init; }

    /// <summary>Running total refunded by this shot, so the per-shot cap holds across a pierce.</summary>
    public int ManaRefundedThisShot { get; set; }

    /// <summary>Pierce bookkeeping: bodies already struck, so a lingering sweep never
    /// double-hits the same unit.</summary>
    public HashSet<int> AlreadyHit { get; } = new();
}
