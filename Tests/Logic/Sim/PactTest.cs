namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Core;
using DraconicWars.Sim.Pacts;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class PactTest
{
    private static (BattleSim sim, BattleState state) CreateBattle(BattleConfig? config = null)
    {
        var sim = new BattleSim(
            config ?? BattleConfig.Default,
            new[] { TestUnits.Grunt(), TestUnits.Grunt(id: "fire2") with { Element = DraconicWars.Sim.Units.Element.Fire } },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            PactCatalog.All);
        var state = sim.CreateInitialState(7UL);
        state.Left.WalletCap = 50000f;
        state.Right.WalletCap = 50000f;
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

    /// <summary>Pushes the side's meter to the brink and advances one tick — the
    /// tier-up queues a parley and ProcessParleys opens it within the same Advance.</summary>
    private static void ReachNextTier(BattleSim sim, BattleState state, PlayerSide side)
    {
        var player = state.Player(side);
        player.AscensionMeter =
            state.Config.AscensionThresholds[player.AscensionTier - 1] - 0.001f;
        sim.Advance(state, SimCommand.None);
    }

    [TestCase]
    public void TierPathTableIsNormalizedAndEveryRowGuaranteesDrakeOrBetter()
    {
        var total = 0f;
        foreach (var row in TierPath.WeightedSequences)
        {
            total += row.Weight;
            var hasDrakePlus = row.Tiers.Any(t => t != PactTier.Ember);
            AssertThat(hasDrakePlus).IsTrue();
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
    public void ParleyOpensOnTierUp_AndGameplayContinues()
    {
        var (sim, state) = CreateBattle();

        ReachNextTier(sim, state, PlayerSide.Left);

        AssertThat(state.Left.AscensionTier).IsEqual(2);
        AssertThat(state.Left.AwaitingParley).IsTrue();
        AssertThat(state.Left.PendingOffers.Count).IsEqual(3);
        AssertThat(state.Right.AwaitingParley).IsFalse();

        // The Broker does not stop the war: ticks and mana keep flowing.
        var tickBefore = state.Tick;
        var manaBefore = state.Left.Mana;
        AdvanceTicks(sim, state, 10);
        AssertThat(state.Tick).IsEqual(tickBefore + 10);
        AssertThat(state.Left.Mana > manaBefore).IsTrue();
    }

    [TestCase]
    public void OpenParleyAutoSealsItsFirstOfferAtTheDeadline()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);
        var firstOffer = state.Left.PendingOffers[0];

        AdvanceTicks(sim, state, state.Config.ParleyPickTicks + 2);

        AssertThat(state.Left.AwaitingParley).IsFalse();
        AssertThat(state.Left.SealedPacts.Count).IsEqual(1);
        AssertThat(state.Left.SealedPacts[0]).IsEqual(firstOffer);
    }

    [TestCase]
    public void EachSideConsumesTheSharedPathAtItsOwnPace()
    {
        var (sim, state) = CreateBattle();

        ReachNextTier(sim, state, PlayerSide.Left);
        AssertThat(state.Left.ParleysOpened).IsEqual(1);
        AssertThat(state.Right.ParleysOpened).IsEqual(0);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, state.Left.PendingOffers[0]),
        });
        ReachNextTier(sim, state, PlayerSide.Left);
        AssertThat(state.Left.ParleysOpened).IsEqual(2);

        ReachNextTier(sim, state, PlayerSide.Right);
        AssertThat(state.Right.ParleysOpened).IsEqual(1);
        // Right's first offers draw from the path's FIRST row even though Left is ahead.
        var firstTier = state.ParleyTierPath[0];
        foreach (var offer in state.Right.PendingOffers)
        {
            AssertThat(PactCatalog.ById(offer).Tier).IsEqual(firstTier);
        }
    }

    [TestCase]
    public void SealedPactAppliesItsEffect()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);

        // Force a known pact: ley_tap grants +3 drip/s.
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("ley_tap");
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "ley_tap"),
        });

        AssertThat(state.Left.SealedPacts.Contains("ley_tap")).IsTrue();
        AssertThat(state.Left.Buffs.DripBonusPerSecond >= 3f).IsTrue();
    }

    [TestCase]
    public void PayingTheTitheCostsManaAndDrawsFreshTermsInTier()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);
        var tier = state.ParleyTierPath[0];
        state.Left.Mana = 500f;

        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });

        AssertThat(state.Left.TithesPaidThisParley).IsEqual(1);
        AssertThat(state.Left.Mana <= 500f - state.Config.TitheCostMana + 2f).IsTrue();
        AssertThat(state.Left.PendingOffers.Count).IsEqual(3);
        foreach (var offer in state.Left.PendingOffers)
        {
            AssertThat(PactCatalog.ById(offer).Tier).IsEqual(tier);
        }

        // The Broker keeps dealing while the mana lasts — no free-reroll cap.
        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });
        AssertThat(state.Left.TithesPaidThisParley).IsEqual(2);
    }

    [TestCase]
    public void TitheIsRejectedWhenUnaffordable()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);
        state.Left.Mana = 10f;
        var offersBefore = state.Left.PendingOffers.ToList();

        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });

        AssertThat(state.Left.TithesPaidThisParley).IsEqual(0);
        AssertThat(state.Left.PendingOffers.SequenceEqual(offersBefore)).IsTrue();
    }

    [TestCase]
    public void TitheCountResetsAtTheNextParley()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);
        sim.Advance(state, new List<SimCommand> { SimCommand.PayTithe(PlayerSide.Left) });
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, state.Left.PendingOffers[0]),
        });

        ReachNextTier(sim, state, PlayerSide.Left);

        AssertThat(state.Left.AwaitingParley).IsTrue();
        AssertThat(state.Left.TithesPaidThisParley).IsEqual(0);
    }

    [TestCase]
    public void SealingAWyrmPactTakesItsBloodPriceFromTheSpire()
    {
        var bloodOath = new PactDef(
            "blood_oath", "Blood Oath", PactTier.Wyrm, PactCategory.Combat,
            Lore: "test", DamagePct: 0.3f, PriceSpireHpPct: 0.2f);
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { TestUnits.Grunt() },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            new[] { bloodOath });
        var state = sim.CreateInitialState(7UL);
        ReachNextTier(sim, state, PlayerSide.Left);
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("blood_oath");

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "blood_oath"),
        });

        AssertThat(state.LeftSpireHp).IsEqualApprox(
            BattleConfig.Default.SpireMaxHp * 0.8f, 0.01f);
        AssertThat(state.RightSpireHp).IsEqualApprox(BattleConfig.Default.SpireMaxHp, 0.01f);
    }

    [TestCase]
    public void OngoingDripPriceReducesIncomeButRespectsTheFloor()
    {
        var deepTithe = new PactDef(
            "deep_tithe", "Deep Tithe", PactTier.Wyrm, PactCategory.Economy,
            Lore: "test", WalletCapBonus: 500f, PriceDripPerSecond: 50f);
        var sim = new BattleSim(
            BattleConfig.Default,
            new[] { TestUnits.Grunt() },
            DraconicWars.Sim.Conduits.ConduitDefs.All,
            new[] { deepTithe });
        var state = sim.CreateInitialState(7UL);
        ReachNextTier(sim, state, PlayerSide.Left);
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("deep_tithe");
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "deep_tithe"),
        });
        var manaBefore = state.Left.Mana;

        AdvanceTicks(sim, state, 30);

        // Tier 2 is up: floor rate x the ascension drip escalation for one second.
        var escalation = System.MathF.Pow(state.Config.AscensionDripEscalation, 1);
        var expected = manaBefore + state.Config.DripFloorPerSecond * escalation;
        AssertThat(state.Left.Mana).IsEqualApprox(expected, 0.1f);
    }

    [TestCase]
    public void EmptyParleyTiersMeansTheBrokerNeverComes()
    {
        var config = BattleConfig.Default with { ParleyTiers = System.Array.Empty<int>() };
        var (sim, state) = CreateBattle(config);

        ReachNextTier(sim, state, PlayerSide.Left);

        AssertThat(state.Left.AscensionTier).IsEqual(2);
        AssertThat(state.Left.AwaitingParley).IsFalse();
        AssertThat(state.Left.PendingParleys).IsEqual(0);
    }

    [TestCase]
    public void OffersAreDistinctAndMatchThePathTier()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);

        var tier = state.ParleyTierPath[0];
        var offers = state.Left.PendingOffers;
        AssertThat(offers.Distinct().Count()).IsEqual(3);
        foreach (var offer in offers)
        {
            AssertThat(PactCatalog.ById(offer).Tier).IsEqual(tier);
        }
    }

    [TestCase]
    public void SameSeedProducesIdenticalOffers()
    {
        var (simA, stateA) = CreateBattle();
        var (simB, stateB) = CreateBattle();
        ReachNextTier(simA, stateA, PlayerSide.Left);
        ReachNextTier(simB, stateB, PlayerSide.Left);

        AssertThat(stateA.Left.PendingOffers.SequenceEqual(stateB.Left.PendingOffers)).IsTrue();
        AssertThat(stateA.ParleyTierPath.SequenceEqual(stateB.ParleyTierPath)).IsTrue();
    }

    [TestCase]
    public void PrismParleyGrantsTwoFreeReswearsOnSeal()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("prism_parley");

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "prism_parley"),
        });

        AssertThat(state.Left.FreeAttunements).IsEqual(2);
    }

    [TestCase]
    public void SalvageCharterRaisesConduitRefundToThreeQuarters()
    {
        var (sim, state) = CreateBattle();
        ReachNextTier(sim, state, PlayerSide.Left);
        state.Left.PendingOffers.Clear();
        state.Left.PendingOffers.Add("salvage_charter");
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SealPact(PlayerSide.Left, "salvage_charter"),
            SimCommand.BuildConduit(PlayerSide.Left, "war_horn"),
        });
        var spent = state.Left.ConduitSpent["war_horn"];
        var manaBefore = state.Left.Mana;

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SellConduit(PlayerSide.Left, "war_horn"),
        });

        var expectedRefund = spent * 0.75f;
        AssertThat(state.Left.Mana >= manaBefore + expectedRefund - 1f).IsTrue();
        AssertThat(state.Left.Mana <= manaBefore + expectedRefund + 2f).IsTrue();
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
}
