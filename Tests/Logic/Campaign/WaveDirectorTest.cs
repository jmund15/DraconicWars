namespace DraconicWars.Tests.Logic.Campaign;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Campaign;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class WaveDirectorTest
{
    [TestCase]
    public void EmitsScriptedDeploysAtExactTicks()
    {
        var director = new WaveDirector(
            new[] { new WaveEntry(Tick: 30, UnitDefId: "enemy:grunt") },
            new List<RepeatingWave>());

        AssertThat(director.CommandsForTick(29).Count).IsEqual(0);
        var commands = director.CommandsForTick(30);
        AssertThat(commands.Count).IsEqual(1);
        AssertThat(commands[0].Kind).IsEqual(SimCommandKind.Deploy);
        AssertThat(commands[0].Side).IsEqual(PlayerSide.Right);
        AssertThat(commands[0].TargetId).IsEqual("enemy:grunt");
        AssertThat(director.CommandsForTick(31).Count).IsEqual(0);
    }

    [TestCase]
    public void RepeatingWaveFiresOnInterval()
    {
        var director = new WaveDirector(
            new List<WaveEntry>(),
            new[] { new RepeatingWave(StartTick: 100, IntervalTicks: 50, UnitDefId: "enemy:grunt", EndTick: 220) });

        AssertThat(director.CommandsForTick(99).Count).IsEqual(0);
        AssertThat(director.CommandsForTick(100).Count).IsEqual(1);
        AssertThat(director.CommandsForTick(150).Count).IsEqual(1);
        AssertThat(director.CommandsForTick(200).Count).IsEqual(1);
        AssertThat(director.CommandsForTick(250).Count).IsEqual(0);
    }

    [TestCase]
    public void MagnificationScalesEnemyHpAndDamageOnly()
    {
        var baseDef = TestUnitFactory.Grunt();

        var magnified = CampaignLevelDef.Magnify(baseDef, 250, idPrefix: "enemy:");

        AssertThat(magnified.Id).IsEqual("enemy:grunt");
        AssertThat(magnified.MaxHp).IsEqual(250);
        AssertThat(magnified.Damage).IsEqual(25);
        AssertThat(magnified.MoveSpeed).IsEqualApprox(baseDef.MoveSpeed, 0.001f);
        AssertThat(magnified.Range).IsEqualApprox(baseDef.Range, 0.001f);
    }

    [TestCase]
    public void WinPaysFullGoldLossPaysFortyToSixtyPercent()
    {
        var full = BattleRewards.ComputeGold(
            baseGold: 100, won: true, battleTicks: 9000, hardEndTick: 21600, enemySpireDamagePct: 0.4f);
        AssertThat(full).IsEqual(100);

        var quickLoss = BattleRewards.ComputeGold(
            baseGold: 100, won: false, battleTicks: 0, hardEndTick: 21600, enemySpireDamagePct: 0f);
        AssertThat(quickLoss).IsEqual(40);

        var longLoss = BattleRewards.ComputeGold(
            baseGold: 100, won: false, battleTicks: 21600, hardEndTick: 21600, enemySpireDamagePct: 1f);
        AssertThat(longLoss).IsEqual(60);

        var midLoss = BattleRewards.ComputeGold(
            baseGold: 100, won: false, battleTicks: 10800, hardEndTick: 21600, enemySpireDamagePct: 0.5f);
        AssertThat(midLoss).IsEqual(50);
    }

    [TestCase]
    public void CampaignHasFiveLevelsWithCompressedTeachingArcs()
    {
        var levels = CampaignCatalog.Levels;
        AssertThat(levels.Count).IsEqual(5);

        // L1-L4 are compressed (3-4 min arcs); L5 debuts the full 10-minute timeline.
        foreach (var level in levels.Take(4))
        {
            AssertThat(level.Config.HardEndTick < BattleConfig.Default.HardEndTick).IsTrue();
            AssertThat(level.Config.LastStandEnabled).IsTrue();
        }
        AssertThat(levels[4].Config.HardEndTick).IsEqual(BattleConfig.Default.HardEndTick);

        // Every level has at least one wave source and a positive reward.
        foreach (var level in levels)
        {
            var hasWaves = level.Waves.Count > 0 || level.RepeatingWaves.Count > 0;
            AssertThat(hasWaves).IsTrue();
            AssertThat(level.BaseGoldReward > 0).IsTrue();
        }

        // L1's scripted aerial wave (forces the breath verb) exists.
        AssertThat(levels[0].Waves.Any(w => w.UnitDefId.Contains("whelp"))).IsTrue();
    }

    [TestCase]
    public void ScriptedBattleAgainstLevelOneIsWinnableBySimulation()
    {
        // Drive the sim with L1's wave script and a simple player macro: deploy the
        // cheapest unit on cooldown. The compressed L1 must end (no stalemate) within
        // its hard end, by spire kill or timeout.
        var level = CampaignCatalog.Levels[0];
        var defs = CampaignCatalog.BuildBattleDefs(level);
        var sim = new BattleSim(level.Config, defs, DraconicWars.Sim.Conduits.ConduitDefs.All);
        var state = sim.CreateInitialState(42UL);
        var director = new WaveDirector(level.Waves, level.RepeatingWaves);

        var safety = level.Config.HardEndTick + 10;
        while (state.Outcome == BattleOutcome.Ongoing && state.Tick < safety)
        {
            var commands = new List<SimCommand>(director.CommandsForTick(state.Tick))
            {
                SimCommand.Deploy(PlayerSide.Left, "kobold_spearman"),
            };
            sim.Advance(state, commands);
        }

        AssertThat(state.Outcome != BattleOutcome.Ongoing).IsTrue();
    }
}

