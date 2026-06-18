namespace DraconicWars.Tests.Logic.Units;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// The completed 40-unit roster (roster-expansion-40.md §4). The design roster is 35 warband
/// + 5 Draconic dragons; the rental Elder Drake is a 41st special outside that count. Pins the
/// rarity distribution and the per-element guarantees (≥2 Commons/element, ≥1 low-rarity AA).
/// </summary>
[TestSuite]
public class FullRosterTest
{
    // The shipped roster minus the rental dragon = the 40 design units.
    private static IReadOnlyList<UnitDef> Roster40() =>
        UnitCatalog.FullRoster.Where(d => d.Id != UnitCatalog.RentalDragonId).ToList();

    [TestCase]
    public void RosterIsFortyDesignUnitsPlusTheRental()
    {
        AssertThat(Roster40().Count).IsEqual(40);
        AssertThat(UnitCatalog.FullRoster.Count).IsEqual(41); // 40 + Elder Drake rental
    }

    [TestCase]
    public void RarityDistributionMatchesTheDesign()
    {
        var counts = Roster40().GroupBy(d => d.Rarity).ToDictionary(g => g.Key, g => g.Count());
        AssertThat(counts.GetValueOrDefault(Rarity.Common)).IsEqual(10);
        AssertThat(counts.GetValueOrDefault(Rarity.Uncommon)).IsEqual(9);
        AssertThat(counts.GetValueOrDefault(Rarity.Rare)).IsEqual(8);
        AssertThat(counts.GetValueOrDefault(Rarity.Epic)).IsEqual(4);
        AssertThat(counts.GetValueOrDefault(Rarity.Mythic)).IsEqual(4);
        AssertThat(counts.GetValueOrDefault(Rarity.Draconic)).IsEqual(5);
    }

    [TestCase]
    public void EveryElementHasAtLeastTwoCommons()
    {
        var commonsByElement = Roster40()
            .Where(d => d.Rarity == Rarity.Common)
            .GroupBy(d => d.Element)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var element in new[]
                 { Element.Fire, Element.Frost, Element.Storm, Element.Venom, Element.Stone })
        {
            AssertThat(commonsByElement.GetValueOrDefault(element) >= 2)
                .OverrideFailureMessage($"{element} has fewer than 2 Common units").IsTrue();
        }
    }

    [TestCase]
    public void EveryElementHasALowRarityAntiAirUnit()
    {
        var lowRarityAaElements = Roster40()
            .Where(d => (d.Rarity == Rarity.Common || d.Rarity == Rarity.Uncommon) && d.CanTargetAir)
            .Select(d => d.Element)
            .ToHashSet();
        foreach (var element in new[]
                 { Element.Fire, Element.Frost, Element.Storm, Element.Venom, Element.Stone })
        {
            AssertThat(lowRarityAaElements.Contains(element))
                .OverrideFailureMessage($"{element} has no low-rarity anti-air unit").IsTrue();
        }
    }
}
