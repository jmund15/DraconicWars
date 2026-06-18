namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Voltherax's mana-refund-on-kill (roster-expansion-40.md §4): a projectile with
/// ManaRefundPerKill returns mana to its owner for each enemy its shot kills, capped at
/// ManaRefundCapPerShot per shot (so a deep pierce can't refund unbounded).
/// </summary>
[TestSuite]
public class ProjectileManaRefundTest
{
    // Refund mana isolated from drip + kill-bounty: enemies carry DeployCost 0 (zero bounty),
    // and we subtract a refund=0 baseline so only the refund term remains.
    private static float RefundPortion(int refundPerKill, int cap, int enemyCount)
    {
        float Gain(int refund)
        {
            var shooter = TestUnits.Grunt("shooter") with
            {
                TypeClass = TypeClass.Ranged, MoveSpeed = 0f, Range = 60f, Damage = 1000,
                ForeswingTicks = 2, BackswingTicks = 2, KnockbackCount = 0, DeployCost = 0,
                ProjectileSpeed = 4f, ProjectilePierces = true,
                ManaRefundPerKill = refund, ManaRefundCapPerShot = cap,
            };
            var enemies = Enumerable.Range(0, enemyCount).Select(i =>
                TestUnits.Grunt($"e{i}") with { MoveSpeed = 0f, MaxHp = 10, KnockbackCount = 0, DeployCost = 0 });
            var sim = new BattleSim(BattleConfig.Default, new[] { shooter }.Concat(enemies).ToArray());
            var state = sim.CreateInitialState(1UL);
            state.Left.Mana = 0f;
            state.Right.Mana = 100000f;
            var cmds = new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "shooter") };
            for (var i = 0; i < enemyCount; i++)
            {
                cmds.Add(SimCommand.Deploy(PlayerSide.Right, $"e{i}"));
            }
            sim.Advance(state, cmds);
            state.Units.First(u => u.Def.Id == "shooter").X = 2f;
            for (var i = 0; i < enemyCount; i++)
            {
                state.Units.First(u => u.Def.Id == $"e{i}").X = 8f + i; // strung in a line
            }
            var before = state.Left.Mana;
            for (var t = 0; t < 40; t++)
            {
                sim.Advance(state, SimCommand.None);
            }
            return state.Left.Mana - before;
        }

        return Gain(refundPerKill) - Gain(0);
    }

    [TestCase]
    public void PierceShotRefundsManaForEachKill()
    {
        var refund = RefundPortion(refundPerKill: 8, cap: 100, enemyCount: 2);
        AssertThat(refund > 0f).IsTrue();
    }

    [TestCase]
    public void PerShotRefundIsCapped()
    {
        var capped = RefundPortion(refundPerKill: 8, cap: 16, enemyCount: 5);
        var uncapped = RefundPortion(refundPerKill: 8, cap: 1000, enemyCount: 5);
        AssertThat(uncapped > capped).IsTrue();
    }
}
