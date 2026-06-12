namespace DraconicWars.Tests.Logic.Campaign;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Campaign;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class AiCommanderTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(
            BattleConfig.Default,
            UnitCatalog.FirstPlayable,
            ConduitDefs.All,
            DraconicWars.Sim.Pacts.PactCatalog.All);
        var state = sim.CreateInitialState(11UL);
        return (sim, state);
    }

    [TestCase]
    public void RusherDeploysTheCheapestAffordableUnit()
    {
        var (sim, state) = CreateBattle();
        var ai = new AiCommander(AiPersona.Rusher, PlayerSide.Right, seed: 3UL);
        state.Right.Mana = 45f;

        var commands = ai.CommandsForTick(state).ToList();

        var deploy = commands.FirstOrDefault(c => c.Kind == SimCommandKind.Deploy);
        AssertThat(deploy.TargetId).IsEqual("kobold_spearman");
    }

    [TestCase]
    public void PowerhouseBuildsEconomyBeforeUnits()
    {
        var (sim, state) = CreateBattle();
        var ai = new AiCommander(AiPersona.Powerhouse, PlayerSide.Right, seed: 3UL);
        state.Right.Mana = 400f;

        var commands = ai.CommandsForTick(state).ToList();

        AssertThat(commands.Any(c =>
            c.Kind == SimCommandKind.BuildConduit && c.TargetId == "mana_well")).IsTrue();
        AssertThat(commands.Any(c => c.Kind == SimCommandKind.Deploy)).IsFalse();
    }

    [TestCase]
    public void AiSealsAParleyWithinAFewTicksOfTieringUp()
    {
        var (sim, state) = CreateBattle();
        var ai = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);
        state.Right.AscensionMeter = state.Config.AscensionThresholds[0] - 0.001f;

        for (var i = 0; i < 6; i++)
        {
            sim.Advance(state, ai.CommandsForTick(state).ToList());
        }

        AssertThat(state.Right.AwaitingParley).IsFalse();
        AssertThat(state.Right.SealedPacts.Count).IsEqual(1);
    }

    [TestCase]
    public void AiChannelsSummoningAtDragonTier()
    {
        var (sim, state) = CreateBattle();
        state.Right.EquippedDragonId = "elder_drake";
        state.Right.AscensionTier = 4;
        state.Right.Mana = 500f;
        var ai = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);

        var commands = ai.CommandsForTick(state).ToList();

        AssertThat(commands.Any(c => c.Kind == SimCommandKind.ChannelMana)).IsTrue();
    }

    [TestCase]
    public void AiCastsWrathWhenPushIsOnItsHalf()
    {
        var (sim, state) = CreateBattle();
        var ai = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);
        state.Right.WrathCooldownTicks = 0;
        state.Left.Mana = 50000f;
        state.Left.WalletCap = 50000f;

        // Distinct def ids: same-id deploys are rejected by the per-unit cooldown.
        foreach (var defId in new[] { "kobold_spearman", "forest_archer", "stone_warden" })
        {
            sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, defId) });
            state.Units[^1].X = BattleConfig.Default.LaneLength * 0.8f;
        }

        var commands = ai.CommandsForTick(state).ToList();

        AssertThat(commands.Any(c => c.Kind == SimCommandKind.CastWrath)).IsTrue();
    }

    [TestCase]
    public void AiBreathAimsNearTheDensestEnemyCluster()
    {
        var (sim, state) = CreateBattle();
        var ai = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);
        state.Right.BreathEnergySeconds = 4f;
        state.Left.Mana = 50000f;
        state.Left.WalletCap = 50000f;
        var clusterIds = new[] { "kobold_spearman", "forest_archer", "stone_warden" };
        for (var i = 0; i < clusterIds.Length; i++)
        {
            sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, clusterIds[i]) });
            state.Units[^1].X = 20f + i * 0.5f;
        }

        var commands = ai.CommandsForTick(state).ToList();

        var breath = commands.Where(c => c.Kind == SimCommandKind.FireBreath).ToList();
        AssertThat(breath.Count).IsEqual(1);
        // Published accuracy handicap: aim lands within +-3 lane meters of the cluster.
        AssertThat(System.MathF.Abs(breath[0].X - 20.5f) <= 3f).IsTrue();
    }

    [TestCase]
    public void AiPaysOneTitheWhenNoOfferMatchesItsDoctrine()
    {
        var (sim, state) = CreateBattle();
        var ai = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);
        state.Right.AwaitingParley = true;
        state.Right.Mana = 500f;
        state.Right.PendingOffers.Clear();
        // Streamer doctrine prefers Combat; offer it a coin-counter's table.
        state.Right.PendingOffers.AddRange(new[] { "ley_tap", "iron_rations", "venom_coffers" });

        // The AI answers the Broker AND keeps fighting — assert the parley command
        // is present, not that it is alone.
        var first = ai.CommandsForTick(state).ToList();
        AssertThat(first.Count(c => c.Kind == SimCommandKind.PayTithe)).IsEqual(1);
        AssertThat(first.Any(c => c.Kind == SimCommandKind.SealPact)).IsFalse();

        state.Right.TithesPaidThisParley = 1;
        var second = ai.CommandsForTick(state).ToList();
        AssertThat(second.Count(c => c.Kind == SimCommandKind.SealPact)).IsEqual(1);
    }

    [TestCase]
    public void AiDeploysOnlyFromItsInjectedPool()
    {
        var enemyKobold = CampaignLevelDef.Magnify(
            UnitCatalog.FirstPlayable[0], 100, CampaignLevelDef.EnemyIdPrefix);
        var defs = UnitCatalog.FirstPlayable.Append(enemyKobold).ToList();
        var sim = new BattleSim(BattleConfig.Default, defs, ConduitDefs.All);
        var state = sim.CreateInitialState(11UL);
        state.Right.Mana = 400f;
        var ai = new AiCommander(
            AiPersona.Rusher, PlayerSide.Right, seed: 3UL, deployPool: new[] { enemyKobold });

        var commands = ai.CommandsForTick(state).ToList();

        var deploy = commands.First(c => c.Kind == SimCommandKind.Deploy);
        AssertThat(deploy.TargetId).IsEqual(CampaignLevelDef.EnemyIdPrefix + "kobold_spearman");
    }

    [TestCase]
    public void PersonaDoctrinesIncludeAnArmament()
    {
        var (sim, state) = CreateBattle();
        state.Right.Mana = 5000f;
        state.Right.WalletCap = 50000f;

        var streamer = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);
        state.Right.Conduits["mana_well"] = 1;
        state.Right.Conduits["war_horn"] = 1;
        var build = streamer.CommandsForTick(state).ToList()
            .First(c => c.Kind == SimCommandKind.BuildConduit);
        AssertThat(build.TargetId).IsEqual("skyward_flak");

        state.Right.Conduits.Clear();
        state.Right.Conduits["mana_well"] = 1;
        state.Right.Conduits["aurum_vault"] = 1;
        var powerhouse = new AiCommander(AiPersona.Powerhouse, PlayerSide.Right, seed: 3UL);
        var build2 = powerhouse.CommandsForTick(state).ToList()
            .First(c => c.Kind == SimCommandKind.BuildConduit);
        AssertThat(build2.TargetId).IsEqual("siege_mortar");
    }

    [TestCase]
    public void SameSeedSameState_ProducesIdenticalCommands()
    {
        var (simA, stateA) = CreateBattle();
        var (simB, stateB) = CreateBattle();
        var aiA = new AiCommander(AiPersona.Rusher, PlayerSide.Right, seed: 9UL);
        var aiB = new AiCommander(AiPersona.Rusher, PlayerSide.Right, seed: 9UL);
        stateA.Right.Mana = 300f;
        stateB.Right.Mana = 300f;

        for (var i = 0; i < 60; i++)
        {
            var commandsA = aiA.CommandsForTick(stateA).ToList();
            var commandsB = aiB.CommandsForTick(stateB).ToList();
            AssertThat(commandsA.SequenceEqual(commandsB)).IsTrue();
            simA.Advance(stateA, commandsA);
            simB.Advance(stateB, commandsB);
        }
    }
}
