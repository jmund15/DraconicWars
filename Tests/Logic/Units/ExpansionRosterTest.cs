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

    [TestCase]
    public void CragTyrantIsAnEpicGrabbingRoc()
    {
        var u = Find("crag_tyrant");
        AssertThat(u.TypeClass).IsEqual(TypeClass.Aerial);
        AssertThat(u.Element).IsEqual(Element.Stone);
        AssertThat(u.Rarity).IsEqual(Rarity.Epic);
        AssertThat(u.CanTargetAir).IsTrue();
        // The grab is its kit: it throws AND stuns, not just nudges.
        AssertThat(u.GrabThrowDistance > 0f).IsTrue();
        AssertThat(u.GrabStunTicks > 0).IsTrue();
    }

    [TestCase]
    public void TempestChoirIsAnEpicConduitHasteSupport()
    {
        var u = Find("tempest_choir");
        AssertThat(u.TypeClass).IsEqual(TypeClass.Support);
        AssertThat(u.Element).IsEqual(Element.Storm);
        AssertThat(u.Rarity).IsEqual(Rarity.Epic);
        // Both auras present: living conduit + haste-halo with a real radius.
        AssertThat(u.ConduitManaPerSecond > 0f).IsTrue();
        AssertThat(u.HasteHaloSpeedPct > 0f && u.HasteHaloRadius > 0f).IsTrue();
    }

    [TestCase]
    public void MossmiteIsARareCounterFlipper()
    {
        var u = Find("mossmite");
        AssertThat(u.TypeClass).IsEqual(TypeClass.Ranged);
        AssertThat(u.Element).IsEqual(Element.Venom);
        AssertThat(u.Rarity).IsEqual(Rarity.Rare);
        AssertThat(u.CanTargetAir).IsTrue();
        // The mark is its kit: it rewrites the target's defensive element for a window.
        AssertThat(u.OverrideTargetElement).IsNotNull();
        AssertThat(u.OverrideTargetTicks > 0).IsTrue();
    }

    [TestCase]
    public void VoltheraxIsADraconicPiercingStormDragon()
    {
        var u = Find("voltherax");
        AssertThat(u.Rarity).IsEqual(Rarity.Draconic);
        AssertThat(u.Element).IsEqual(Element.Storm);
        AssertThat(u.Tier).IsEqual(4);
        AssertThat(u.ProjectilePierces).IsTrue();
        AssertThat(u.ManaRefundPerKill > 0).IsTrue();
        // Escrow-summoned crescendo, not mana-deployed.
        AssertThat(u.DeployCost).IsEqual(0);
    }

    [TestCase]
    public void TerravosskIsADraconicUnstaggerableShockwaveDragon()
    {
        var u = Find("terravossk");
        AssertThat(u.Rarity).IsEqual(Rarity.Draconic);
        AssertThat(u.Element).IsEqual(Element.Stone);
        AssertThat(u.Tier).IsEqual(4);
        AssertThat(u.Unstaggerable).IsTrue();
        AssertThat(u.ShockwaveDamage > 0 && u.ShockwaveRange > 0f).IsTrue();
        AssertThat(u.DeployCost).IsEqual(0);
    }

    [TestCase]
    public void GlacerethIsADraconicFreezeFieldDragon()
    {
        var u = Find("glacereth");
        AssertThat(u.Rarity).IsEqual(Rarity.Draconic);
        AssertThat(u.Element).IsEqual(Element.Frost);
        AssertThat(u.Tier).IsEqual(4);
        AssertThat(u.ZoneRadius > 0f).IsTrue();
        AssertThat(u.ZoneFreezeDwellTicks > 0 && u.ZoneFreezeTicks > 0).IsTrue();
        AssertThat(u.DeployCost).IsEqual(0);
    }

    [TestCase]
    public void SythraalIsADraconicContagionDragon()
    {
        var u = Find("sythraal");
        AssertThat(u.Rarity).IsEqual(Rarity.Draconic);
        AssertThat(u.Element).IsEqual(Element.Venom);
        AssertThat(u.Tier).IsEqual(4);
        AssertThat(u.PoisonOnHitTicks > 0).IsTrue();
        AssertThat(u.ContagionRadius > 0f).IsTrue();
        AssertThat(u.DeployCost).IsEqual(0);
    }

    [TestCase]
    public void RimecoilIsARareFreezeSniper()
    {
        var u = Find("rimecoil");
        AssertThat(u.Rarity).IsEqual(Rarity.Rare);
        AssertThat(u.Element).IsEqual(Element.Frost);
        AssertThat(u.TypeClass).IsEqual(TypeClass.Sniper);
        AssertThat(u.ProjectileSpeed > 0f).IsTrue();
        AssertThat(u.ProjectileFreezeTicks > 0).IsTrue();
        AssertThat(u.BonusVsImpairedPct > 0f).IsTrue();
    }

    [TestCase]
    public void PlaguechargerIsAnUncommonDismountingCharger()
    {
        var u = Find("plaguecharger");
        AssertThat(u.Rarity).IsEqual(Rarity.Uncommon);
        AssertThat(u.Element).IsEqual(Element.Venom);
        AssertThat(u.TypeClass).IsEqual(TypeClass.Melee);
        // Charges fast, dismounts slower, and leaves a venom wake on the sacrifice.
        AssertThat(u.DismountSpeed > 0f && u.DismountSpeed < u.MoveSpeed).IsTrue();
        AssertThat(u.ZoneRadius > 0f).IsTrue();
    }

    [TestCase]
    public void StormwrightIsAMythicLobbedSplashSiege()
    {
        var u = Find("stormwright");
        AssertThat(u.Rarity).IsEqual(Rarity.Mythic);
        AssertThat(u.Element).IsEqual(Element.Storm);
        AssertThat(u.TypeClass).IsEqual(TypeClass.Siege);
        AssertThat(u.Tier).IsEqual(3);
        // A dodgeable/blockable lobbed shell with splash — its counterplay window.
        AssertThat(u.ProjectileSpeed > 0f).IsTrue();
        AssertThat(u.ProjectileSplashRadius > 0f).IsTrue();
    }

    [TestCase]
    public void SporekeepIsAMythicEscrowConduitSpawner()
    {
        var u = Find("sporekeep_monolith");
        AssertThat(u.Rarity).IsEqual(Rarity.Mythic);
        AssertThat(u.Element).IsEqual(Element.Venom);
        AssertThat(u.Tier).IsEqual(3);
        AssertThat(u.ConduitEscrowPerSecond > 0f).IsTrue();
        AssertThat(u.SpawnDefId).IsEqual("spore_wisp");
        AssertThat(u.SpawnCap > 0).IsTrue();
    }

    [TestCase]
    public void CloudwhaleIsAMythicShelterBarge()
    {
        var u = Find("frostbarge_cloudwhale");
        AssertThat(u.Rarity).IsEqual(Rarity.Mythic);
        AssertThat(u.Element).IsEqual(Element.Frost);
        AssertThat(u.Tier).IsEqual(3);
        AssertThat(u.Unstaggerable).IsTrue();
        // A pure enabler: shelter aura, zero offense.
        AssertThat(u.ShelterDrPct > 0f && u.ShelterRegenPerTick > 0).IsTrue();
        AssertThat(u.Damage).IsEqual(0);
    }

    [TestCase]
    public void TheTitheIsAMythicBurrowingEconomySapper()
    {
        var u = Find("the_tithe");
        AssertThat(u.Rarity).IsEqual(Rarity.Mythic);
        AssertThat(u.Element).IsEqual(Element.Stone);
        AssertThat(u.Tier).IsEqual(3);
        // Burrows untargetable, then siphons mana + stalls escrow on contact.
        AssertThat(u.PhaseCadenceTicks > 0 && u.PhaseDurationTicks > 0).IsTrue();
        AssertThat(u.DrainManaOnContact > 0).IsTrue();
        AssertThat(u.EscrowStallOnContact > 0).IsTrue();
    }

    [TestCase]
    public void AllFourMythicsAreMythic()
    {
        foreach (var id in new[]
                 { "stormwright", "frostbarge_cloudwhale", "sporekeep_monolith", "the_tithe" })
        {
            AssertThat(Find(id).Rarity).IsEqual(Rarity.Mythic);
        }
    }

    [TestCase]
    public void AllFiveDragonsAreDraconic()
    {
        foreach (var id in new[] { "pyraxis", "voltherax", "glacereth", "sythraal", "terravossk" })
        {
            AssertThat(Find(id).Rarity).IsEqual(Rarity.Draconic);
        }
    }
}
