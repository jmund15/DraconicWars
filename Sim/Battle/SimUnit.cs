namespace DraconicWars.Sim.Battle;

using DraconicWars.Sim.Units;

public sealed class SimUnit
{
    public required int InstanceId { get; init; }

    public required UnitDef Def { get; init; }

    public required PlayerSide Side { get; init; }

    public float X { get; set; }

    public int Hp { get; set; }

    public bool IsAlive => Hp > 0;

    public Stratum Stratum => Def.Stratum;
}
