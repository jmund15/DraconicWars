namespace DraconicWars.Sim.Edicts;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;

/// <summary>Shared progress read for sim completion checks and HUD display.</summary>
public static class EdictProgress
{
    public static float Of(PlayerState player, EdictDef def)
    {
        return def.Kind switch
        {
            EdictKind.ElementManaDeployed => def.RequiredElement is { } element
                ? player.ManaDeployedByElement.GetValueOrDefault(element)
                : 0f,
            EdictKind.SingleDeployCost => player.MaxSingleDeployCost,
            EdictKind.ConduitGrafts => player.ConduitGrafts,
            EdictKind.Kills => player.Kills,
            EdictKind.BreathPulses => player.BreathPulsesLanded,
            EdictKind.BankedMana => player.Mana,
            _ => 0f,
        };
    }

    public static bool Completed(PlayerState player, EdictDef def)
    {
        return Of(player, def) >= def.Threshold;
    }
}
