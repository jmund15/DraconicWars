namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// StunTicks primitive (roster-expansion-40.md §5): self-contained suppression — while
/// &gt;0 a unit neither attacks nor moves; ticks down like SlowTicks. Backs freeze,
/// stun-dive, and the teleport+self-stun relocate pattern (no cross-unit ownership).
/// </summary>
[TestSuite]
public class StunTicksTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(BattleConfig.Default, new[] { TestUnits.Grunt() });
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
    public void StunnedUnitDoesNotMoveAndTicksDown()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var unit = state.Units[0];

        AdvanceTicks(sim, state, 5);
        AssertThat(unit.X > 0f).IsTrue(); // free mover advanced from the left spire

        unit.StunTicks = 10;
        var frozenX = unit.X;
        AdvanceTicks(sim, state, 3); // 10 -> 7, always >0, so frozen the whole window

        AssertThat(unit.X).IsEqual(frozenX);
        AssertThat(unit.StunTicks).IsEqual(7);
    }

    [TestCase]
    public void StunExpiryLetsTheUnitResumeMoving()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var unit = state.Units[0];
        AdvanceTicks(sim, state, 5);

        unit.StunTicks = 2;
        var frozenX = unit.X;
        AdvanceTicks(sim, state, 1); // 2 -> 1, still stunned: frozen
        AssertThat(unit.X).IsEqual(frozenX);

        AdvanceTicks(sim, state, 2); // 1 -> 0 (resumes), then a clear moving tick
        AssertThat(unit.StunTicks).IsEqual(0);
        AssertThat(unit.X > frozenX).IsTrue();
    }

    [TestCase]
    public void StunnedUnitDealsNoDamageWhileSuppressed()
    {
        var sim = new BattleSim(BattleConfig.Default, new[] { TestUnits.Grunt() });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 1000f;
        state.Right.Mana = 1000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "grunt"),
            SimCommand.Deploy(PlayerSide.Right, "grunt"),
        });
        AdvanceTicks(sim, state, 270); // engaged, both alive (per BattleSimMovementTest)

        var left = state.Units[0];
        var right = state.Units[1];
        AssertThat(left.IsAlive && right.IsAlive).IsTrue();

        left.StunTicks = 60;
        var rightHpBefore = right.Hp;
        AdvanceTicks(sim, state, 30); // left stunned the whole window

        AssertThat(right.Hp).IsEqual(rightHpBefore); // suppressed: left never landed a hit
    }
}
