namespace DraconicWars.Sim.Battle;

using System;
using System.Collections.Generic;
using DraconicWars.Sim.Core;

public sealed class BattleState
{
    public required BattleConfig Config { get; init; }

    public required SimRng Rng { get; init; }

    public required PlayerState Left { get; init; }

    public required PlayerState Right { get; init; }

    public int Tick { get; set; }

    public BattleOutcome Outcome { get; set; } = BattleOutcome.Ongoing;

    public List<SimUnit> Units { get; } = new();

    /// <summary>Lingering lane zones (signature kits): seeded by contacts, expire on
    /// their own clock, persist past their caster's death by design.</summary>
    public List<LaneZone> Zones { get; } = new();

    /// <summary>Real in-flight projectiles (roster-expansion-40.md §5): travel the lane,
    /// snapshot damage at spawn, hit the first body swept over. Outlive their caster.</summary>
    public List<SimProjectile> Projectiles { get; } = new();

    public required SimRng PactRng { get; init; }

    /// <summary>Shared pre-rolled tier sequence; each side consumes it at its own
    /// pace as it earns parleys by tiering up (symmetric in PvP by construction).</summary>
    public required IReadOnlyList<DraconicWars.Sim.Pacts.PactTier> ParleyTierPath { get; init; }

    /// <summary>The Court's trials for this duel — same set for both sides, rolled
    /// at battle start and published (design.md §8, Edicts of Ascent).</summary>
    public List<DraconicWars.Sim.Edicts.ActiveEdict> Edicts { get; } = new();

    public float LeftSpireHp { get; set; }

    public float RightSpireHp { get; set; }

    public int NextInstanceId { get; set; } = 1;

    public PlayerState Player(PlayerSide side)
    {
        return side == PlayerSide.Left ? Left : Right;
    }

    /// <summary>
    /// FNV-1a fold over the deterministic gameplay state. Cooldown dictionaries are
    /// excluded: their iteration order is unspecified, and identical runs produce
    /// identical cooldowns anyway via the hashed fields' evolution.
    /// </summary>
    public ulong StateHash()
    {
        var hash = 14695981039346656037UL;

        Mix(ref hash, (ulong)Tick);
        Mix(ref hash, BitsOf(Left.Mana));
        Mix(ref hash, BitsOf(Right.Mana));
        Mix(ref hash, BitsOf(LeftSpireHp));
        Mix(ref hash, BitsOf(RightSpireHp));
        foreach (var unit in Units)
        {
            Mix(ref hash, (ulong)unit.InstanceId);
            Mix(ref hash, BitsOf(unit.X));
            Mix(ref hash, (ulong)(long)unit.Hp);
            Mix(ref hash, (ulong)unit.Side);
        }

        return hash;

        static void Mix(ref ulong hash, ulong value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        static ulong BitsOf(float value)
        {
            return (uint)BitConverter.SingleToInt32Bits(value);
        }
    }
}
