namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class ConduitTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { TestUnits.Grunt() },
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
    public void BuildOccupiesSocketAndSpendsMana()
    {
        var (sim, state) = CreateBattle();
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "mana_well") });

        AssertThat(state.Left.Conduits.Count).IsEqual(1);
        AssertThat(state.Left.Conduits["mana_well"]).IsEqual(1);
        var cost = ConduitDefs.ById("mana_well").CostForTier(1);
        AssertThat(state.Left.Mana < manaBefore - cost + 5f).IsTrue();
    }

    [TestCase]
    public void SecondCopyOfSameConduitTypeIsRejected()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "mana_well") });

        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "mana_well") });

        AssertThat(state.Left.Conduits.Count).IsEqual(1);
        AssertThat(state.Left.Conduits["mana_well"]).IsEqual(1);
    }

    [TestCase]
    public void BuildRejectedWhenAllSocketsFull()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "mana_well"),
            SimCommand.BuildConduit(PlayerSide.Left, "war_horn"),
            SimCommand.BuildConduit(PlayerSide.Left, "aurum_vault"),
        });
        AssertThat(state.Left.Conduits.Count).IsEqual(3);

        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "swift_banner") });

        AssertThat(state.Left.Conduits.Count).IsEqual(3);
        AssertThat(state.Left.Conduits.ContainsKey("swift_banner")).IsFalse();
    }

    [TestCase]
    public void UpgradeIncrementsTierAtScaledCost()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "war_horn") });
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });

        AssertThat(state.Left.Conduits["war_horn"]).IsEqual(2);
        var def = ConduitDefs.ById("war_horn");
        AssertThat(def.CostForTier(2)).IsEqual((int)(def.BaseCost * 1.6f));
        AssertThat(state.Left.Mana).IsEqualApprox(
            manaBefore - def.CostForTier(2) + BattleConfig.Default.DripPerTick, 0.5f);
    }

    [TestCase]
    public void UpgradeBeyondTierThreeIsRejected()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "war_horn") });
        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });
        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });
        AssertThat(state.Left.Conduits["war_horn"]).IsEqual(3);
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });

        AssertThat(state.Left.Conduits["war_horn"]).IsEqual(3);
        AssertThat(state.Left.Mana).IsEqualApprox(manaBefore + BattleConfig.Default.DripPerTick, 0.01f);
    }

    [TestCase]
    public void SellRefundsHalfOfTotalSpentAndFreesSocket()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "war_horn") });
        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });
        var def = ConduitDefs.ById("war_horn");
        var totalSpent = def.CostForTier(1) + def.CostForTier(2);
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand> { SimCommand.SellConduit(PlayerSide.Left, "war_horn") });

        AssertThat(state.Left.Conduits.ContainsKey("war_horn")).IsFalse();
        AssertThat(state.Left.Mana).IsEqualApprox(
            manaBefore + totalSpent * 0.5f + BattleConfig.Default.DripPerTick, 0.5f);
    }

    [TestCase]
    public void ManaWellIncreasesDripRate()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Left, "mana_well") });
        var leftBefore = state.Left.Mana;
        var rightBefore = state.Right.Mana;

        AdvanceTicks(sim, state, BattleConfig.Default.TickRate);

        var leftGain = state.Left.Mana - leftBefore;
        var rightGain = state.Right.Mana - rightBefore;
        var wellBonus = ConduitDefs.ById("mana_well").DripBonusPerTier;
        AssertThat(leftGain).IsEqualApprox(rightGain + wellBonus, 0.05f);
    }

    [TestCase]
    public void ManaWellTierOnePaybackIsAtLeast150Seconds()
    {
        var def = ConduitDefs.ById("mana_well");
        var paybackSeconds = def.CostForTier(1) / def.DripBonusPerTier;
        AssertThat(paybackSeconds >= 150f).IsTrue();
    }

    [TestCase]
    public void WarHornIncreasesUnitDamage()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "war_horn"),
        });
        var grunt = TestUnits.Grunt();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var attacker = state.Units[0];

        // Tier 3 (+24%) gives an observable integer difference: 10 * 1.24 -> 12.
        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });
        sim.Advance(state, new List<SimCommand> { SimCommand.UpgradeConduit(PlayerSide.Left, "war_horn") });
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "grunt") });
        var defender = state.Units[1];
        attacker.X = 10f;
        defender.X = 10.5f;

        AdvanceTicks(sim, state, grunt.ForeswingTicks + 1);

        AssertThat(defender.Hp).IsEqual(grunt.MaxHp - 12);
    }

    [TestCase]
    public void KillBountyCreditsTheKillersSide()
    {
        var biter = TestUnits.FastBiter() with { Damage = 100 };
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { biter, TestUnits.Grunt(id: "pillar", moveSpeed: 0f) },
            ConduitDefs.All);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 500f;
        state.Left.WalletCap = 5000f;
        state.Right.Mana = 500f;

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "biter") });
        state.Units[0].X = 10f;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, "pillar") });
        state.Units[1].X = 10.5f;
        var manaBeforeKill = state.Left.Mana;

        AdvanceTicks(sim, state, biter.ForeswingTicks + 1);

        var expectedBounty = TestUnits.Grunt().DeployCost / 5;
        var drip = BattleConfig.Default.DripPerTick * (biter.ForeswingTicks + 1);
        AssertThat(state.Left.Mana).IsEqualApprox(manaBeforeKill + expectedBounty + drip, 0.5f);
    }

    [TestCase]
    public void RampartShieldAbsorbsSpireDamage()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.BuildConduit(PlayerSide.Right, "rampart") });
        var shield = state.Right.SpireShield;
        AssertThat(shield > 0f).IsTrue();

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var attacker = state.Units[0];
        attacker.X = BattleConfig.Default.LaneLength - 0.5f;
        var spireBefore = state.RightSpireHp;

        AdvanceTicks(sim, state, TestUnits.Grunt().ForeswingTicks + 1);

        AssertThat(state.RightSpireHp).IsEqualApprox(spireBefore, 0.01f);
        AssertThat(state.Right.SpireShield < shield).IsTrue();
    }

    [TestCase]
    public void LastStandGrantsDripOncePerBattle()
    {
        var config = BattleConfig.Default with { LastStandEnabled = true };
        var sim = new BattleSim(config, new[] { TestUnits.Grunt() }, ConduitDefs.All);
        var state = sim.CreateInitialState(1UL);
        state.LeftSpireHp = config.SpireMaxHp * 0.09f;

        var manaBefore = state.Left.Mana;
        sim.Advance(state, SimCommand.None);

        AssertThat(state.Left.LastStandUsed).IsTrue();
        var expectedDrip = (config.BaseDripPerSecond + config.LastStandDripBonus) / config.TickRate;
        AssertThat(state.Left.Mana).IsEqualApprox(manaBefore + expectedDrip, 0.01f);
    }
}
