namespace DraconicWars.Tests.Logic.Battle;

using DraconicWars.Game.Battle.Hud;
using DraconicWars.Sim.Conduits;
using DraconicWars.Sim.Pacts;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class EffectTextTest
{
    [TestCase]
    public void ConduitTextScalesWithTier()
    {
        var manaWell = ConduitDefs.ById("mana_well");

        AssertThat(EffectText.ForConduit(manaWell, 1)).IsEqual("+2 mana/s");
        AssertThat(EffectText.ForConduit(manaWell, 3)).IsEqual("+6 mana/s");
    }

    [TestCase]
    public void ConduitTextJoinsMultipleEffects()
    {
        var vault = ConduitDefs.ById("aurum_vault");

        var text = EffectText.ForConduit(vault, 2);
        AssertThat(text.Contains("+300 cap")).IsTrue();
        AssertThat(text.Contains("+40% bounty")).IsTrue();
    }

    [TestCase]
    public void PactTextListsBoons()
    {
        var avatar = PactCatalog.ById("avatar_of_war");

        var text = EffectText.ForPact(avatar);
        AssertThat(text.Contains("+30% dmg")).IsTrue();
        AssertThat(text.Contains("+12% speed")).IsTrue();
    }

    [TestCase]
    public void PactPriceTextNamesEveryPrice()
    {
        var covenant = PactCatalog.ById("draconic_covenant");

        var text = EffectText.ForPactPrice(covenant);
        AssertThat(text.Contains("10% spire blood")).IsTrue();
        AssertThat(text.Contains("-2 drip")).IsTrue();
    }

    [TestCase]
    public void PactPriceTextEmptyForFreeTiers()
    {
        var leyTap = PactCatalog.ById("ley_tap");

        AssertThat(EffectText.ForPactPrice(leyTap)).IsEqual(string.Empty);
    }

    [TestCase]
    public void EveryConduitAndPactProducesNonEmptyEffectText()
    {
        foreach (var conduit in ConduitDefs.All)
        {
            AssertThat(EffectText.ForConduit(conduit, 1).Length > 0).IsTrue();
        }
        foreach (var pact in PactCatalog.All)
        {
            AssertThat(EffectText.ForPact(pact).Length > 0).IsTrue();
        }
    }

    [TestCase]
    public void EdictTextStatesTheRequirementPlainly()
    {
        var sky = DraconicWars.Sim.Edicts.EdictCatalog.All[0];

        AssertThat(EffectText.ForEdict(sky)).IsEqual("Deploy 400 mana of Storm units");
    }

    [TestCase]
    public void EveryEdictProducesNonEmptyRequirementText()
    {
        foreach (var edict in DraconicWars.Sim.Edicts.EdictCatalog.All)
        {
            AssertThat(EffectText.ForEdict(edict).Length > 0)
                .OverrideFailureMessage($"{edict.Id} renders no requirement text").IsTrue();
        }
    }
}
