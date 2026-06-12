namespace DraconicWars.Tests.Logic.Meta;

using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Meta;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class WarStandardTest
{
    [TestCase]
    public void ClampScalesEveryUnitToLevelNine()
    {
        var defs = WarStandard.BuildPvpDefs(UnitCatalog.FirstPlayable);

        var kobold = defs.First(d => d.Id == "kobold_spearman");
        var baseKobold = UnitCatalog.FirstPlayable.First(d => d.Id == "kobold_spearman");
        var expected = MetaProgression.StatMultiplier(WarStandard.ClampLevel, 1);
        AssertThat(kobold.MaxHp).IsEqual(
            (int)System.MathF.Round(baseKobold.MaxHp * expected));

        // Dragons enter at level 9 — the clamp leaves them at base stats.
        var drake = defs.First(d => d.Id == "elder_drake");
        var baseDrake = UnitCatalog.FirstPlayable.First(d => d.Id == "elder_drake");
        AssertThat(drake.MaxHp).IsEqual(baseDrake.MaxHp);
    }

    [TestCase]
    public void TheClampIsProfileIndependent()
    {
        var a = WarStandard.BuildPvpDefs(UnitCatalog.FirstPlayable);
        var b = WarStandard.BuildPvpDefs(UnitCatalog.FirstPlayable);

        AssertThat(a.Select(d => d.MaxHp).SequenceEqual(b.Select(d => d.MaxHp))).IsTrue();
        AssertThat(a.Count).IsEqual(UnitCatalog.FirstPlayable.Count);
    }
}
