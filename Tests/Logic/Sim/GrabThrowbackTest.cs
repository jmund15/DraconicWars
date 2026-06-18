namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Grab-throwback primitive (crag_tyrant / Roc — "Return To Sender"): on contact the unit
/// seizes the FRONTMOST non-Unstaggerable enemy (the one most advanced toward the grabber's
/// spire), teleports it back toward its OWN spire by GrabThrowDistance, and self-stuns it
/// (StunTicks). Modelled as teleport + self-stun — no cross-unit ownership link.
/// </summary>
[TestSuite]
public class GrabThrowbackTest
{
    private static (BattleSim sim, BattleState state) Arena(params UnitDef[] defs)
    {
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        return (sim, state);
    }

    [TestCase]
    public void RocThrowsBreakthroughEnemyBackTowardItsSpireAndStunsIt()
    {
        var roc = TestUnits.Grunt("roc") with
        {
            MoveSpeed = 0f, Range = 40f, Damage = 5, ForeswingTicks = 2, BackswingTicks = 2,
            CanTargetGround = true, CanTargetAir = true,
            GrabThrowDistance = 8f, GrabStunTicks = 20,
        };
        var runner = TestUnits.Grunt("runner") with { MoveSpeed = 0f, MaxHp = 5000, KnockbackCount = 0 };
        var (sim, state) = Arena(roc, runner);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "roc"),
            SimCommand.Deploy(PlayerSide.Right, "runner"),
        });
        var rocUnit = state.Units[0];
        var runnerUnit = state.Units[1];
        rocUnit.X = 10f;
        runnerUnit.X = 4f; // broken THROUGH the Roc, advancing on the Left spire at x=0

        // Advance until the throw fires; on that tick the runner is also stunned.
        var threw = false;
        for (var i = 0; i < 30 && !threw; i++)
        {
            var before = runnerUnit.X;
            sim.Advance(state, SimCommand.None);
            if (runnerUnit.X > before + 1f)
            {
                threw = true;
                AssertThat(runnerUnit.StunTicks > 0).IsTrue();
            }
        }
        AssertThat(threw).IsTrue();
        // Net effect: shoved back toward its own (Right) spire, away from x=0.
        AssertThat(runnerUnit.X > 4f).IsTrue();
    }

    [TestCase]
    public void UnstaggerableEnemyIsNotGrabbed()
    {
        var roc = TestUnits.Grunt("roc") with
        {
            MoveSpeed = 0f, Range = 40f, Damage = 5, ForeswingTicks = 2, BackswingTicks = 2,
            GrabThrowDistance = 8f, GrabStunTicks = 20,
        };
        var tank = TestUnits.Grunt("tank") with
        {
            MoveSpeed = 0f, MaxHp = 5000, KnockbackCount = 0, Unstaggerable = true,
        };
        var (sim, state) = Arena(roc, tank);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "roc"),
            SimCommand.Deploy(PlayerSide.Right, "tank"),
        });
        state.Units[0].X = 10f;
        state.Units[1].X = 4f;

        for (var i = 0; i < 30; i++)
        {
            sim.Advance(state, SimCommand.None);
        }

        // Unstaggerable: never teleported, never stunned.
        AssertThat(state.Units[1].X).IsEqual(4f);
        AssertThat(state.Units[1].StunTicks).IsEqual(0);
    }
}
