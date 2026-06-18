namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// sporekeep's forward escrow-conduit (roster-expansion-40.md §4): while alive it feeds its
/// owner's Dragon-summoning escrow (SummoningProgress) flat per second — gated by the same
/// EscrowStallTicks that the_tithe's siphon raises (a deliberate Mythic-vs-Mythic interaction).
/// </summary>
[TestSuite]
public class EscrowConduitTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    private static (BattleSim sim, BattleState state) AtDragonTier(UnitDef keeper)
    {
        var sim = new BattleSim(BattleConfig.Default, new[] { keeper, TestUnits.Dragon("dragon") });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Left.AscensionTier = 4;
        state.Left.EquippedDragonId = "dragon";
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, keeper.Id) });
        return (sim, state);
    }

    private static UnitDef Keeper() => TestUnits.Grunt("keeper") with
    {
        MoveSpeed = 0f, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
        ConduitEscrowPerSecond = 30f,
    };

    [TestCase]
    public void EscrowConduitFillsSummoningPassively()
    {
        var (sim, state) = AtDragonTier(Keeper());
        var before = state.Left.SummoningProgress;
        AdvanceTicks(sim, state, 30); // one second of conduit
        AssertThat(state.Left.SummoningProgress > before).IsTrue();
    }

    [TestCase]
    public void EscrowStallFreezesTheConduit()
    {
        var (sim, state) = AtDragonTier(Keeper());
        state.Left.EscrowStallTicks = 1000; // the_tithe's siphon, in effect
        var before = state.Left.SummoningProgress;
        AdvanceTicks(sim, state, 30);
        AssertThat(state.Left.SummoningProgress).IsEqual(before);
    }
}