[TestSuite]
public class CampaignProgressTest
{
    [TestCase]
    public void FirstClearAwardsGoldRankAndUnlocksNext()
    {
        var profile = new DraconicWars.Meta.PlayerProfile();

        var firstClear = CampaignProgress.ApplyBattleResult(
            profile, CampaignCatalog.Levels[0], won: true, battleTicks: 4000, enemySpireDamagePct: 1f);

        AssertThat(firstClear).IsTrue();
        AssertThat(profile.ClearedLevelIds.Contains("cm_01")).IsTrue();
        AssertThat(profile.CampaignFirstClears).IsEqual(1);
        AssertThat(profile.Gold).IsEqual(CampaignCatalog.Levels[0].BaseGoldReward);
        AssertThat(CampaignProgress.IsUnlocked(profile, 1)).IsTrue();
        AssertThat(CampaignProgress.IsUnlocked(profile, 2)).IsFalse();
    }

    [TestCase]
    public void RepeatWinPaysGoldButNoSecondFirstClear()
    {
        var profile = new DraconicWars.Meta.PlayerProfile();
        CampaignProgress.ApplyBattleResult(
            profile, CampaignCatalog.Levels[0], won: true, battleTicks: 4000, enemySpireDamagePct: 1f);
        var goldAfterFirst = profile.Gold;

        var secondClear = CampaignProgress.ApplyBattleResult(
            profile, CampaignCatalog.Levels[0], won: true, battleTicks: 4000, enemySpireDamagePct: 1f);

        AssertThat(secondClear).IsFalse();
        AssertThat(profile.CampaignFirstClears).IsEqual(1);
        AssertThat(profile.Gold > goldAfterFirst).IsTrue();
    }

    [TestCase]
    public void LossPaysPartialGoldAndDoesNotClear()
    {
        var profile = new DraconicWars.Meta.PlayerProfile();

        CampaignProgress.ApplyBattleResult(
            profile, CampaignCatalog.Levels[0], won: false, battleTicks: 0, enemySpireDamagePct: 0f);

        AssertThat(profile.ClearedLevelIds.Count).IsEqual(0);
        AssertThat(profile.Gold).IsEqual((int)(CampaignCatalog.Levels[0].BaseGoldReward * 0.4f));
    }

    [TestCase]
    public void PlayerUnitLevelsScaleBattleDefsButNotEnemies()
    {
        var profile = new DraconicWars.Meta.PlayerProfile();
        profile.UnitLevels["kobold_spearman"] = 3;

        var defs = CampaignCatalog.BuildBattleDefs(CampaignCatalog.Levels[0], profile);

        var player = defs.First(d => d.Id == "kobold_spearman");
        var enemy = defs.First(d => d.Id == "enemy:kobold_spearman");
        AssertThat(player.MaxHp).IsEqual((int)System.MathF.Round(110 * 1.21f));
        AssertThat(player.Damage).IsEqual((int)System.MathF.Round(12 * 1.21f));
        AssertThat(enemy.MaxHp).IsEqual(110);
        AssertThat(enemy.Damage).IsEqual(12);
    }

    [TestCase]
    public void ClearingTheBossLevelBondsItsDragon()
    {
        var profile = new DraconicWars.Meta.PlayerProfile();
        var boss = CampaignCatalog.Levels[4];
        AssertThat(boss.BondedDragonId).IsEqual("pyraxis");

        CampaignProgress.ApplyBattleResult(
            profile, boss, won: true, battleTicks: 12000, enemySpireDamagePct: 1f);

        AssertThat(profile.UnitLevels.ContainsKey("pyraxis")).IsTrue();
        AssertThat(profile.UnitLevels["pyraxis"]).IsEqual(9);
    }

    [TestCase]
    public void OnlyLevelOneUnlockedOnFreshProfile()
    {
        var profile = new DraconicWars.Meta.PlayerProfile();
        AssertThat(CampaignProgress.IsUnlocked(profile, 0)).IsTrue();
        AssertThat(CampaignProgress.IsUnlocked(profile, 1)).IsFalse();
    }
}

internal static class TestUnitFactory
{
    internal static DraconicWars.Sim.Units.UnitDef Grunt()
    {
        return new DraconicWars.Sim.Units.UnitDef(
            Id: "grunt", DisplayName: "Grunt", Tier: 1,
            TypeClass: DraconicWars.Sim.Units.TypeClass.Melee,
            Element: DraconicWars.Sim.Units.Element.Stone,
            MaxHp: 100, Damage: 10, ForeswingTicks: 8, BackswingTicks: 10,
            Range: 0.8f, RangeMin: 0f, IsArea: false, MoveSpeed: 2f,
            KnockbackCount: 3, DeployCost: 50, DeployCooldownTicks: 60,
            Stratum: DraconicWars.Sim.Units.Stratum.Ground,
            CanTargetGround: true, CanTargetAir: false);
    }
}
