namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BattleSimEconomyTest
{
    private static BattleSim CreateSim(params UnitDef[] defs)
    {
        var config = BattleConfig.Default;
        return new BattleSim(config, defs);
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void ManaDripsAtConfiguredRate()
    {
        var sim = CreateSim(TestUnits.Grunt());
        var state = sim.CreateInitialState(1UL);
        var startingMana = state.Left.Mana;

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate);

        var expected = startingMana + BattleConfig.Default.BaseDripPerSecond;
        AssertThat(state.Left.Mana).IsEqualApprox(expected, 0.01f);
        AssertThat(state.Right.Mana).IsEqualApprox(expected, 0.01f);
    }

    [TestCase]
    public void ManaClampsAtWalletCap()
    {
        var sim = CreateSim(TestUnits.Grunt());
        var state = sim.CreateInitialState(1UL);

        var ticksToOverfill = (int)(BattleConfig.Default.StartingWalletCap
            / BattleConfig.Default.BaseDripPerSecond
            * BattleConfig.Default.TickRate) + 200;
        AdvanceTicks(sim, state, ticksToOverfill);

        AssertThat(state.Left.Mana).IsEqualApprox(BattleConfig.Default.StartingWalletCap, 0.01f);
    }

    [TestCase]
    public void DeploySpendsManaAndSpawnsUnitAtOwnSpire()
    {
        var grunt = TestUnits.Grunt(deployCost: 50);
        var sim = CreateSim(grunt);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 80f;

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });

        AssertThat(state.Units.Count).IsEqual(1);
        AssertThat(state.Left.Mana).IsEqualApprox(80f - 50f + BattleConfig.Default.DripPerTick, 0.01f);
        var unit = state.Units[0];
        AssertThat(unit.Side).IsEqual(PlayerSide.Left);
        AssertThat(unit.Hp).IsEqual(grunt.MaxHp);
        AssertThat(unit.X < BattleConfig.Default.LaneLength * 0.25f).IsTrue();
    }

    [TestCase]
    public void DeployRejectedWhenUnaffordable()
    {
        var sim = CreateSim(TestUnits.Grunt(deployCost: 500));
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 10f;

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });

        AssertThat(state.Units.Count).IsEqual(0);
        AssertThat(state.Left.Mana).IsEqualApprox(10f + BattleConfig.Default.DripPerTick, 0.01f);
    }

    [TestCase]
    public void DeployRejectedDuringCooldown_ThenAllowedAfterExpiry()
    {
        var grunt = TestUnits.Grunt(deployCost: 10, deployCooldownTicks: 30);
        var sim = CreateSim(grunt);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 300f;

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        AssertThat(state.Units.Count).IsEqual(1);

        AdvanceTicks(sim, state, 30);
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        AssertThat(state.Units.Count).IsEqual(2);
    }

    [TestCase]
    public void DeployForUnknownUnitIsIgnored()
    {
        var sim = CreateSim(TestUnits.Grunt());
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 300f;

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "nonexistent") });

        AssertThat(state.Units.Count).IsEqual(0);
    }
}
