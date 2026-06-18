namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Economy-attack hooks (roster-expansion-40.md §5): a contact can drain the enemy's mana
/// wallet and stall their Dragon-summoning escrow (the_tithe's signature). EscrowStallTicks
/// freezes TryChannelMana and ticks down each tick.
/// </summary>
[TestSuite]
public class EconomyHooksTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    private static (BattleSim sim, BattleState state) Engaged(UnitDef attacker)
    {
        var dummy = TestUnits.Grunt("dummy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, dummy });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, attacker.Id),
            SimCommand.Deploy(PlayerSide.Right, "dummy"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;
        return (sim, state);
    }

    private static UnitDef Attacker(string id) => TestUnits.Grunt(id) with
    {
        MoveSpeed = 0f, Range = 30f, Damage = 5, ForeswingTicks = 2, BackswingTicks = 2,
    };

    [TestCase]
    public void DrainManaOnContactReducesEnemyWallet()
    {
        var (sim, state) = Engaged(Attacker("drainer") with { DrainManaOnContact = 200 });
        AdvanceTicks(sim, state, 40);
        AssertThat(state.Right.Mana < 5000f).IsTrue();
    }

    [TestCase]
    public void EscrowStallOnContactStallsTheEnemy()
    {
        var (sim, state) = Engaged(Attacker("staller") with { EscrowStallOnContact = 150 });
        AdvanceTicks(sim, state, 20);
        AssertThat(state.Right.EscrowStallTicks > 0).IsTrue();
    }

    [TestCase]
    public void EscrowStallTicksDecrementEachTick()
    {
        var sim = new BattleSim(BattleConfig.Default, new[] { TestUnits.Grunt() });
        var state = sim.CreateInitialState(1UL);
        state.Left.EscrowStallTicks = 5;

        AdvanceTicks(sim, state, 3);

        AssertThat(state.Left.EscrowStallTicks).IsEqual(2);
    }

    [TestCase]
    public void EscrowStallBlocksDragonSummoning()
    {
        var sim = new BattleSim(
            BattleConfig.Default, new[] { TestUnits.Grunt(), TestUnits.Dragon("dragon") });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Left.AscensionTier = 4;
        state.Left.EquippedDragonId = "dragon";

        state.Left.EscrowStallTicks = 60;
        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, 500) });
        AssertThat(state.Left.SummoningProgress).IsEqual(0f);

        state.Left.EscrowStallTicks = 0;
        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, 500) });
        AssertThat(state.Left.SummoningProgress > 0f).IsTrue();
    }
}
