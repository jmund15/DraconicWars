namespace DraconicWars.Game.Campaign;

using System;
using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;

public readonly record struct WaveEntry(int Tick, string UnitDefId);

public readonly record struct RepeatingWave(
    int StartTick, int IntervalTicks, string UnitDefId, int EndTick);

/// <summary>
/// Authored enemy wave script → per-tick deploy commands for the Right side. Pure
/// logic: the campaign flow feeds its output into BattleSim.Advance each tick.
/// </summary>
public sealed class WaveDirector
{
    private readonly IReadOnlyList<WaveEntry> _entries;
    private readonly IReadOnlyList<RepeatingWave> _repeating;

    public WaveDirector(IEnumerable<WaveEntry> entries, IEnumerable<RepeatingWave> repeating)
    {
        _entries = entries.OrderBy(entry => entry.Tick).ToList();
        _repeating = repeating.ToList();
    }

    public IReadOnlyList<SimCommand> CommandsForTick(int tick)
    {
        List<SimCommand>? result = null;
        foreach (var entry in _entries)
        {
            if (entry.Tick > tick)
            {
                break;
            }
            if (entry.Tick == tick)
            {
                (result ??= new List<SimCommand>())
                    .Add(SimCommand.Deploy(PlayerSide.Right, entry.UnitDefId));
            }
        }

        foreach (var wave in _repeating)
        {
            var inWindow = tick >= wave.StartTick && tick <= wave.EndTick;
            if (inWindow && (tick - wave.StartTick) % wave.IntervalTicks == 0)
            {
                (result ??= new List<SimCommand>())
                    .Add(SimCommand.Deploy(PlayerSide.Right, wave.UnitDefId));
            }
        }

        return result ?? (IReadOnlyList<SimCommand>)Array.Empty<SimCommand>();
    }
}
