namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// tempest_choir's two support auras (roster-expansion-40.md §4):
/// (1) living mana-conduit — each live choir adds ConduitManaPerSecond to its owner's
///     wallet, but only ConduitContributeCap of them count; and
/// (2) haste-halo — allies within HasteHaloRadius attack faster (HasteHaloSpeedPct fed
///     into the shared ScaledTicks attack-speed channel).
/// </summary>
[TestSuite]
public class ConduitHaloTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    private static UnitDef Choir(string id) => TestUnits.Grunt(id) with
    {
        MoveSpeed = 0f, DeployCost = 0, DeployCooldownTicks = 0, CanTargetGround = false,
        CanTargetAir = false, ConduitManaPerSecond = 4f, ConduitContributeCap = 2,
    };

    private static float ConduitGainOver(int choirCount, int ticks)
    {
        var defs = Enumerable.Range(0, choirCount).Select(i => Choir($"choir{i}")).ToArray();
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 0f;
        sim.Advance(state,
            defs.Select(d => SimCommand.Deploy(PlayerSide.Left, d.Id)).ToList());
        var before = state.Left.Mana;
        AdvanceTicks(sim, state, ticks);
        return state.Left.Mana - before;
    }

    [TestCase]
    public void SecondChoirAddsManaButTheThirdIsCapped()
    {
        var g1 = ConduitGainOver(1, 30);
        var g2 = ConduitGainOver(2, 30);
        var g3 = ConduitGainOver(3, 30);

        // The second choir contributes; the third is over the cap and adds nothing.
        AssertThat(g2 > g1).IsTrue();
        AssertThat(System.MathF.Abs(g3 - g2) < 0.001f).IsTrue();
    }

    private static int EnemyDamageOver(bool withChoir, int ticks)
    {
        // A non-Storm attacker (so only the halo, not StormAttackSpeedPct, speeds it up)
        // with a long swing, chipping a stationary dummy.
        var attacker = TestUnits.Grunt("atk") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 10, ForeswingTicks = 20, BackswingTicks = 20,
            KnockbackCount = 0,
        };
        var dummy = TestUnits.Grunt("dummy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var choir = TestUnits.Grunt("choir") with
        {
            MoveSpeed = 0f, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
            HasteHaloRadius = 6f, HasteHaloSpeedPct = 0.25f,
        };
        var defs = withChoir ? new[] { attacker, dummy, choir } : new[] { attacker, dummy };
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        var cmds = new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "atk"),
            SimCommand.Deploy(PlayerSide.Right, "dummy"),
        };
        if (withChoir)
        {
            cmds.Add(SimCommand.Deploy(PlayerSide.Left, "choir"));
        }
        sim.Advance(state, cmds);
        var atkUnit = state.Units.First(u => u.Def.Id == "atk");
        var dummyUnit = state.Units.First(u => u.Def.Id == "dummy");
        atkUnit.X = 5f;
        dummyUnit.X = 8f;
        if (withChoir)
        {
            state.Units.First(u => u.Def.Id == "choir").X = 4f;
        }
        AdvanceTicks(sim, state, ticks);
        return dummyUnit.Def.MaxHp - dummyUnit.Hp;
    }

    [TestCase]
    public void HasteHaloMakesNearbyAllyAttackFaster()
    {
        var plain = EnemyDamageOver(withChoir: false, ticks: 180);
        var hasted = EnemyDamageOver(withChoir: true, ticks: 180);

        // Faster swings over the same window => strictly more damage landed.
        AssertThat(hasted > plain).IsTrue();
    }
}
