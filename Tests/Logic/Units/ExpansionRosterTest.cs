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

    [TestCase]
    public void EmberArbalestIsAGlassSingleTargetGroundSniper()
    {
        var u = Find("ember_arbalest");
        AssertThat(u.TypeClass).IsEqual(TypeClass.Sniper);
        AssertThat(u.Element).IsEqual(Element.Fire);
        AssertThat(u.Rarity).IsEqual(Rarity.Common);
        AssertThat(u.IsArea).IsFalse();
        AssertThat(u.CanTargetAir).IsFalse();
        // Sniper dead-zone: it cannot fight point-blank, so a rusher beats it.
        AssertThat(u.RangeMin > 0f).IsTrue();
    }

    [TestCase]
    public void GlideMantaIsACommonAntiAirInterceptor()
    {
        var u = Find("glide_manta");
        AssertThat(u.TypeClass).IsEqual(TypeClass.Aerial);
        AssertThat(u.Element).IsEqual(Element.Frost);
        AssertThat(u.Rarity).IsEqual(Rarity.Common);
        AssertThat(u.PrefersAirTarget).IsTrue();
        AssertThat(u.CanTargetAir).IsTrue();
        // No strafe — it holds airspace rather than overshooting.
        AssertThat(u.StrafeDistance).IsEqual(0f);
    }

    [TestCase]
    public void SporeWispIsACommonPhasingVenomHarasser()
    {
        var u = Find("spore_wisp");
        AssertThat(u.TypeClass).IsEqual(TypeClass.Ranged);
        AssertThat(u.Element).IsEqual(Element.Venom);
        AssertThat(u.Rarity).IsEqual(Rarity.Common);
        AssertThat(u.CanTargetAir).IsTrue();
        // Periodic i-frame window: phased ticks are a real fraction of the cycle.
        AssertThat(u.PhaseCadenceTicks > 0).IsTrue();
        AssertThat(u.PhaseDurationTicks > 0 && u.PhaseDurationTicks < u.PhaseCadenceTicks).IsTrue();
    }
}
