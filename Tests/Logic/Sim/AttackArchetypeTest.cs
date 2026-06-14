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
    public void AttackArchetype_ProducesForm_FalseForPhysical()
    {
        AssertThat(AttackArchetype.MeleePhysical.ProducesForm).IsFalse();
        // Magic but no form still produces nothing (a pure gesture / body-strike).
        var magicNoForm = new AttackArchetype(AttackClass.Magic, AttackPose.Channel, AttackForm.None);
        AssertThat(magicNoForm.ProducesForm).IsFalse();
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
}
