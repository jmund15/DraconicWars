namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BattleSimDeterminismTest
{
    private static ulong RunScriptedBattle(ulong seed)
    {
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { TestUnits.Grunt(), TestUnits.Archer(), TestUnits.Whelp() });
        var state = sim.CreateInitialState(seed);

        for (var tick = 0; tick < 900; tick++)
        {
            var commands = new List<SimCommand>();
            if (tick % 90 == 0)
            {
                commands.Add(SimCommand.Deploy(PlayerSide.Left, "grunt"));
                commands.Add(SimCommand.Deploy(PlayerSide.Right, "archer"));
            }
            if (tick % 150 == 0)
            {
                commands.Add(SimCommand.Deploy(PlayerSide.Left, "whelp"));
            }
            sim.Advance(state, commands);
        }

        return state.StateHash();
    }

    [TestCase]
    public void SameSeedAndCommands_ProduceIdenticalStateHash()
    {
        AssertThat(RunScriptedBattle(777UL)).IsEqual(RunScriptedBattle(777UL));
    }

    [TestCase]
    public void DifferentSeeds_StillIdenticalWhenNoRngConsumed()
    {
        // Movement/economy consume no randomness; the hash covers positions, HP,
        // mana, and tick — so scripted identical commands must match across seeds.
        AssertThat(RunScriptedBattle(1UL)).IsEqual(RunScriptedBattle(2UL));
    }
}
