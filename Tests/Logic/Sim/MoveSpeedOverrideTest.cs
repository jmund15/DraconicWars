namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Per-instance MoveSpeed override (roster-expansion-40.md §5): UnitDef is an immutable
/// shared record, so a kit that changes one unit's pace (plaguecharger's dismount) needs
/// per-SimUnit state. Null = fall back to Def.MoveSpeed.
/// </summary>
[TestSuite]
public class MoveSpeedOverrideTest
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
    public void ZeroOverrideHaltsAnOtherwiseMovingUnit()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var unit = state.Units[0];

        AdvanceTicks(sim, state, 5);
        AssertThat(unit.X > 0f).IsTrue();

        unit.MoveSpeedOverride = 0f;
        var haltedX = unit.X;
        AdvanceTicks(sim, state, 10);
        AssertThat(unit.X).IsEqual(haltedX);
    }

    [TestCase]
    public void HigherOverrideCoversMoreGroundThanDefSpeed()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var unit = state.Units[0];
        unit.MoveSpeedOverride = TestUnits.Grunt().MoveSpeed * 3f;
        var startX = unit.X;

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate); // one second

        AssertThat(unit.X - startX > TestUnits.Grunt().MoveSpeed * 2f).IsTrue();
    }
}
