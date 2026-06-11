namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class AscensionTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { TestUnits.Grunt(), TestUnits.Elite(), TestUnits.Dragon() });
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
    public void TrickleFillsMeterOverTime()
    {
        var (sim, state) = CreateBattle();

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate);

        AssertThat(state.Left.AscensionMeter).IsEqualApprox(
            BattleConfig.Default.AscensionTricklePerSecond, 0.01f);
    }

    [TestCase]
    public void CrossingThresholdRaisesTier()
    {
        var (sim, state) = CreateBattle();
        state.Left.AscensionMeter = BattleConfig.Default.AscensionThresholds[0];

        sim.Advance(state, SimCommand.None);

        AssertThat(state.Left.AscensionTier).IsEqual(2);
        AssertThat(state.Right.AscensionTier).IsEqual(1);
    }

    [TestCase]
    public void DeployIsGatedByAscensionTier()
    {
        var (sim, state) = CreateBattle();

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "elite") });
        AssertThat(state.Units.Count).IsEqual(0);

        state.Left.AscensionMeter = BattleConfig.Default.AscensionThresholds[0];
        sim.Advance(state, SimCommand.None);
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "elite") });
        AssertThat(state.Units.Count).IsEqual(1);
    }

    [TestCase]
    public void KillContributionIsCappedPerSegment()
    {
        var (sim, state) = CreateBattle();
        var segment = BattleConfig.Default.AscensionThresholds[0];
        var cap = segment * BattleConfig.Default.KillAscensionCapPct;

        for (var i = 0; i < 100; i++)
        {
            sim._TestCreditKillAscension(state, PlayerSide.Left, TestUnits.Grunt());
        }

        AssertThat(state.Left.AscensionMeter).IsEqualApprox(cap, 0.01f);
    }

    [TestCase]
    public void FrontlinePastMidfieldGrantsLaneControlBonus()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        state.Units[0].X = BattleConfig.Default.LaneLength * 0.75f;
        var meterBefore = state.Left.AscensionMeter;
        var rightBefore = state.Right.AscensionMeter;

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate);

        var leftGain = state.Left.AscensionMeter - meterBefore;
        var rightGain = state.Right.AscensionMeter - rightBefore;
        AssertThat(leftGain).IsEqualApprox(
            rightGain + BattleConfig.Default.LaneControlBonusPerSecond, 0.05f);
    }

    [TestCase]
    public void TierBehindGrantsCatchupTrickle()
    {
        var (sim, state) = CreateBattle();
        state.Right.AscensionTier = 3;

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate);

        var expected = BattleConfig.Default.AscensionTricklePerSecond
            * BattleConfig.Default.TierBehindTrickleMultiplier;
        AssertThat(state.Left.AscensionMeter).IsEqualApprox(expected, 0.02f);
    }

    [TestCase]
    public void FirstTierCrossingEscalatesDripForBothSides()
    {
        var (sim, state) = CreateBattle();
        state.Right.AscensionMeter = BattleConfig.Default.AscensionThresholds[0];
        sim.Advance(state, SimCommand.None);
        AssertThat(state.Right.AscensionTier).IsEqual(2);

        var leftBefore = state.Left.Mana;
        sim.Advance(state, SimCommand.None);

        var expectedGain = BattleConfig.Default.DripPerTick
            * BattleConfig.Default.AscensionDripEscalation;
        AssertThat(state.Left.Mana - leftBefore).IsEqualApprox(expectedGain, 0.01f);
    }

    [TestCase]
    public void SpireChipDamageGrantsAttackerMeter()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var attacker = state.Units[0];
        attacker.X = BattleConfig.Default.LaneLength - 0.5f;
        var grunt = TestUnits.Grunt();

        var meterBefore = state.Left.AscensionMeter;
        AdvanceTicks(sim, state, grunt.ForeswingTicks + 1);

        var perSecond = BattleConfig.Default.AscensionTricklePerSecond
            + BattleConfig.Default.LaneControlBonusPerSecond;
        var trickle = perSecond / BattleConfig.Default.TickRate * (grunt.ForeswingTicks + 1);
        var chip = grunt.Damage * BattleConfig.Default.ChipDamageAscensionRate;
        AssertThat(state.Left.AscensionMeter - meterBefore)
            .IsEqualApprox(trickle + chip, 0.05f);
    }

    [TestCase]
    public void ChannelingRequiresDragonTier()
    {
        var (sim, state) = CreateBattle();
        state.Left.EquippedDragonId = "dragon";

        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, 200) });
        AssertThat(state.Left.SummoningProgress).IsEqualApprox(0f, 0.001f);

        state.Left.AscensionTier = 4;
        var manaBefore = state.Left.Mana;
        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, 200) });
        AssertThat(state.Left.SummoningProgress).IsEqualApprox(200f, 0.001f);
        AssertThat(state.Left.Mana).IsEqualApprox(
            manaBefore - 200f + BattleConfig.Default.DripPerTick, 0.5f);
    }

    [TestCase]
    public void CompletedSummoningSpawnsTheEquippedDragon()
    {
        var (sim, state) = CreateBattle();
        state.Left.EquippedDragonId = "dragon";
        state.Left.AscensionTier = 4;

        var cost = (int)BattleConfig.Default.SummoningCost;
        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, cost) });

        AssertThat(state.Units.Count).IsEqual(1);
        AssertThat(state.Units[0].Def.Tier).IsEqual(4);
        AssertThat(state.Units[0].Side).IsEqual(PlayerSide.Left);
    }

    [TestCase]
    public void ChannelingBlockedWhileDragonIsOnTheField()
    {
        var (sim, state) = CreateBattle();
        state.Left.EquippedDragonId = "dragon";
        state.Left.AscensionTier = 4;
        var cost = (int)BattleConfig.Default.SummoningCost;
        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, cost) });
        AssertThat(state.Units.Count).IsEqual(1);

        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, 200) });

        AssertThat(state.Left.SummoningProgress).IsEqualApprox(cost, 0.001f);
        AssertThat(state.Units.Count).IsEqual(1);
    }

    [TestCase]
    public void DragonDeathResetsSummoningForResummon()
    {
        var (sim, state) = CreateBattle();
        state.Left.EquippedDragonId = "dragon";
        state.Left.AscensionTier = 4;
        var cost = (int)BattleConfig.Default.SummoningCost;
        sim.Advance(state, new List<SimCommand> { SimCommand.ChannelMana(PlayerSide.Left, cost) });
        var dragon = state.Units.Single(u => u.Def.Tier == 4);

        dragon.Hp = 1;
        sim._TestDealDamage(state, dragon, dragon);
        sim.Advance(state, SimCommand.None);

        AssertThat(state.Units.Count).IsEqual(0);
        AssertThat(state.Left.SummoningProgress).IsEqualApprox(0f, 0.001f);
    }
}
