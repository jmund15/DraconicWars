namespace DraconicWars.Tests.Logic.Campaign;

using System.Collections.Generic;
using DraconicWars.Game.Campaign;
using DraconicWars.Game.Content;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>Alive-world contracts: every shipped unit and level carries narrative.</summary>
[TestSuite]
public class LoreTest
{
    [TestCase]
    public void EveryRosterUnitCarriesTitleAndFlavor()
    {
        foreach (var def in UnitCatalog.FullRoster)
        {
            var lore = UnitLore.For(def.Id);
            AssertThat(lore.Title.Length > 0)
                .OverrideFailureMessage($"{def.Id} has no lore title").IsTrue();
            AssertThat(lore.Flavor.Length > 0)
                .OverrideFailureMessage($"{def.Id} has no flavor line").IsTrue();
        }
    }

    [TestCase]
    public void EveryElementFieldsAtLeastFourDistinctTierUnits()
    {
        var counts = new Dictionary<DraconicWars.Sim.Units.Element, int>();
        foreach (var def in UnitCatalog.FullRoster)
        {
            if (def.Tier >= 4)
            {
                continue;
            }
            counts[def.Element] = counts.GetValueOrDefault(def.Element) + 1;
        }
        foreach (var element in System.Enum.GetValues<DraconicWars.Sim.Units.Element>())
        {
            AssertThat(counts.GetValueOrDefault(element) >= 4)
                .OverrideFailureMessage(
                    $"{element} fields only {counts.GetValueOrDefault(element)} tier units"
                    + " — tier-2 Resonance (4 distinct) is unreachable").IsTrue();
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
