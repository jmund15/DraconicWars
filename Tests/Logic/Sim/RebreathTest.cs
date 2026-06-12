namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Meta;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class RebreathTest
{
    private static (BattleSim sim, BattleState state) CreateBattle(params UnitDef[] defs)
    {
        var roster = defs.Length > 0
            ? defs
            : new[]
            {
                TestUnits.Grunt(),
                TestUnits.Grunt(id: "g2"),
                TestUnits.Grunt(id: "g3"),
                TestUnits.Grunt(id: "g4"),
                TestUnits.Grunt(id: "g5"),
            };
        var sim = new BattleSim(
            BattleConfig.Default with { EdictsPerTier = 0 }, roster);
        var state = sim.CreateInitialState(3UL);
        state.Left.Mana = 5000f;
        state.Left.WalletCap = 50000f;
        return (sim, state);
    }

    [TestCase]
    public void RebreathingSwapsFutureDeploysOnly()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });
        var fieldedBefore = state.Units[0];

        var manaBefore = state.Left.Mana;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Fire),
        });
        AssertThat(state.Left.Mana).IsEqualApprox(
            manaBefore - TestUnits.Grunt().DeployCost * state.Config.RebreathCostFactor + 0.4f,
            0.5f);

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "g2") });
        sim.Advance(state, SimCommand.None);
        for (var i = 0; i < TestUnits.Grunt().DeployCooldownTicks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "grunt") });

        AssertThat(fieldedBefore.Def.Element).IsEqual(Element.Stone);
        var attunedUnit = state.Units[^1];
        AssertThat(attunedUnit.Def.Id).IsEqual("grunt");
        AssertThat(attunedUnit.Def.Element).IsEqual(Element.Fire);
        AssertThat(attunedUnit.Def.NativeElement).IsEqual(Element.Stone);
    }

    [TestCase]
    public void ACompanyReswearsOnlyOncePerDuel()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Fire),
        });
        var manaAfterFirst = state.Left.Mana;

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Frost),
        });

        AssertThat(state.Left.AttunedThisBattle["grunt"]).IsEqual(Element.Fire);
        AssertThat(state.Left.Mana >= manaAfterFirst).IsTrue();
    }

    [TestCase]
    public void RebreathingRejectedWhenUnaffordableOrSameElement()
    {
        var (sim, state) = CreateBattle();
        state.Left.Mana = 5f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Fire),
        });
        AssertThat(state.Left.AttunedThisBattle.ContainsKey("grunt")).IsFalse();

        state.Left.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Stone),
        });
        AssertThat(state.Left.AttunedThisBattle.ContainsKey("grunt")).IsFalse();
    }

    [TestCase]
    public void AttunedCapLimitsTheSynergyTally()
    {
        var (sim, state) = CreateBattle();
        foreach (var id in new[] { "grunt", "g2", "g3" })
        {
            sim.Advance(state, new List<SimCommand>
            {
                SimCommand.AttuneUnit(PlayerSide.Left, id, Element.Fire),
            });
        }
        foreach (var id in new[] { "grunt", "g2", "g3" })
        {
            sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, id) });
            state.Units[^1].X = 5f + state.Units.Count;
        }

        // 3 attuned Fire types fielded, but only 2 count toward Resonance.
        AssertThat(ElementSynergies.TierFor(state, PlayerSide.Left, Element.Fire)).IsEqual(1);

        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "g4") });
        state.Units[^1].X = 30f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "g5", Element.Fire),
        });
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "g5") });
        state.Units[^1].X = 32f;

        // Still 2 attuned counted; natives would lift it, attuned cannot.
        AssertThat(ElementSynergies.TierFor(state, PlayerSide.Left, Element.Fire)).IsEqual(1);
    }

    [TestCase]
    public void EachPaidReswearRaisesTheNextPrice()
    {
        var (sim, state) = CreateBattle();
        var baseCost = TestUnits.Grunt().DeployCost * state.Config.RebreathCostFactor;
        var step = state.Config.RebreathCostStepPct;

        foreach (var (id, priorPaid) in new[] { ("grunt", 0), ("g2", 1), ("g3", 2) })
        {
            var manaBefore = state.Left.Mana;
            sim.Advance(state, new List<SimCommand>
            {
                SimCommand.AttuneUnit(PlayerSide.Left, id, Element.Fire),
            });
            var expected = baseCost * (1f + priorPaid * step);
            AssertThat(state.Left.Mana).IsEqualApprox(manaBefore - expected + 0.4f, 0.5f);
        }
    }

    [TestCase]
    public void FreeReswearsDoNotEscalateThePaidPrice()
    {
        var (sim, state) = CreateBattle();
        state.Left.FreeAttunements = 1;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Fire),
        });

        var manaBefore = state.Left.Mana;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "g2", Element.Frost),
        });

        var baseCost = TestUnits.Grunt(id: "g2").DeployCost * state.Config.RebreathCostFactor;
        AssertThat(state.Left.Mana).IsEqualApprox(manaBefore - baseCost + 0.4f, 0.5f);
    }

    [TestCase]
    public void PrismParleyGrantsFreeReswears_ThenManaPricingResumes()
    {
        var (sim, state) = CreateBattle();
        state.Left.FreeAttunements = 2;
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "grunt", Element.Fire),
            SimCommand.AttuneUnit(PlayerSide.Left, "g2", Element.Frost),
        });

        AssertThat(state.Left.FreeAttunements).IsEqual(0);
        AssertThat(state.Left.AttunedThisBattle.Count).IsEqual(2);
        // Free re-swears spent no mana (drip may have added a tick's worth).
        AssertThat(state.Left.Mana >= manaBefore).IsTrue();

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.AttuneUnit(PlayerSide.Left, "g3", Element.Storm),
        });
        AssertThat(state.Left.AttunedThisBattle.Count).IsEqual(3);
        AssertThat(state.Left.Mana < manaBefore).IsTrue();
    }

    [TestCase]
    public void MetaUnlockCostsGoldAndFeedsDragonRank()
    {
        var profile = new PlayerProfile { Gold = 1000 };
        profile.UnitLevels["grunt"] = 1;
        var rankBefore = MetaProgression.DragonRank(profile);

        var bought = MetaProgression.TryBuyAttunement(
            profile, "grunt", Element.Fire, unitTier: 1, nativeElement: Element.Stone);

        AssertThat(bought).IsTrue();
        AssertThat(profile.Gold).IsEqual(900);
        AssertThat(profile.AttunementsOwned.Contains("grunt:Fire")).IsTrue();
        AssertThat(MetaProgression.DragonRank(profile)).IsEqual(rankBefore + 1);

        AssertThat(MetaProgression.TryBuyAttunement(
            profile, "grunt", Element.Fire, 1, Element.Stone)).IsFalse();
        AssertThat(MetaProgression.TryBuyAttunement(
            profile, "grunt", Element.Stone, 1, Element.Stone)).IsFalse();
    }
}
