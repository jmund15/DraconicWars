namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Augments;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Core;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class AugmentTest
{
    private static BattleConfig WindowedConfig => BattleConfig.Default with
    {
        AugmentWindowTicks = new[] { 60 },
    };

    private static (BattleSim sim, BattleState state) CreateBattle(BattleConfig config)
    {
        var sim = new BattleSim(
            config,
            new[] { TestUnits.Grunt(), TestUnits.Grunt(id: "fire2") with { Element = DraconicWars.Sim.Units.Element.Fire } },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            AugmentCatalog.All);
        var state = sim.CreateInitialState(7UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        return (sim, state);
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void TierPathTableIsNormalizedAndEveryRowGuaranteesGoldOrBetter()
    {
        var total = 0f;
        foreach (var row in TierPath.WeightedSequences)
        {
            total += row.Weight;
            var hasGoldPlus = row.Tiers.Any(t => t != AugmentTier.Silver);
            AssertThat(hasGoldPlus).IsTrue();
            AssertThat(row.Tiers.Length).IsEqual(3);
        }
        AssertThat(total).IsEqualApprox(1f, 0.001f);
    }

    [TestCase]
    public void TierPathRollIsDeterministicPerSeed()
    {
        var a = TierPath.Roll(new SimRng(5UL));
        var b = TierPath.Roll(new SimRng(5UL));
        AssertThat(a.SequenceEqual(b)).IsTrue();
    }

    [TestCase]
    public void WindowFreezesTheSimUntilBothSidesPick()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);

        AssertThat(state.Left.AwaitingDraft).IsTrue();
        AssertThat(state.Right.AwaitingDraft).IsTrue();
        AssertThat(state.Left.PendingOffers.Count).IsEqual(3);
        var frozenTick = state.Tick;
        var frozenMana = state.Left.Mana;

        AdvanceTicks(sim, state, 10);
        AssertThat(state.Tick).IsEqual(frozenTick);
        AssertThat(state.Left.Mana).IsEqualApprox(frozenMana, 0.001f);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.PickAugment(PlayerSide.Left, state.Left.PendingOffers[0]),
        });
        AssertThat(state.Tick).IsEqual(frozenTick);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.PickAugment(PlayerSide.Right, state.Right.PendingOffers[0]),
        });
        sim.Advance(state, SimCommand.None);
        AssertThat(state.Tick > frozenTick).IsTrue();
        AssertThat(state.Left.AwaitingDraft).IsFalse();
    }

    [TestCase]
    public void PickedAugmentAppliesItsEffect()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);

        // Force a known augment: ley_tap grants +3 drip/s.
        state.Left.PendingOffers[0] = "ley_tap";
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.PickAugment(PlayerSide.Left, "ley_tap"),
            SimCommand.PickAugment(PlayerSide.Right, state.Right.PendingOffers[0]),
        });

        AssertThat(state.Left.PickedAugments.Contains("ley_tap")).IsTrue();
        AssertThat(state.Left.Buffs.DripBonusPerSecond >= 3f).IsTrue();
    }

    [TestCase]
    public void RerollReplacesAllThreeWithinSameTierOnce()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);
        var tier = state.AugmentTierPath[0];
        var before = state.Left.PendingOffers.ToList();
        AssertThat(state.Left.RerollsLeft).IsEqual(1);

        sim.Advance(state, new List<SimCommand> { SimCommand.RerollOffers(PlayerSide.Left) });

        AssertThat(state.Left.RerollsLeft).IsEqual(0);
        AssertThat(state.Left.PendingOffers.Count).IsEqual(3);
        foreach (var offer in state.Left.PendingOffers)
        {
            AssertThat(AugmentCatalog.ById(offer).Tier).IsEqual(tier);
        }

        var offersAfterFirstReroll = state.Left.PendingOffers.ToList();
        sim.Advance(state, new List<SimCommand> { SimCommand.RerollOffers(PlayerSide.Left) });
        AssertThat(state.Left.PendingOffers.SequenceEqual(offersAfterFirstReroll)).IsTrue();
    }

    [TestCase]
    public void OffersAreDistinctAndMatchThePathTier()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);

        var tier = state.AugmentTierPath[0];
        var offers = state.Left.PendingOffers;
        AssertThat(offers.Distinct().Count()).IsEqual(3);
        foreach (var offer in offers)
        {
            AssertThat(AugmentCatalog.ById(offer).Tier).IsEqual(tier);
        }
    }

    [TestCase]
    public void NoWindowsConfiguredMeansNoFreezes()
    {
        var config = BattleConfig.Default with { AugmentWindowTicks = System.Array.Empty<int>() };
        var (sim, state) = CreateBattle(config);

        AdvanceTicks(sim, state, 200);

        AssertThat(state.Tick).IsEqual(200);
        AssertThat(state.Left.AwaitingDraft).IsFalse();
    }

    [TestCase]
    public void SameSeedProducesIdenticalOffers()
    {
        var (simA, stateA) = CreateBattle(WindowedConfig);
        var (simB, stateB) = CreateBattle(WindowedConfig);
        AdvanceTicks(simA, stateA, 61);
        AdvanceTicks(simB, stateB, 61);

        AssertThat(stateA.Left.PendingOffers.SequenceEqual(stateB.Left.PendingOffers)).IsTrue();
        AssertThat(stateA.AugmentTierPath.SequenceEqual(stateB.AugmentTierPath)).IsTrue();
    }
}
