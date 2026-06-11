namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class CombatTest
{
    private static (BattleSim sim, BattleState state) CreateBattle(params UnitDef[] defs)
    {
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 10000f;
        state.Right.Mana = 10000f;
        return (sim, state);
    }

    private static SimUnit Spawn(
        BattleSim sim, BattleState state, PlayerSide side, string defId, float x)
    {
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(side, defId) });
        var unit = state.Units[^1];
        unit.X = x;
        return unit;
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void DamageLandsAtForeswingContactTick_NotBefore()
    {
        var grunt = TestUnits.Grunt();
        var (sim, state) = CreateBattle(grunt, TestUnits.Grunt(id: "pillar", moveSpeed: 0f));
        var attacker = Spawn(sim, state, PlayerSide.Left, "grunt", 10f);
        var defender = Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;

        AdvanceTicks(sim, state, grunt.ForeswingTicks - 1);
        AssertThat(defender.Hp).IsEqual(defender.Def.MaxHp);

        AdvanceTicks(sim, state, 2);
        AssertThat(defender.Hp).IsEqual(defender.Def.MaxHp - grunt.Damage);
    }

    [TestCase]
    public void BackswingDelaysSecondHit()
    {
        var grunt = TestUnits.Grunt();
        var (sim, state) = CreateBattle(grunt, TestUnits.Grunt(id: "pillar", moveSpeed: 0f));
        var attacker = Spawn(sim, state, PlayerSide.Left, "grunt", 10f);
        var defender = Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;

        var oneCycle = grunt.ForeswingTicks + grunt.BackswingTicks;
        AdvanceTicks(sim, state, oneCycle + grunt.ForeswingTicks - 1);
        AssertThat(defender.Hp).IsEqual(defender.Def.MaxHp - grunt.Damage);

        AdvanceTicks(sim, state, 2);
        AssertThat(defender.Hp).IsEqual(defender.Def.MaxHp - 2 * grunt.Damage);
    }

    [TestCase]
    public void SingleTargetAttackHitsOnlyNearestEnemy()
    {
        // Distinct def ids: a same-id second deploy is rejected by the per-unit cooldown.
        var (sim, state) = CreateBattle(
            TestUnits.Archer(),
            TestUnits.Grunt(id: "pillar", moveSpeed: 0f),
            TestUnits.Grunt(id: "pillar2", moveSpeed: 0f));
        var archer = Spawn(sim, state, PlayerSide.Left, "archer", 10f);
        var near = Spawn(sim, state, PlayerSide.Right, "pillar", 12f);
        var far = Spawn(sim, state, PlayerSide.Right, "pillar2", 14f);
        archer.X = 10f;

        AdvanceTicks(sim, state, TestUnits.Archer().ForeswingTicks + 1);

        AssertThat(near.Hp < near.Def.MaxHp).IsTrue();
        AssertThat(far.Hp).IsEqual(far.Def.MaxHp);
    }

    [TestCase]
    public void AreaAttackHitsAllEnemiesInRange()
    {
        var mage = TestUnits.Mage();
        var (sim, state) = CreateBattle(
            mage,
            TestUnits.Grunt(id: "pillar", moveSpeed: 0f),
            TestUnits.Grunt(id: "pillar2", moveSpeed: 0f));
        var caster = Spawn(sim, state, PlayerSide.Left, "mage", 10f);
        var near = Spawn(sim, state, PlayerSide.Right, "pillar", 12f);
        var far = Spawn(sim, state, PlayerSide.Right, "pillar2", 14f);
        caster.X = 10f;

        AdvanceTicks(sim, state, mage.ForeswingTicks + 1);

        AssertThat(near.Hp).IsEqual(near.Def.MaxHp - mage.Damage);
        AssertThat(far.Hp).IsEqual(far.Def.MaxHp - mage.Damage);
    }

    [TestCase]
    public void SniperCannotHitEnemyInsideBlindSpot()
    {
        var sniper = TestUnits.Sniper();
        var (sim, state) = CreateBattle(sniper, TestUnits.Grunt(id: "pillar", moveSpeed: 0f));
        var shooter = Spawn(sim, state, PlayerSide.Left, "sniper", 10f);
        var tooClose = Spawn(sim, state, PlayerSide.Right, "pillar", 11f);
        shooter.X = 10f;

        AdvanceTicks(sim, state, (sniper.ForeswingTicks + sniper.BackswingTicks) * 3);

        AssertThat(tooClose.Hp).IsEqual(tooClose.Def.MaxHp);
    }

    [TestCase]
    public void DeadUnitsAreRemovedFromTheLane()
    {
        var biter = TestUnits.FastBiter();
        var (sim, state) = CreateBattle(
            biter with { Damage = 100 }, TestUnits.Grunt(id: "pillar", moveSpeed: 0f));
        var attacker = Spawn(sim, state, PlayerSide.Left, "biter", 10f);
        Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;

        AdvanceTicks(sim, state, biter.ForeswingTicks + 2);

        AssertThat(state.Units.Count).IsEqual(1);
        AssertThat(state.Units.All(u => u.Side == PlayerSide.Left)).IsTrue();
    }

    [TestCase]
    public void UnitAttacksSpireWhenNoEnemiesRemain()
    {
        var grunt = TestUnits.Grunt();
        var (sim, state) = CreateBattle(grunt);
        var attacker = Spawn(sim, state, PlayerSide.Left, "grunt", 0f);
        attacker.X = BattleConfig.Default.LaneLength - 0.5f;

        var spireBefore = state.RightSpireHp;
        AdvanceTicks(sim, state, grunt.ForeswingTicks + 1);

        AssertThat(state.RightSpireHp).IsEqualApprox(spireBefore - grunt.Damage, 0.01f);
    }

    [TestCase]
    public void SpireDestructionEndsTheBattle()
    {
        var grunt = TestUnits.Grunt();
        var (sim, state) = CreateBattle(grunt);
        var attacker = Spawn(sim, state, PlayerSide.Left, "grunt", 0f);
        attacker.X = BattleConfig.Default.LaneLength - 0.5f;
        state.RightSpireHp = grunt.Damage;

        AdvanceTicks(sim, state, grunt.ForeswingTicks + 1);

        AssertThat(state.Outcome).IsEqual(BattleOutcome.LeftVictory);
        var tickAtEnd = state.Tick;
        AdvanceTicks(sim, state, 10);
        AssertThat(state.Tick).IsEqual(tickAtEnd);
    }
}
