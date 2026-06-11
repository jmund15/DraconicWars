namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BreathWrathTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { TestUnits.Grunt(), TestUnits.Grunt(id: "pillar", moveSpeed: 0f) },
            ConduitDefs.All);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Left.WalletCap = 50000f;
        state.Right.Mana = 5000f;
        state.Right.WalletCap = 50000f;
        return (sim, state);
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void BreathStartsEmptyAndRegeneratesToFull()
    {
        var (sim, state) = CreateBattle();
        AssertThat(state.Left.BreathEnergySeconds).IsEqualApprox(0f, 0.001f);

        AdvanceTicks(sim, state, BattleConfig.Default.BreathRechargeSeconds
            * BattleConfig.Default.TickRate + 5);

        AssertThat(state.Left.BreathEnergySeconds).IsEqualApprox(
            BattleConfig.Default.BreathMaxSeconds, 0.05f);
    }

    [TestCase]
    public void BreathCoilAcceleratesRegen()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "breath_coil") });
        var leftStart = state.Left.BreathEnergySeconds;
        var rightStart = state.Right.BreathEnergySeconds;

        AdvanceTicks(sim, state, 90);

        var leftGain = state.Left.BreathEnergySeconds - leftStart;
        var rightGain = state.Right.BreathEnergySeconds - rightStart;
        AssertThat(leftGain).IsEqualApprox(rightGain * 1.25f, 0.01f);
    }

    [TestCase]
    public void FiringWithoutEnergyDoesNothing()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "pillar") });
        var target = state.Units[0];
        target.X = 20f;

        sim.Advance(state, new List<SimCommand> { SimCommand.FireBreath(PlayerSide.Left, 20f) });

        AssertThat(target.Hp).IsEqual(target.Def.MaxHp);
    }

    [TestCase]
    public void SustainedFireDrainsEnergyAndPulsesDamage()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "pillar") });
        var target = state.Units[0];
        target.X = 20f;

        // Charge fully, then hold fire for one second (30 ticks = 5 pulses at cadence 6).
        AdvanceTicks(sim, state, BattleConfig.Default.BreathRechargeSeconds
            * BattleConfig.Default.TickRate);
        target.Hp = target.Def.MaxHp;
        var energyBefore = state.Left.BreathEnergySeconds;
        for (var i = 0; i < 30; i++)
        {
            sim.Advance(state, new List<SimCommand> { SimCommand.FireBreath(PlayerSide.Left, target.X) });
        }

        var pulses = 30 / BattleConfig.Default.BreathPulseTicks;
        AssertThat(target.Hp).IsEqual(
            target.Def.MaxHp - pulses * BattleConfig.Default.BreathPulseDamage);
        AssertThat(state.Left.BreathEnergySeconds < energyBefore).IsTrue();
    }

    [TestCase]
    public void BreathFriendlyFireHitsOwnUnits()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var friendly = state.Units[0];
        friendly.X = 20f;

        AdvanceTicks(sim, state, BattleConfig.Default.BreathRechargeSeconds
            * BattleConfig.Default.TickRate);
        friendly.X = 20f;
        friendly.Hp = friendly.Def.MaxHp;
        for (var i = 0; i < BattleConfig.Default.BreathPulseTicks; i++)
        {
            sim.Advance(state, new List<SimCommand> { SimCommand.FireBreath(PlayerSide.Left, 20f) });
        }

        AssertThat(friendly.Hp < friendly.Def.MaxHp).IsTrue();
    }

    [TestCase]
    public void WrathStartsOnCooldownAndRejectsEarlyCast()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "pillar") });
        var enemy = state.Units[0];
        enemy.X = 5f;
        var startX = enemy.X;

        sim.Advance(state, new List<SimCommand> { SimCommand.CastWrath(PlayerSide.Left) });

        AssertThat(enemy.X).IsEqualApprox(startX, 0.001f);
        AssertThat(enemy.Hp).IsEqual(enemy.Def.MaxHp);
    }

    [TestCase]
    public void WrathDamagesAndPushesEnemiesOnOwnHalf_NoIFrames()
    {
        var (sim, state) = CreateBattle();
        var config = BattleConfig.Default;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "pillar") });
        var nearEnemy = state.Units[0];
        state.Left.WrathCooldownTicks = 0;
        nearEnemy.X = 5f;
        var startX = nearEnemy.X;

        sim.Advance(state, new List<SimCommand> { SimCommand.CastWrath(PlayerSide.Left) });

        AssertThat(nearEnemy.Hp).IsEqual(nearEnemy.Def.MaxHp - config.WrathDamage);
        AssertThat(nearEnemy.X).IsEqualApprox(startX + config.WrathKnockbackDistance, 0.05f);
        AssertThat(nearEnemy.IFrameTicks).IsEqual(0);
        // The cast tick itself counts toward the cooldown (economy decrements same tick).
        AssertThat(state.Left.WrathCooldownTicks).IsEqual(config.WrathCooldownTicks - 1);
    }

    [TestCase]
    public void WrathIgnoresEnemiesOnTheFarHalf()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "pillar") });
        var farEnemy = state.Units[0];
        state.Left.WrathCooldownTicks = 0;
        farEnemy.X = 30f;
        var startX = farEnemy.X;

        sim.Advance(state, new List<SimCommand> { SimCommand.CastWrath(PlayerSide.Left) });

        AssertThat(farEnemy.Hp).IsEqual(farEnemy.Def.MaxHp);
        AssertThat(farEnemy.X).IsEqualApprox(startX, 0.001f);
    }
}
