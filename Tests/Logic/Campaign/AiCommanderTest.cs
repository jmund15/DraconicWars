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
            DraconicWars.Sim.Augments.AugmentCatalog.All);
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
    public void AiResolvesDraftWindowsImmediately()
    {
        var config = BattleConfig.Default with { AugmentWindowTicks = new[] { 10 } };
        var sim = new BattleSim(
            config, UnitCatalog.FirstPlayable, ConduitDefs.All,
            DraconicWars.Sim.Augments.AugmentCatalog.All);
        var state = sim.CreateInitialState(11UL);
        var ai = new AiCommander(AiPersona.Streamer, PlayerSide.Right, seed: 3UL);

        for (var i = 0; i < 12; i++)
        {
            sim.Advance(state, ai.CommandsForTick(state).ToList());
        }

        AssertThat(state.Right.AwaitingDraft).IsFalse();
        AssertThat(state.Right.PickedAugments.Count).IsEqual(1);
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
