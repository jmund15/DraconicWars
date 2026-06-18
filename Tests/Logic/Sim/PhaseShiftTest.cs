namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Periodic phase-shift primitive (spore_wisp — "Can't Swat Smoke"): a unit with
/// PhaseCadenceTicks &gt; 0 cycles through a solid window then goes untargetable for
/// PhaseDurationTicks of every cadence period. Reuses the Targetable gate, so while
/// phased it is immune to damage/targeting (proven by UntargetableTest) yet still acts.
/// </summary>
[TestSuite]
public class PhaseShiftTest
{
    [TestCase]
    public void PhasingUnitStartsSolidThenIsUntargetableExactlyDurationPerCycle()
    {
        var wisp = TestUnits.Grunt("wisp") with
        {
            MoveSpeed = 0f, PhaseCadenceTicks = 10, PhaseDurationTicks = 4,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { wisp });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "wisp") });
        var u = state.Units[0];

        // Begins solid (does not phase out the instant it deploys).
        AssertThat(u.Targetable).IsTrue();

        // Over exactly one cadence period, the phased (untargetable) ticks equal the
        // declared duration — independent of where in the cycle we sample.
        var phased = 0;
        for (var i = 0; i < 10; i++)
        {
            sim.Advance(state, SimCommand.None);
            if (!u.Targetable)
            {
                phased++;
            }
        }
        AssertThat(phased).IsEqual(4);
    }

    [TestCase]
    public void NonPhasingUnitStaysTargetable()
    {
        var grunt = TestUnits.Grunt("grunt") with { MoveSpeed = 0f };
        var sim = new BattleSim(BattleConfig.Default, new[] { grunt });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var u = state.Units[0];

        for (var i = 0; i < 20; i++)
        {
            sim.Advance(state, SimCommand.None);
            AssertThat(u.Targetable).IsTrue();
        }
    }
}
