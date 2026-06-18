namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// PrefersAirTarget primitive (glide_manta — "Nothing Flies Here"): an interceptor with
/// this flag strikes the nearest AIR-stratum enemy in band, even when a ground enemy is
/// closer. Falls back to the normal nearest target when no air target is in band.
/// </summary>
[TestSuite]
public class PrefersAirTargetTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void InterceptorStrikesFartherAirEnemyOverNearerGround()
    {
        var manta = TestUnits.Grunt("manta") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 50, ForeswingTicks = 2, BackswingTicks = 2,
            CanTargetGround = true, CanTargetAir = true, PrefersAirTarget = true,
        };
        var ground = TestUnits.Grunt("ground") with { MoveSpeed = 0f, MaxHp = 4000, KnockbackCount = 0 };
        var air = TestUnits.Whelp("air") with { MoveSpeed = 0f, MaxHp = 4000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { manta, ground, air });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "manta"),
            SimCommand.Deploy(PlayerSide.Right, "ground"),
            SimCommand.Deploy(PlayerSide.Right, "air"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;  // ground enemy — NEARER
        state.Units[2].X = 14f; // air enemy — FARTHER

        AdvanceTicks(sim, state, 60);

        // The air enemy bleeds despite being farther; the nearer ground enemy is untouched.
        AssertThat(state.Units[2].Hp < state.Units[2].Def.MaxHp).IsTrue();
        AssertThat(state.Units[1].Hp).IsEqual(state.Units[1].Def.MaxHp);
    }

    [TestCase]
    public void InterceptorFallsBackToGroundWhenNoAirInBand()
    {
        var manta = TestUnits.Grunt("manta") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 50, ForeswingTicks = 2, BackswingTicks = 2,
            CanTargetGround = true, CanTargetAir = true, PrefersAirTarget = true,
        };
        var ground = TestUnits.Grunt("ground") with { MoveSpeed = 0f, MaxHp = 4000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { manta, ground });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "manta"),
            SimCommand.Deploy(PlayerSide.Right, "ground"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;

        AdvanceTicks(sim, state, 60);

        AssertThat(state.Units[1].Hp < state.Units[1].Def.MaxHp).IsTrue();
    }
}
