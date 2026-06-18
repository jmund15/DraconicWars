namespace DraconicWars.Tests.Logic.Units;

using DraconicWars.Meta;
using DraconicWars.Sim.Units;
using DraconicWars.Tests.Logic.Sim;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Unit Rarity &amp; Data Model — collection rarity decoupled from battle Tier
/// (roster-expansion-40.md §1-2). Pure-CLR Logic: UnitDef is a record, MetaProgression
/// is pure static, so no Godot runtime is required.
/// </summary>
[TestSuite]
public class RarityModelTest
{
    [TestCase]
    public void RarityLadderOrdersWarbandTiersBelowDraconic()
    {
        AssertThat((int)Rarity.Common).IsEqual(0);
        AssertThat((int)Rarity.Uncommon).IsEqual(1);
        AssertThat((int)Rarity.Rare).IsEqual(2);
        AssertThat((int)Rarity.Epic).IsEqual(3);
        AssertThat((int)Rarity.Mythic).IsEqual(4);
        AssertThat((int)Rarity.Draconic).IsEqual(5);
    }

    [TestCase]
    public void UnitDefDefaultsToCommonRarity()
    {
        AssertThat(TestUnits.Grunt().Rarity).IsEqual(Rarity.Common);
    }

    [TestCase]
    public void RarityIsIndependentOfBattleTier()
    {
        // The decoupling promise: a Mythic can sit at any Tier; a Common can be Tier III.
        var mythicEarly = TestUnits.Grunt() with { Tier = 1, Rarity = Rarity.Mythic };
        var commonLate = TestUnits.Grunt() with { Tier = 3, Rarity = Rarity.Common };
        AssertThat(mythicEarly.Rarity).IsEqual(Rarity.Mythic);
        AssertThat(mythicEarly.Tier).IsEqual(1);
        AssertThat(commonLate.Rarity).IsEqual(Rarity.Common);
        AssertThat(commonLate.Tier).IsEqual(3);
    }

    [TestCase]
    public void SigilUnlockCostAscendsAndCommonIsFree()
    {
        AssertThat(MetaProgression.SigilUnlockCost(Rarity.Common)).IsEqual(0);
        AssertThat(MetaProgression.SigilUnlockCost(Rarity.Uncommon)).IsEqual(1);
        AssertThat(MetaProgression.SigilUnlockCost(Rarity.Rare)).IsEqual(3);
        AssertThat(MetaProgression.SigilUnlockCost(Rarity.Epic)).IsEqual(8);
        AssertThat(MetaProgression.SigilUnlockCost(Rarity.Mythic)).IsEqual(20);
    }

    [TestCase]
    public void DraconicUnlocksViaBondNotSigils()
    {
        // Dragons acquire via egg/bond, never the warband Sigil schedule.
        var threw = false;
        try
        {
            MetaProgression.SigilUnlockCost(Rarity.Draconic);
        }
        catch (System.ArgumentOutOfRangeException)
        {
            threw = true;
        }
        AssertThat(threw).IsTrue();
    }

    [TestCase]
    public void LoadoutAllowsAtMostOneMythic()
    {
        var common = TestUnits.Grunt("c");
        var mythicA = TestUnits.Grunt("m_a") with { Rarity = Rarity.Mythic };
        var mythicB = TestUnits.Grunt("m_b") with { Rarity = Rarity.Mythic };

        AssertThat(MetaProgression.LoadoutMythicCapOk(new[] { common, mythicA })).IsTrue();
        AssertThat(MetaProgression.LoadoutMythicCapOk(new[] { mythicA, mythicB })).IsFalse();
    }
}
