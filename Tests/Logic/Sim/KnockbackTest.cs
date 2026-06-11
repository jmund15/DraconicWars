namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class KnockbackTest
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
    public void CrossingHpThresholdKnocksUnitBackTowardItsOwnSide()
    {
        // Defender: 100 HP, KB count 3 -> thresholds at 66.7 and 33.3 HP.
        // A 40-damage hit (100 -> 60) crosses the first threshold.
        var biter = TestUnits.FastBiter();
        var (sim, state) = CreateBattle(biter, TestUnits.Grunt(id: "pillar", moveSpeed: 0f));
        var attacker = Spawn(sim, state, PlayerSide.Left, "biter", 10f);
        var defender = Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;
        var defenderStartX = defender.X;

        AdvanceTicks(sim, state, biter.ForeswingTicks + 1);

        AssertThat(defender.Hp).IsEqual(60);
        AssertThat(defender.X > defenderStartX).IsTrue();
    }

    [TestCase]
    public void IFramesBlockDamageAfterKnockback()
    {
        var biter = TestUnits.FastBiter();
        var (sim, state) = CreateBattle(biter, TestUnits.Grunt(id: "pillar", moveSpeed: 0f));
        var attacker = Spawn(sim, state, PlayerSide.Left, "biter", 10f);
        var defender = Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;

        AdvanceTicks(sim, state, biter.ForeswingTicks + 1);
        AssertThat(defender.Hp).IsEqual(60);
        var hpAfterKnockback = defender.Hp;

        // Immediately damage again while i-frames are active: pin the defender back
        // into attack range and tick once with a direct hit attempt.
        defender.X = attacker.X + 0.5f;
        sim._TestDealDamage(state, attacker, defender);
        AssertThat(defender.Hp).IsEqual(hpAfterKnockback);
    }

    [TestCase]
    public void CrossingTwoThresholdsInOneHitCausesSingleKnockback()
    {
        // 80 damage (100 -> 20) crosses both thresholds; distance must equal ONE knockback.
        var heavy = TestUnits.FastBiter(id: "heavy") with { Damage = 80 };
        var light = TestUnits.FastBiter(id: "light") with { Damage = 40 };
        var pillar = TestUnits.Grunt(id: "pillar", moveSpeed: 0f);

        var (simA, stateA) = CreateBattle(heavy, pillar);
        var attackerA = Spawn(simA, stateA, PlayerSide.Left, "heavy", 10f);
        var defenderA = Spawn(simA, stateA, PlayerSide.Right, "pillar", 10.5f);
        attackerA.X = 10f;
        var startA = defenderA.X;
        AdvanceTicks(simA, stateA, heavy.ForeswingTicks + 1);
        var pushA = defenderA.X - startA;

        var (simB, stateB) = CreateBattle(light, pillar);
        var attackerB = Spawn(simB, stateB, PlayerSide.Left, "light", 10f);
        var defenderB = Spawn(simB, stateB, PlayerSide.Right, "pillar", 10.5f);
        attackerB.X = 10f;
        var startB = defenderB.X;
        AdvanceTicks(simB, stateB, light.ForeswingTicks + 1);
        var pushB = defenderB.X - startB;

        AssertThat(pushA).IsEqualApprox(pushB, 0.001f);
    }

    [TestCase]
    public void KnockbackDuringForeswingCancelsThePendingHit()
    {
        // Slow attacker (foreswing 8, damage 50) vs fast biter (foreswing 2, damage 40).
        // The biter's hit lands first, knocks the slow attacker back mid-foreswing,
        // and the slow attacker's pending hit must never land.
        var slow = TestUnits.Grunt(id: "slow") with { ForeswingTicks = 8, Damage = 50 };
        var biter = TestUnits.FastBiter();
        var (sim, state) = CreateBattle(slow, biter);
        var slowUnit = Spawn(sim, state, PlayerSide.Left, "slow", 10f);
        var biterUnit = Spawn(sim, state, PlayerSide.Right, "biter", 10.5f);
        slowUnit.X = 10f;

        AdvanceTicks(sim, state, 12);

        AssertThat(biterUnit.Hp).IsEqual(biterUnit.Def.MaxHp);
        AssertThat(slowUnit.Hp).IsEqual(60);
    }
}
