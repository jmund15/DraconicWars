namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BattleSimMovementTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(
            BattleConfig.Default,
            new[]
            {
                TestUnits.Grunt(),
                TestUnits.Archer(),
                TestUnits.Whelp(),
                TestUnits.Grunt(id: "pillar", moveSpeed: 0f),
            });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 1000f;
        state.Right.Mana = 1000f;
        return (sim, state);
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void UnitAdvancesTowardEnemySpireAtMoveSpeed()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var startX = state.Units[0].X;

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate);

        var expected = startX + TestUnits.Grunt().MoveSpeed;
        AssertThat(state.Units[0].X).IsEqualApprox(expected, 0.05f);
    }

    [TestCase]
    public void OpposingSidesMoveTowardEachOther()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "grunt"),
            SimCommand.Deploy(PlayerSide.Right, "grunt"),
        });
        var leftStart = state.Units[0].X;
        var rightStart = state.Units[1].X;

        AdvanceTicks(sim, state, 30);

        AssertThat(state.Units[0].X > leftStart).IsTrue();
        AssertThat(state.Units[1].X < rightStart).IsTrue();
    }

    [TestCase]
    public void UnitStopsAtStandingRangeOfNearestTargetableEnemy()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "grunt"),
            SimCommand.Deploy(PlayerSide.Right, "grunt"),
        });

        AdvanceTicks(sim, state, 2000);

        var left = state.Units[0];
        var right = state.Units[1];
        var gap = right.X - left.X;
        AssertThat(gap >= 0f).IsTrue();
        AssertThat(gap <= TestUnits.Grunt().Range + 0.2f).IsTrue();
    }

    [TestCase]
    public void GroundMeleeDoesNotStopForAerialEnemy()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "grunt"),
            SimCommand.Deploy(PlayerSide.Right, "whelp"),
        });

        AdvanceTicks(sim, state, 600);

        var grunt = state.Units[0];
        var laneEnd = BattleConfig.Default.LaneLength;
        AssertThat(grunt.X > laneEnd * 0.5f).IsTrue();
    }

    [TestCase]
    public void AerialStopsAtRangeOfStationaryGroundEnemy()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "whelp"),
            SimCommand.Deploy(PlayerSide.Right, "pillar"),
        });

        AdvanceTicks(sim, state, 2000);

        var whelp = state.Units[0];
        var pillar = state.Units[1];
        var gap = pillar.X - whelp.X;
        AssertThat(gap >= 0f).IsTrue();
        AssertThat(gap <= TestUnits.Whelp().Range + 0.2f).IsTrue();
    }

    [TestCase]
    public void UnitStopsAtEnemySpireRange()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });

        AdvanceTicks(sim, state, 4000);

        var unit = state.Units[0];
        var laneEnd = BattleConfig.Default.LaneLength;
        AssertThat(unit.X <= laneEnd).IsTrue();
        var distanceToSpire = laneEnd - unit.X;
        AssertThat(distanceToSpire <= TestUnits.Grunt().Range + 0.2f).IsTrue();
    }
}
