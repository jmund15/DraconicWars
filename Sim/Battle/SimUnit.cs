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

    public bool IsAlive => Hp > 0;

    public Stratum Stratum => Def.Stratum;
}

public enum AttackPhase
{
    None,
    Foreswing,
    Backswing,
}
