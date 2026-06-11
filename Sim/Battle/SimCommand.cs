namespace DraconicWars.Sim.Battle;

using System;
using System.Collections.Generic;

public enum SimCommandKind
{
    Deploy,
}

public readonly record struct SimCommand(SimCommandKind Kind, PlayerSide Side, string UnitDefId)
{
    public static readonly IReadOnlyList<SimCommand> None = Array.Empty<SimCommand>();

    public static SimCommand Deploy(PlayerSide side, string unitDefId)
    {
        return new SimCommand(SimCommandKind.Deploy, side, unitDefId);
    }
}
