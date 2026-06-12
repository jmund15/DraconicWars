namespace DraconicWars.Sim.Battle;

/// <summary>A lingering patch on the lane: slows and chips enemies of its owner
/// standing inside until it expires. Persists past its caster's death by design
/// ("her spells arrive late and stay forever").</summary>
public sealed class LaneZone
{
    public required PlayerSide Side { get; init; }

    public required float X { get; init; }

    public required float Radius { get; init; }

    public required float SlowPct { get; init; }

    public required int DamagePerTick { get; init; }

    public int TicksLeft { get; set; }
}
