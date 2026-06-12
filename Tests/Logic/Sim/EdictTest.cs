namespace DraconicWars.Tests.Logic.Sim;

using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Edicts;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class EdictTest
{
    private static (BattleSim sim, BattleState state) CreateBattle(
        BattleConfig? config = null, params UnitDef[] defs)
    {
        var roster = defs.Length > 0
            ? defs
            : new[]
            {
                TestUnits.Grunt(),
                TestUnits.Grunt(id: "storm1") with { Element = Element.Storm },
                TestUnits.Grunt(id: "fire1") with { Element = Element.Fire },
            };
        var sim = new BattleSim(
            config ?? BattleConfig.Default, roster,
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            DraconicWars.Sim.Pacts.PactCatalog.All);
        var state = sim.CreateInitialState(21UL);
        return (sim, state);
    }

    [TestCase]
    public void EdictsRollDeterministicallyAndCoverAllThreeSegments()
    {
        var (_, stateA) = CreateBattle();
        var (_, stateB) = CreateBattle();

        AssertThat(stateA.Edicts.Count)
            .IsEqual(BattleConfig.Default.EdictsPerTier * 3);
        AssertThat(stateA.Edicts.Select(e => e.Def.Id)
            .SequenceEqual(stateB.Edicts.Select(e => e.Def.Id))).IsTrue();
        foreach (var tierIndex in new[] { 0, 1, 2 })
        {
            AssertThat(stateA.Edicts.Count(e => e.TierIndex == tierIndex))
                .IsEqual(BattleConfig.Default.EdictsPerTier);
        }
        AssertThat(stateA.Edicts.Select(e => e.Def.Id).Distinct().Count())
            .IsEqual(stateA.Edicts.Count);
    }

    [TestCase]
    public void FirstClaimantTakesTheFullSurge_RunnerUpCollectsHalf()
    {
        // 3 per tier exhausts the 8-entry pool, guaranteeing the Kills edict rolls.
        var config = BattleConfig.Default with { EdictsPerTier = 3 };
        var (sim, state) = CreateBattle(config);
        var edict = state.Edicts.First(e => e.Def.Kind == EdictKind.Kills);
        var gap = state.Config.AscensionThresholds[edict.TierIndex]
            - (edict.TierIndex > 0 ? state.Config.AscensionThresholds[edict.TierIndex - 1] : 0f);
        var fullSurge = gap * state.Config.EdictSurgePct;

        state.Left.Kills = (int)edict.Def.Threshold;
        var leftBefore = state.Left.AscensionMeter;
        sim.Advance(state, SimCommand.None);

        AssertThat(edict.FirstClaimant).IsEqual(PlayerSide.Left);
        AssertThat(state.Left.AscensionFromEdicts).IsEqualApprox(fullSurge, 0.01f);
        AssertThat(state.Left.AscensionMeter >= leftBefore + fullSurge).IsTrue();

        state.Right.Kills = (int)edict.Def.Threshold;
        sim.Advance(state, SimCommand.None);

        AssertThat(state.Right.AscensionFromEdicts)
            .IsEqualApprox(fullSurge * state.Config.EdictRunnerUpPct, 0.01f);
    }

    [TestCase]
    public void ElementEdictsOnlyRollWhenTheElementIsFieldable()
    {
        var stoneOnly = new[] { TestUnits.Grunt() };
        var (_, state) = CreateBattle(config: null, defs: stoneOnly);

        foreach (var edict in state.Edicts)
        {
            if (edict.Def.RequiredElement is { } element)
            {
                AssertThat(element).IsEqual(Element.Stone);
            }
        }
    }

    [TestCase]
    public void EdictSurgeCanCascadeIntoTierUpAndParley()
    {
        var config = BattleConfig.Default with { EdictsPerTier = 3 };
        var (sim, state) = CreateBattle(config);
        var edict = state.Edicts.First(e => e.Def.Kind == EdictKind.Kills);
        state.Left.AscensionMeter = state.Config.AscensionThresholds[0] - 1f;
        state.Left.Kills = (int)edict.Def.Threshold;

        sim.Advance(state, SimCommand.None);

        AssertThat(state.Left.AscensionTier).IsEqual(2);
        AssertThat(state.Left.AwaitingParley).IsTrue();
    }

    [TestCase]
    public void ZeroEdictsPerTierDisablesTheTrials()
    {
        var config = BattleConfig.Default with { EdictsPerTier = 0 };
        var (_, state) = CreateBattle(config);

        AssertThat(state.Edicts.Count).IsEqual(0);
    }

    [TestCase]
    public void EveryEdictCarriesLore()
    {
        foreach (var def in EdictCatalog.All)
        {
            AssertThat(def.Lore.Length > 0).IsTrue();
            AssertThat(def.DisplayName.Length > 0).IsTrue();
        }
    }
}
