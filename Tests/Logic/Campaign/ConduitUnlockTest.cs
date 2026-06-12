namespace DraconicWars.Tests.Logic.Campaign;

using DraconicWars.Game.Campaign;
using DraconicWars.Meta;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class ConduitUnlockTest
{
    [TestCase]
    public void FreshProfilesOwnTheBaseTrio()
    {
        var profile = new PlayerProfile();

        CampaignProgress.EnsureBaseConduits(profile);

        AssertThat(profile.ConduitsUnlocked.Contains("mana_well")).IsTrue();
        AssertThat(profile.ConduitsUnlocked.Contains("war_horn")).IsTrue();
        AssertThat(profile.ConduitsUnlocked.Contains("rampart")).IsTrue();
        AssertThat(profile.ConduitsUnlocked.Contains("skyward_flak")).IsFalse();
    }

    [TestCase]
    public void FirstClearGrantsTheLevelsConduit()
    {
        var profile = new PlayerProfile();
        CampaignProgress.EnsureBaseConduits(profile);
        var level = CampaignCatalog.Levels[3];
        AssertThat(level.UnlockConduitId).IsEqual("skyward_flak");

        CampaignProgress.ApplyBattleResult(
            profile, level, won: true, battleTicks: 3000, enemySpireDamagePct: 1f);

        AssertThat(profile.ConduitsUnlocked.Contains("skyward_flak")).IsTrue();
    }

    [TestCase]
    public void PreLibraryProfilesAreReseededWithClearedGrants()
    {
        var profile = new PlayerProfile();
        profile.ClearedLevelIds.Add("cm_01");
        profile.ClearedLevelIds.Add("cm_04");

        CampaignProgress.EnsureBaseConduits(profile);

        AssertThat(profile.ConduitsUnlocked.Contains("aurum_vault")).IsTrue();
        AssertThat(profile.ConduitsUnlocked.Contains("skyward_flak")).IsTrue();
        AssertThat(profile.ConduitsUnlocked.Contains("siege_mortar")).IsFalse();
    }

    [TestCase]
    public void EveryCampaignConduitGrantResolvesInTheCatalog()
    {
        foreach (var level in CampaignCatalog.Levels)
        {
            if (level.UnlockConduitId is { } conduitId)
            {
                AssertThat(DraconicWars.Sim.Conduits.ConduitDefs.ById(conduitId).Id)
                    .IsEqual(conduitId);
            }
        }
    }
}
