namespace DraconicWars.Tests.Logic.Units;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// The shipped 24-unit roster carries its collection rarity (roster-expansion-40.md §1,
/// the Part-4 Common-wave starting point). Rarity is decoupled from Tier and applied via
/// UnitCatalog's rarity table; unmapped ids default to Common.
/// </summary>
[TestSuite]
public class RosterRarityTest
{
    private static UnitDef Find(string id) => UnitCatalog.FullRoster.First(d => d.Id == id);

    [TestCase]
    public void DragonsAreDraconicRarity()
    {
        AssertThat(Find("elder_drake").Rarity).IsEqual(Rarity.Draconic);
        AssertThat(Find("pyraxis").Rarity).IsEqual(Rarity.Draconic);
    }

    [TestCase]
    public void SignatureUnitsCarryTheirAssignedRarity()
    {
        AssertThat(Find("deepway_bulwark").Rarity).IsEqual(Rarity.Epic);
        AssertThat(Find("boreal_colossus").Rarity).IsEqual(Rarity.Epic);
        AssertThat(Find("ash_revenant").Rarity).IsEqual(Rarity.Rare);
        AssertThat(Find("gale_harrier").Rarity).IsEqual(Rarity.Rare);
        AssertThat(Find("spark_courier").Rarity).IsEqual(Rarity.Uncommon);
    }

    [TestCase]
    public void UnmappedUnitsDefaultToCommon()
    {
        AssertThat(Find("kobold_spearman").Rarity).IsEqual(Rarity.Common);
        AssertThat(Find("forest_archer").Rarity).IsEqual(Rarity.Common);
        AssertThat(Find("frost_whelp").Rarity).IsEqual(Rarity.Common);
    }

    [TestCase]
    public void RarityDistributionMatchesTheShippedRoster()
    {
        var counts = UnitCatalog.FullRoster
            .GroupBy(d => d.Rarity)
            .ToDictionary(g => g.Key, g => g.Count());

        AssertThat(counts.GetValueOrDefault(Rarity.Common)).IsEqual(7);
        AssertThat(counts.GetValueOrDefault(Rarity.Uncommon)).IsEqual(7);
        AssertThat(counts.GetValueOrDefault(Rarity.Rare)).IsEqual(6);
        AssertThat(counts.GetValueOrDefault(Rarity.Epic)).IsEqual(2);
        AssertThat(counts.GetValueOrDefault(Rarity.Draconic)).IsEqual(2);
    }
}
