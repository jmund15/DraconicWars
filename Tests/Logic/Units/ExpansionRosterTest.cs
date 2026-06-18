namespace DraconicWars.Tests.Logic.Units;

using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Roster-expansion-to-40 units (roster-expansion-40.md §4): pins each new unit's
/// kit-data wiring over the shipped sim primitives. Grows one unit at a time as the
/// expansion lands (each gated on its rendered art via RosterArtContractTest).
/// </summary>
[TestSuite]
public class ExpansionRosterTest
{
    private static UnitDef Find(string id) => UnitCatalog.FullRoster.First(d => d.Id == id);

    [TestCase]
    public void SkylanceIsAnAerialRealProjectileStriker()
    {
        var skylance = Find("skylance_emberknight");
        AssertThat(skylance.ProjectileSpeed > 0f).IsTrue();
        AssertThat(skylance.TypeClass).IsEqual(TypeClass.Aerial);
        AssertThat(skylance.Element).IsEqual(Element.Fire);
        AssertThat(skylance.Rarity).IsEqual(Rarity.Uncommon);
        AssertThat(skylance.CanTargetAir).IsTrue();
    }
}
