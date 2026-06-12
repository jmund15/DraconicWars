namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Pacts;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Core;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class AugmentTest
{
    private static BattleConfig WindowedConfig => BattleConfig.Default with
    {
        ParleyTicks = new[] { 60 },
    };

    private static (BattleSim sim, BattleState state) CreateBattle(BattleConfig config)
    {
        var sim = new BattleSim(
            config,
            new[] { TestUnits.Grunt(), TestUnits.Grunt(id: "fire2") with { Element = DraconicWars.Sim.Units.Element.Fire } },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            PactCatalog.All);
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
            var hasGoldPlus = row.Tiers.Any(t => t != PactTier.Ember);
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

        AssertThat(state.Left.AwaitingParley).IsTrue();
        AssertThat(state.Right.AwaitingParley).IsTrue();
        AssertThat(state.Left.PendingOffers.Count).IsEqual(3);
        var frozenTick = state.Tick;
        var frozenMana = state.Left.Mana;

        AdvanceTicks(sim, state, 10);
        AssertThat(state.Tick).IsEqual(frozenTick);
        AssertThat(state.Left.Mana).IsEqualApprox(frozenMana, 0.001f);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, state.Left.PendingOffers[0]),
        });
        AssertThat(state.Tick).IsEqual(frozenTick);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Right, state.Right.PendingOffers[0]),
        });
        sim.Advance(state, SimCommand.None);
        AssertThat(state.Tick > frozenTick).IsTrue();
        AssertThat(state.Left.AwaitingParley).IsFalse();
    }

    [TestCase]
    public void PickedAugmentAppliesItsEffect()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);

        // Force a known pact: ley_tap grants +3 drip/s.
        state.Left.PendingOffers[0] = "ley_tap";
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "ley_tap"),
            SimCommand.SealPact(PlayerSide.Right, state.Right.PendingOffers[0]),
        });

        AssertThat(state.Left.SealedPacts.Contains("ley_tap")).IsTrue();
        AssertThat(state.Left.Buffs.DripBonusPerSecond >= 3f).IsTrue();
    }

    [TestCase]
    public void PayingTheTitheCostsManaAndDrawsFreshTermsInTier()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);
        var tier = state.ParleyTierPath[0];
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });

        AssertThat(state.Left.TithesPaidThisParley).IsEqual(1);
        AssertThat(state.Left.Mana).IsEqualApprox(
            manaBefore - WindowedConfig.TitheCostMana, 0.001f);
        AssertThat(state.Left.PendingOffers.Count).IsEqual(3);
        foreach (var offer in state.Left.PendingOffers)
        {
            AssertThat(PactCatalog.ById(offer).Tier).IsEqual(tier);
        }

        // The Broker keeps dealing while the mana lasts — no free-reroll cap.
        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });
        AssertThat(state.Left.TithesPaidThisParley).IsEqual(2);
        AssertThat(state.Left.Mana).IsEqualApprox(
            manaBefore - 2 * WindowedConfig.TitheCostMana, 0.001f);
    }

    [TestCase]
    public void TitheIsRejectedWhenUnaffordable()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);
        state.Left.Mana = WindowedConfig.TitheCostMana - 1f;
        var offersBefore = state.Left.PendingOffers.ToList();

        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });

        AssertThat(state.Left.TithesPaidThisParley).IsEqual(0);
        AssertThat(state.Left.PendingOffers.SequenceEqual(offersBefore)).IsTrue();
    }

    [TestCase]
    public void TitheCountResetsAtTheNextParley()
    {
        var config = BattleConfig.Default with { ParleyTicks = new[] { 60, 120 } };
        var (sim, state) = CreateBattle(config);
        AdvanceTicks(sim, state, 61);
        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, state.Left.PendingOffers[0]),
            SimCommand.SealPact(PlayerSide.Right, state.Right.PendingOffers[0]),
        });

        AdvanceTicks(sim, state, 120);

        AssertThat(state.Left.AwaitingParley).IsTrue();
        AssertThat(state.Left.TithesPaidThisParley).IsEqual(0);
    }

    [TestCase]
    public void SealingAWyrmPactTakesItsBloodPriceFromTheSpire()
    {
        var bloodOath = new DraconicWars.Sim.Pacts.PactDef(
            "blood_oath", "Blood Oath", PactTier.Wyrm, PactCategory.Combat,
            Lore: "test", DamagePct: 0.3f, PriceSpireHpPct: 0.2f);
        var sim = new BattleSim(
            WindowedConfig,
            new[] { TestUnits.Grunt() },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            new[] { bloodOath });
        var state = sim.CreateInitialState(7UL);
        AdvanceTicks(sim, state, 61);
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("blood_oath");

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "blood_oath"),
        });

        AssertThat(state.LeftSpireHp).IsEqualApprox(
            WindowedConfig.SpireMaxHp * 0.8f, 0.01f);
        AssertThat(state.RightSpireHp).IsEqualApprox(WindowedConfig.SpireMaxHp, 0.01f);
    }

    [TestCase]
    public void OngoingDripPriceReducesIncomeButRespectsTheFloor()
    {
        var deepTithe = new DraconicWars.Sim.Pacts.PactDef(
            "deep_tithe", "Deep Tithe", PactTier.Wyrm, PactCategory.Economy,
            Lore: "test", WalletCapBonus: 500f, PriceDripPerSecond: 50f);
        var sim = new BattleSim(
            WindowedConfig,
            new[] { TestUnits.Grunt() },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            new[] { deepTithe });
        var state = sim.CreateInitialState(7UL);
        AdvanceTicks(sim, state, 61);
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("deep_tithe");
        state.Right.PendingOffers.Clear();
        state.Right.PendingOffers.Add("deep_tithe");
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "deep_tithe"),
            SimCommand.SealPact(PlayerSide.Right, "deep_tithe"),
        });
        var manaBefore = state.Left.Mana;

        AdvanceTicks(sim, state, 30);

        var expected = manaBefore + WindowedConfig.DripFloorPerSecond;
        AssertThat(state.Left.Mana).IsEqualApprox(expected, 0.05f);
    }

    [TestCase]
    public void EveryWyrmPactCarriesAPrice_AndLowerTiersAreFree()
    {
        foreach (var def in PactCatalog.All)
        {
            var hasPrice = def.PriceSpireHpPct > 0f || def.PriceDripPerSecond > 0f;
            if (def.Tier == PactTier.Wyrm)
            {
                AssertThat(hasPrice).IsTrue();
            }
            else
            {
                AssertThat(hasPrice).IsFalse();
            }
        }
    }

    [TestCase]
    public void EveryPactCarriesALoreLine()
    {
        foreach (var def in PactCatalog.All)
        {
            AssertThat(def.Lore.Length > 0).IsTrue();
        }
    }

    [TestCase]
    public void OffersAreDistinctAndMatchThePathTier()
    {
        var (sim, state) = CreateBattle(WindowedConfig);
        AdvanceTicks(sim, state, 61);

        var tier = state.ParleyTierPath[0];
        var offers = state.Left.PendingOffers;
        AssertThat(offers.Distinct().Count()).IsEqual(3);
        foreach (var offer in offers)
        {
            AssertThat(PactCatalog.ById(offer).Tier).IsEqual(tier);
        }
    }

    [TestCase]
    public void NoWindowsConfiguredMeansNoFreezes()
    {
        var config = BattleConfig.Default with { ParleyTicks = System.Array.Empty<int>() };
        var (sim, state) = CreateBattle(config);

        AdvanceTicks(sim, state, 200);

        AssertThat(state.Tick).IsEqual(200);
        AssertThat(state.Left.AwaitingParley).IsFalse();
    }

    [TestCase]
    public void SameSeedProducesIdenticalOffers()
    {
        var (simA, stateA) = CreateBattle(WindowedConfig);
        var (simB, stateB) = CreateBattle(WindowedConfig);
        AdvanceTicks(simA, stateA, 61);
        AdvanceTicks(simB, stateB, 61);

        AssertThat(stateA.Left.PendingOffers.SequenceEqual(stateB.Left.PendingOffers)).IsTrue();
        AssertThat(stateA.ParleyTierPath.SequenceEqual(stateB.ParleyTierPath)).IsTrue();
    }
}
