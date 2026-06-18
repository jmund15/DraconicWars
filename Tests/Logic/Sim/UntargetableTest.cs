namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Untargetable primitive (roster-expansion-40.md §5): while SimUnit.Targetable is false
/// (the_tithe submerged) the unit is invisible to enemy targeting and immune to all damage,
/// zone-slows, shoves, and projectiles. Default true.
/// </summary>
[TestSuite]
public class UntargetableTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void UntargetableUnitTakesNoDamageThenBecomesHittable()
    {
        var attacker = TestUnits.Grunt("attacker") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 50, ForeswingTicks = 2, BackswingTicks = 2,
        };
        var target = TestUnits.Grunt("target") with { MoveSpeed = 0f, MaxHp = 2000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, target });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "attacker"),
            SimCommand.Deploy(PlayerSide.Right, "target"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;

        state.Units[1].Targetable = false;
        AdvanceTicks(sim, state, 40);
        AssertThat(state.Units[1].Hp).IsEqual(state.Units[1].Def.MaxHp);

        state.Units[1].Targetable = true;
        AdvanceTicks(sim, state, 40);
        AssertThat(state.Units[1].Hp < state.Units[1].Def.MaxHp).IsTrue();
    }

    [TestCase]
    public void ProjectilesPassThroughUntargetableBodies()
    {
        var shooter = TestUnits.Grunt("shooter") with
        {
            TypeClass = TypeClass.Ranged, MoveSpeed = 0f, Range = 30f, Damage = 40,
            ForeswingTicks = 2, BackswingTicks = 2, KnockbackCount = 0, ProjectileSpeed = 2f,
        };
        var blocker = TestUnits.Grunt("blocker") with { MoveSpeed = 0f, MaxHp = 5000, KnockbackCount = 0 };
        var realTarget = TestUnits.Grunt("realTarget") with { MoveSpeed = 0f, MaxHp = 5000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { shooter, blocker, realTarget });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "shooter"),
            SimCommand.Deploy(PlayerSide.Right, "blocker"),
            SimCommand.Deploy(PlayerSide.Right, "realTarget"),
        });
        state.Units[0].X = 4f;
        state.Units[1].X = 8f;
        state.Units[2].X = 12f;
        state.Units[1].Targetable = false; // phased out — projectiles ignore it

        AdvanceTicks(sim, state, 80);

        AssertThat(state.Units[1].Hp).IsEqual(state.Units[1].Def.MaxHp);
        AssertThat(state.Units[2].Hp < state.Units[2].Def.MaxHp).IsTrue();
    }
}
