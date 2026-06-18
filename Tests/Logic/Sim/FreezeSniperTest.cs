namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// rimecoil's freeze-sniper kit (roster-expansion-40.md §4): its real projectile freezes
/// the body it hits (ProjectileFreezeTicks -> StunTicks), and it deals BonusVsImpairedPct
/// extra against targets already frozen or slowed.
/// </summary>
[TestSuite]
public class FreezeSniperTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void ProjectileFreezesTheBodyItHits()
    {
        var shooter = TestUnits.Grunt("shooter") with
        {
            TypeClass = TypeClass.Sniper, MoveSpeed = 0f, Range = 30f, Damage = 5,
            ForeswingTicks = 2, BackswingTicks = 2, KnockbackCount = 0,
            ProjectileSpeed = 4f, ProjectileFreezeTicks = 24,
        };
        var target = TestUnits.Grunt("target") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { shooter, target });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "shooter"),
            SimCommand.Deploy(PlayerSide.Right, "target"),
        });
        state.Units[0].X = 4f;
        state.Units[1].X = 10f;
        var targetUnit = state.Units.First(u => u.Def.Id == "target");

        AdvanceTicks(sim, state, 12); // fire + travel + hit

        AssertThat(targetUnit.StunTicks > 0).IsTrue();
    }

    private static int DamageVs(bool preImpaired)
    {
        var attacker = TestUnits.Grunt("attacker") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 100, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, BonusVsImpairedPct = 0.5f,
        };
        var target = TestUnits.Grunt("target") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, target });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "attacker"),
            SimCommand.Deploy(PlayerSide.Right, "target"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;
        var targetUnit = state.Units.First(u => u.Def.Id == "target");
        if (preImpaired)
        {
            targetUnit.StunTicks = 1000; // held frozen across the window
        }
        var start = targetUnit.Hp;
        AdvanceTicks(sim, state, 8); // one attack cycle
        if (preImpaired)
        {
            targetUnit.StunTicks = 1000; // keep it frozen (don't let it tick out and act)
        }
        return start - targetUnit.Hp;
    }

    [TestCase]
    public void DealsBonusAgainstImpairedTargets()
    {
        var healthy = DamageVs(preImpaired: false);
        var frozen = DamageVs(preImpaired: true);
        AssertThat(frozen > healthy).IsTrue();
    }
}
