namespace DraconicWars.Tests.Logic.Meta;

using DraconicWars.Meta;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class MetaProgressionTest
{
    [TestCase]
    public void CostTableOpensAtFiftyAndDoublesPerLevel()
    {
        AssertThat(MetaProgression.CostForLevel(2)).IsEqual(50);
        AssertThat(MetaProgression.CostForLevel(3)).IsEqual(100);
        AssertThat(MetaProgression.CostForLevel(4)).IsEqual(200);
        AssertThat(MetaProgression.CostForLevel(13)).IsEqual(50 * 2048);
    }

    [TestCase]
    public void EntryLevelsFollowRarityOffsets()
    {
        AssertThat(MetaProgression.EntryLevel(1)).IsEqual(1);
        AssertThat(MetaProgression.EntryLevel(2)).IsEqual(3);
        AssertThat(MetaProgression.EntryLevel(3)).IsEqual(6);
        AssertThat(MetaProgression.EntryLevel(4)).IsEqual(9);
    }

    [TestCase]
    public void StatMultiplierGrowsTenPercentPerLevelAboveEntry()
    {
        AssertThat(MetaProgression.StatMultiplier(level: 1, tier: 1)).IsEqualApprox(1.0f, 0.001f);
        AssertThat(MetaProgression.StatMultiplier(level: 3, tier: 1)).IsEqualApprox(1.21f, 0.001f);
        AssertThat(MetaProgression.StatMultiplier(level: 9, tier: 4)).IsEqualApprox(1.0f, 0.001f);
        AssertThat(MetaProgression.StatMultiplier(level: 11, tier: 4)).IsEqualApprox(1.21f, 0.001f);
    }

    [TestCase]
    public void DragonRankSumsAllProgressSources()
    {
        var profile = new PlayerProfile();
        profile.UnitLevels["a"] = 1;
        profile.UnitLevels["b"] = 3;
        profile.LevelsPurchased = 10;
        profile.CampaignFirstClears = 3;
        profile.HeatRungClears = 2;
        profile.AchievementsEarned = 4;

        AssertThat(MetaProgression.DragonRank(profile)).IsEqual(10 + 2 + 6 + 2 + 4);
    }

    [TestCase]
    public void LevelCapAndSocketsFollowRankMilestones()
    {
        AssertThat(MetaProgression.LevelCap(0)).IsEqual(4);
        AssertThat(MetaProgression.LevelCap(40)).IsEqual(6);
        AssertThat(MetaProgression.LevelCap(90)).IsEqual(8);
        AssertThat(MetaProgression.LevelCap(150)).IsEqual(10);
        AssertThat(MetaProgression.LevelCap(220)).IsEqual(13);

        AssertThat(MetaProgression.ConduitSockets(0)).IsEqual(3);
        AssertThat(MetaProgression.ConduitSockets(60)).IsEqual(4);
        AssertThat(MetaProgression.ConduitSockets(180)).IsEqual(5);
    }

    [TestCase]
    public void BuyLevelSpendsGoldAndCountsTowardRank()
    {
        var profile = new PlayerProfile { Gold = 500 };
        profile.UnitLevels["grunt"] = 1;

        var bought = MetaProgression.TryBuyLevel(profile, "grunt");

        AssertThat(bought).IsTrue();
        AssertThat(profile.UnitLevels["grunt"]).IsEqual(2);
        AssertThat(profile.Gold).IsEqual(450);
        AssertThat(profile.GoldSpentOnLevels).IsEqual(50);
        AssertThat(profile.LevelsPurchased).IsEqual(1);
    }

    [TestCase]
    public void BuyLevelRejectedWithoutGoldOrAtCap()
    {
        var poor = new PlayerProfile { Gold = 10 };
        poor.UnitLevels["grunt"] = 1;
        AssertThat(MetaProgression.TryBuyLevel(poor, "grunt")).IsFalse();
        AssertThat(poor.UnitLevels["grunt"]).IsEqual(1);

        // Rank 0 caps levels at 4: buying 2, 3, 4 succeeds; 5 is rejected.
        var capped = new PlayerProfile { Gold = 100000 };
        capped.UnitLevels["grunt"] = 1;
        AssertThat(MetaProgression.TryBuyLevel(capped, "grunt")).IsTrue();
        AssertThat(MetaProgression.TryBuyLevel(capped, "grunt")).IsTrue();
        AssertThat(MetaProgression.TryBuyLevel(capped, "grunt")).IsTrue();
        AssertThat(MetaProgression.TryBuyLevel(capped, "grunt")).IsFalse();
        AssertThat(capped.UnitLevels["grunt"]).IsEqual(4);
    }

    [TestCase]
    public void CapRaisesAsPurchasesRaiseRank()
    {
        // 100k gold, one unit: rank grows +1 per purchase, so by the time level 4 is
        // bought the rank is 3 (+1 per level) — still cap 4. Simulate first-clears to
        // push rank past 40 and verify level 5 unlocks.
        var profile = new PlayerProfile { Gold = 1000000 };
        profile.UnitLevels["grunt"] = 1;
        MetaProgression.TryBuyLevel(profile, "grunt");
        MetaProgression.TryBuyLevel(profile, "grunt");
        MetaProgression.TryBuyLevel(profile, "grunt");
        AssertThat(MetaProgression.TryBuyLevel(profile, "grunt")).IsFalse();

        profile.CampaignFirstClears = 20;
        AssertThat(MetaProgression.DragonRank(profile) >= 40).IsTrue();
        AssertThat(MetaProgression.TryBuyLevel(profile, "grunt")).IsTrue();
        AssertThat(profile.UnitLevels["grunt"]).IsEqual(5);
    }

    [TestCase]
    public void RespecRefundsAllGoldButKeepsRank()
    {
        var profile = new PlayerProfile { Gold = 1000 };
        profile.UnitLevels["grunt"] = 1;
        MetaProgression.TryBuyLevel(profile, "grunt");
        MetaProgression.TryBuyLevel(profile, "grunt");
        var rankBefore = MetaProgression.DragonRank(profile);

        MetaProgression.Respec(profile, unitId => 1);

        AssertThat(profile.Gold).IsEqual(1000);
        AssertThat(profile.UnitLevels["grunt"]).IsEqual(1);
        AssertThat(profile.GoldSpentOnLevels).IsEqual(0);
        AssertThat(MetaProgression.DragonRank(profile)).IsEqual(rankBefore);
    }

    [TestCase]
    public void ProfileJsonRoundTripPreservesState()
    {
        var profile = new PlayerProfile { Gold = 123, Sigils = 4 };
        profile.UnitLevels["grunt"] = 5;
        profile.CampaignFirstClears = 2;

        var json = profile.ToJson();
        var restored = PlayerProfile.FromJson(json);

        AssertThat(restored.Gold).IsEqual(123);
        AssertThat(restored.Sigils).IsEqual(4);
        AssertThat(restored.UnitLevels["grunt"]).IsEqual(5);
        AssertThat(restored.CampaignFirstClears).IsEqual(2);
    }

    [TestCase]
    public void ForwardSchemaVersionIsRejected()
    {
        var json = "{\"SchemaVersion\": 999, \"Gold\": 1}";

        var threw = false;
        try
        {
            PlayerProfile.FromJson(json);
        }
        catch (System.InvalidOperationException)
        {
            threw = true;
        }
        AssertThat(threw).IsTrue();
    }
}
