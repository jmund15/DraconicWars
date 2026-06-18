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
    public void EveryDragonDeclaresACounter()
    {
        // The apex matchup wheel is seeded (roster-expansion-40.md §7, Q3): each dragon
        // dominates one element. Catches accidental loss of the wheel.
        foreach (var id in new[] { "pyraxis", "voltherax", "glacereth", "sythraal", "terravossk" })
        {
            AssertThat(UnitCatalog.FullRoster.First(d => d.Id == id).StrongVsElement)
                .OverrideFailureMessage($"{id} declares no counter").IsNotNull();
        }
    }

    [TestCase]
    public void DragonCounterWheelDirectionsArePinned()
    {
        // The apex 5-cycle (each dragon dominates one element). Pins the directions so a
        // silent flip is caught, not just that some counter exists.
        var wheel = new Dictionary<string, Element>
        {
            ["pyraxis"] = Element.Venom,
            ["sythraal"] = Element.Stone,
            ["terravossk"] = Element.Storm,
            ["voltherax"] = Element.Frost,
            ["glacereth"] = Element.Fire,
        };
        foreach (var (id, expected) in wheel)
        {
            AssertThat(UnitCatalog.FullRoster.First(d => d.Id == id).StrongVsElement)
                .OverrideFailureMessage($"{id} counter direction drifted").IsEqual(expected);
        }
    }

    [TestCase]
    public void WarbandTypeClassDistributionMatchesDesign()
    {
        // roster-expansion-40.md §4: Melee 11 / Ranged 6 / Sniper 4 / Aerial 7 / Siege 3 /
        // Support 4 over the 35 warband (excludes the 5 dragons + the rental).
        var warband = Roster40().Where(d => d.Rarity != Rarity.Draconic).ToList();
        var byClass = warband.GroupBy(d => d.TypeClass).ToDictionary(g => g.Key, g => g.Count());
        AssertThat(warband.Count).IsEqual(35);
        AssertThat(byClass.GetValueOrDefault(TypeClass.Melee)).IsEqual(11);
        AssertThat(byClass.GetValueOrDefault(TypeClass.Ranged)).IsEqual(6);
        AssertThat(byClass.GetValueOrDefault(TypeClass.Sniper)).IsEqual(4);
        AssertThat(byClass.GetValueOrDefault(TypeClass.Aerial)).IsEqual(7);
        AssertThat(byClass.GetValueOrDefault(TypeClass.Siege)).IsEqual(3);
        AssertThat(byClass.GetValueOrDefault(TypeClass.Support)).IsEqual(4);
    }

    [TestCase]
    public void MossmitesMarkHasARosterPayoff()
    {
        // Resolves Q3: mossmite's counter-flip is no longer inert — at least one allied unit
        // counters the element it imprints, so the mark sets up real bonus damage.
        var mark = UnitCatalog.FullRoster.First(d => d.Id == "mossmite").OverrideTargetElement;
        AssertThat(mark).IsNotNull();
        var counterers = UnitCatalog.FullRoster.Count(
            d => d.StrongVsElement == mark || d.MassiveVsElement == mark);
        AssertThat(counterers > 0)
            .OverrideFailureMessage($"no unit counters mossmite's mark ({mark})").IsTrue();
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
