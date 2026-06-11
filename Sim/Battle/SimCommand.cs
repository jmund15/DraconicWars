namespace DraconicWars.Sim.Battle;

using System;
using System.Collections.Generic;

public enum SimCommandKind
{
    Deploy,
    BuildConduit,
    UpgradeConduit,
    SellConduit,
    ChannelMana,
}

public readonly record struct SimCommand(
    SimCommandKind Kind, PlayerSide Side, string TargetId, int Amount = 0)
{
    public static SimCommand ChannelMana(PlayerSide side, int amount)
    {
        return new SimCommand(SimCommandKind.ChannelMana, side, string.Empty, amount);
    }

    public static readonly IReadOnlyList<SimCommand> None = Array.Empty<SimCommand>();

    public static SimCommand Deploy(PlayerSide side, string unitDefId)
    {
        return new SimCommand(SimCommandKind.Deploy, side, unitDefId);
    }

    public static SimCommand BuildConduit(PlayerSide side, string conduitId)
    {
        return new SimCommand(SimCommandKind.BuildConduit, side, conduitId);
    }

    public static SimCommand UpgradeConduit(PlayerSide side, string conduitId)
    {
        return new SimCommand(SimCommandKind.UpgradeConduit, side, conduitId);
    }

    public static SimCommand SellConduit(PlayerSide side, string conduitId)
    {
        return new SimCommand(SimCommandKind.SellConduit, side, conduitId);
    }
}
