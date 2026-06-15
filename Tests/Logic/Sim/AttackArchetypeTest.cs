namespace DraconicWars.Tests.Logic.Sim;

using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Attack Archetype Data Model (arch-attack-archetypes §3): every unit declares a
/// class (Physical|Magic) x pose x form. Physical units swing a weapon already on
/// their sprite; Magic units with a non-None form spawn a view-layer element form.
/// Default is MeleePhysical so the legacy roster stays physical-swing unless the
/// catalog overrides.
/// </summary>
[TestSuite]
public class AttackArchetypeTest
{
    private static UnitDef Unit(string id) =>
        UnitCatalog.FullRoster.First(u => u.Id == id);

    [TestCase]
    public void AttackArchetype_Default_IsMeleePhysical()
    {
        var a = AttackArchetype.MeleePhysical;
        AssertThat(a.Class).IsEqual(AttackClass.Physical);
        AssertThat(a.Pose).IsEqual(AttackPose.Swing);
        AssertThat(a.Form).IsEqual(AttackForm.None);
    }

    [TestCase]
    public void AttackArchetype_ProducesForm_TrueForMagicWithForm()
    {
        var a = new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Shard);
        AssertThat(a.ProducesForm).IsTrue();
    }

    [TestCase]
    public void AttackArchetype_ProducesForm_FalseWhenNoForm()
    {
        // No form -> nothing spawns, regardless of class: a physical melee swing,
        // or a formless magic gesture / body-strike.
        AssertThat(AttackArchetype.MeleePhysical.ProducesForm).IsFalse();
        var magicNoForm = new AttackArchetype(AttackClass.Magic, AttackPose.Channel, AttackForm.None);
        AssertThat(magicNoForm.ProducesForm).IsFalse();
    }

    [TestCase]
    public void AttackArchetype_ProducesForm_TrueForPhysicalRanged()
    {
        // A physical archer fires a projectile form (arrow): form spawn is gated on
        // a non-None Form, not on the magic class. So archers visibly shoot too.
        var archer = new AttackArchetype(AttackClass.Physical, AttackPose.Shoot, AttackForm.Arrow);
        AssertThat(archer.ProducesForm).IsTrue();
    }

    [TestCase]
    public void UnitDef_DefaultAttack_IsMeleePhysical()
    {
        // A def that never sets Attack inherits the physical-swing default.
        AssertThat(Unit("kobold_spearman").Attack).IsEqual(AttackArchetype.MeleePhysical);
    }

    [TestCase]
    public void UnitCatalog_DeepwayBulwark_IsPhysical()
    {
        AssertThat(Unit("deepway_bulwark").Attack.Class).IsEqual(AttackClass.Physical);
    }

    [TestCase]
    public void UnitCatalog_BorealColossus_IsMagicProducesForm()
    {
        var a = Unit("boreal_colossus").Attack;
        AssertThat(a.Class).IsEqual(AttackClass.Magic);
        AssertThat(a.ProducesForm).IsTrue();
    }

    [TestCase]
    public void UnitCatalog_CasterUnits_AreMagic()
    {
        foreach (var id in new[] { "cinder_acolyte", "glacier_adept", "vale_chanter", "boreal_colossus" })
        {
            AssertThat(Unit(id).Attack.Class).IsEqual(AttackClass.Magic);
        }
    }

    [TestCase]
    public void UnitCatalog_PhysicalArchers_FireArrows()
    {
        // Bow/crossbow units are physical (weapon on the sprite) but still spawn a
        // projectile form so the shot is visible -- no more archers firing nothing.
        foreach (var id in new[] { "forest_archer", "dune_marksman", "quarry_slinger" })
        {
            var a = Unit(id).Attack;
            AssertThat(a.Class).IsEqual(AttackClass.Physical);
            AssertThat(a.Form).IsEqual(AttackForm.Arrow);
            AssertThat(a.ProducesForm).IsTrue();
        }
    }
}
