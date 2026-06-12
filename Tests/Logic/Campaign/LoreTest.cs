namespace DraconicWars.Tests.Logic.Campaign;

using DraconicWars.Game.Campaign;
using DraconicWars.Game.Content;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>Alive-world contracts: every shipped unit and level carries narrative.</summary>
[TestSuite]
public class LoreTest
{
    [TestCase]
    public void EveryFirstPlayableUnitCarriesTitleAndFlavor()
    {
        foreach (var def in UnitCatalog.FirstPlayable)
        {
            var lore = UnitLore.For(def.Id);
            AssertThat(lore.Title.Length > 0)
                .OverrideFailureMessage($"{def.Id} has no lore title").IsTrue();
            AssertThat(lore.Flavor.Length > 0)
                .OverrideFailureMessage($"{def.Id} has no flavor line").IsTrue();
        }
    }

    [TestCase]
    public void EveryCampaignLevelCarriesABlurb()
    {
        foreach (var level in CampaignCatalog.Levels)
        {
            AssertThat(level.Blurb.Length > 0)
                .OverrideFailureMessage($"{level.Id} has no blurb").IsTrue();
        }
    }
}
