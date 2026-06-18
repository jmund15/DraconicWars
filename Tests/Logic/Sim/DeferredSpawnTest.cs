namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Deferred-spawn queue (roster-expansion-40.md §5): a spawner births chaff at its own
/// position every SpawnCadenceTicks, capped at SpawnCap live spawns per side. Spawns are
/// queued and appended AFTER iteration (never mid-loop). Chaff carries no SpawnDefId, so it
/// never recurses (sporekeep, Sythraal).
/// </summary>
[TestSuite]
public class DeferredSpawnTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void SpawnerProducesChaffCappedAtSpawnCap()
    {
        var sapling = TestUnits.Grunt("sapling") with { MoveSpeed = 0f, MaxHp = 50 };
        var spawner = TestUnits.Grunt("spawner") with
        {
            MoveSpeed = 0f, MaxHp = 5000,
            SpawnDefId = "sapling", SpawnCadenceTicks = 10, SpawnCap = 3,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { spawner, sapling });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "spawner") });

        AdvanceTicks(sim, state, 100);

        var saplings = state.Units.Count(u => u.Def.Id == "sapling" && u.Side == PlayerSide.Left);
        AssertThat(saplings).IsEqual(3);
    }

    [TestCase]
    public void ChaffDoesNotSpawnFurtherChaff()
    {
        var sapling = TestUnits.Grunt("sapling") with { MoveSpeed = 0f, MaxHp = 50 };
        var spawner = TestUnits.Grunt("spawner") with
        {
            MoveSpeed = 0f, MaxHp = 5000,
            SpawnDefId = "sapling", SpawnCadenceTicks = 5, SpawnCap = 2,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { spawner, sapling });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "spawner") });

        AdvanceTicks(sim, state, 200);

        AssertThat(state.Units.Count(u => u.Def.Id == "sapling")).IsEqual(2);
    }
}
