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
    FireBreath,
    CastWrath,
    SealPact,
    PayTithe,
    AttuneUnit,
    BuySocket,
}

public readonly record struct SimCommand(
    SimCommandKind Kind, PlayerSide Side, string TargetId, int Amount = 0, float X = 0f)
{
    public static SimCommand ChannelMana(PlayerSide side, int amount)
    {
        return new SimCommand(SimCommandKind.ChannelMana, side, string.Empty, amount);
    }

    /// <summary>Held-fire command: issue one per tick while the breath verb is held.</summary>
    public static SimCommand FireBreath(PlayerSide side, float x)
    {
        return new SimCommand(SimCommandKind.FireBreath, side, string.Empty, 0, x);
    }

    public static SimCommand CastWrath(PlayerSide side)
    {
        return new SimCommand(SimCommandKind.CastWrath, side, string.Empty);
    }

    public static SimCommand SealPact(PlayerSide side, string pactId)
    {
        return new SimCommand(SimCommandKind.SealPact, side, pactId);
    }

    public static SimCommand PayTithe(PlayerSide side)
    {
        return new SimCommand(SimCommandKind.PayTithe, side, string.Empty);
    }

    /// <summary>Rebreathing: re-swear a company's Breath for the rest of the duel.
    /// Element rides in Amount (the command payload stays a flat value struct).</summary>
    public static SimCommand AttuneUnit(
        PlayerSide side, string unitDefId, DraconicWars.Sim.Units.Element element)
    {
        return new SimCommand(SimCommandKind.AttuneUnit, side, unitDefId, (int)element);
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

    public static SimCommand BuySocket(PlayerSide side)
    {
        return new SimCommand(SimCommandKind.BuySocket, side, string.Empty);
    }
}
