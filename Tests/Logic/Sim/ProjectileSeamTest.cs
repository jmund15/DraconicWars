namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Real-projectile seam (roster-expansion-40.md §5): attacks loose a traveling shot that
/// snapshots damage at spawn and hits the first enemy body it sweeps over — body-blockable
/// (a frontline body intercepts), with optional pierce (hit all in path) + splash.
/// </summary>
[TestSuite]
public class ProjectileSeamTest
{
    private static UnitDef Shooter(bool pierces = false, float splash = 0f) =>
        TestUnits.Grunt("shooter") with
        {
            TypeClass = TypeClass.Ranged,
            MoveSpeed = 0f,
            Range = 30f,
            RangeMin = 0f,
            Damage = 20,
            ForeswingTicks = 2,
            BackswingTicks = 2,
            KnockbackCount = 0,
            ProjectileSpeed = 2f,
            ProjectilePierces = pierces,
            ProjectileSplashRadius = splash,
            CanTargetGround = true,
        };

    private static UnitDef Pillar(string id) =>
        TestUnits.Grunt(id) with { MoveSpeed = 0f, MaxHp = 5000, KnockbackCount = 0 };

    private static (BattleSim sim, BattleState state) Battle(UnitDef shooter)
    {
        var sim = new BattleSim(
            BattleConfig.Default, new[] { shooter, Pillar("near"), Pillar("far") });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        return (sim, state);
    }

    private static void Advance(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void ProjectileTravelsAndDamagesEnemyInPath()
    {
        var (sim, state) = Battle(Shooter());
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "shooter"),
            SimCommand.Deploy(PlayerSide.Right, "near"),
        });
        state.Units[0].X = 4f;
        state.Units[1].X = 10f;

        Advance(sim, state, 60);

        AssertThat(state.Units[1].Hp < state.Units[1].Def.MaxHp).IsTrue();
    }

    [TestCase]
    public void NonPierceProjectileStopsAtTheFirstBody()
    {
        var (sim, state) = Battle(Shooter());
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "shooter"),
            SimCommand.Deploy(PlayerSide.Right, "near"),
            SimCommand.Deploy(PlayerSide.Right, "far"),
        });
        state.Units[0].X = 4f;
        state.Units[1].X = 8f;  // near body
        state.Units[2].X = 12f; // far body, shielded behind the near one

        Advance(sim, state, 80);

        AssertThat(state.Units[1].Hp < state.Units[1].Def.MaxHp).IsTrue();
        AssertThat(state.Units[2].Hp).IsEqual(state.Units[2].Def.MaxHp);
    }

    [TestCase]
    public void PierceProjectilePassesThroughToHitAll()
    {
        var (sim, state) = Battle(Shooter(pierces: true));
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "shooter"),
            SimCommand.Deploy(PlayerSide.Right, "near"),
            SimCommand.Deploy(PlayerSide.Right, "far"),
        });
        state.Units[0].X = 4f;
        state.Units[1].X = 8f;
        state.Units[2].X = 12f;

        Advance(sim, state, 80);

        AssertThat(state.Units[1].Hp < state.Units[1].Def.MaxHp).IsTrue();
        AssertThat(state.Units[2].Hp < state.Units[2].Def.MaxHp).IsTrue();
    }
}
