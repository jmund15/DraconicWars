namespace DraconicWars.Game.Balance;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Campaign;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using DraconicWars.Sim.Pacts;
using DraconicWars.Sim.Units;

/// <summary>
/// Headless AI-vs-AI batch harness (design directive: balance instrumentation). Pure
/// C# over the deterministic sim — same seed and personas always reproduce the same
/// telemetry. The harness measures so tuning can target FEEL; invariants built on it
/// are distributional bands, never per-battle guarantees: variance is a feature.
/// </summary>
public static class BatchRunner
{
    public const int SampleEveryTicks = 300;
    public const int DefaultMaxTicks = 24000;

    public static BattleTelemetry RunOne(
        AiPersona left,
        AiPersona right,
        ulong seed,
        BattleConfig? config = null,
        int maxTicks = DefaultMaxTicks)
    {
        var battleConfig = config ?? BattleConfig.Default;
        var sim = new BattleSim(
            battleConfig, UnitCatalog.FirstPlayable, ConduitDefs.All, PactCatalog.All);
        var state = sim.CreateInitialState(seed);
        state.Left.EquippedDragonId = UnitCatalog.RentalDragonId;
        state.Right.EquippedDragonId = UnitCatalog.RentalDragonId;
        var aiLeft = new AiCommander(left, PlayerSide.Left, seed ^ 0xA5A5UL);
        var aiRight = new AiCommander(right, PlayerSide.Right, seed ^ 0x5A5AUL);

        var leftTier4 = -1;
        var rightTier4 = -1;
        var leftSummoned = false;
        var rightSummoned = false;
        var leftDeploys = 0;
        var rightDeploys = 0;
        var deploysByUnit = new Dictionary<string, int>();
        var seenInstances = new HashSet<int>();
        var leftMana = new List<float>();
        var rightMana = new List<float>();
        var leftSpire = new List<float>();
        var rightSpire = new List<float>();
        var lastSampledTick = -1;

        // Parley freezes consume iterations without advancing the tick, so the loop
        // budget carries slack beyond maxTicks.
        var iterationBudget = maxTicks + 600;
        while (state.Outcome == BattleOutcome.Ongoing
            && state.Tick < maxTicks
            && iterationBudget-- > 0)
        {
            var commands = new List<SimCommand>();
            commands.AddRange(aiLeft.CommandsForTick(state));
            commands.AddRange(aiRight.CommandsForTick(state));
            sim.Advance(state, commands);

            foreach (var unit in state.Units)
            {
                if (!seenInstances.Add(unit.InstanceId))
                {
                    continue;
                }
                if (unit.Side == PlayerSide.Left)
                {
                    leftDeploys++;
                }
                else
                {
                    rightDeploys++;
                }
                deploysByUnit[unit.Def.Id] = deploysByUnit.GetValueOrDefault(unit.Def.Id) + 1;
                if (unit.Def.Tier >= 4)
                {
                    leftSummoned |= unit.Side == PlayerSide.Left;
                    rightSummoned |= unit.Side == PlayerSide.Right;
                }
            }

            if (leftTier4 < 0 && state.Left.AscensionTier >= 4)
            {
                leftTier4 = state.Tick;
            }
            if (rightTier4 < 0 && state.Right.AscensionTier >= 4)
            {
                rightTier4 = state.Tick;
            }
            if (state.Tick != lastSampledTick && state.Tick % SampleEveryTicks == 0)
            {
                lastSampledTick = state.Tick;
                leftMana.Add(state.Left.Mana);
                rightMana.Add(state.Right.Mana);
                leftSpire.Add(state.LeftSpireHp);
                rightSpire.Add(state.RightSpireHp);
            }
        }

        return new BattleTelemetry(
            Seed: seed,
            Outcome: state.Outcome,
            DurationTicks: state.Tick,
            LeftTier4Tick: leftTier4,
            RightTier4Tick: rightTier4,
            LeftSummoned: leftSummoned,
            RightSummoned: rightSummoned,
            LeftDeploys: leftDeploys,
            RightDeploys: rightDeploys,
            LeftFinalSpirePct: state.LeftSpireHp / battleConfig.SpireMaxHp,
            RightFinalSpirePct: state.RightSpireHp / battleConfig.SpireMaxHp,
            ComebackWin: ComputeComeback(state.Outcome, leftSpire, rightSpire),
            LeftManaCurve: leftMana,
            RightManaCurve: rightMana,
            DeploysByUnit: deploysByUnit);
    }

    public static List<BattleTelemetry> Run(
        AiPersona left,
        AiPersona right,
        IEnumerable<ulong> seeds,
        BattleConfig? config = null)
    {
        return seeds.Select(seed => RunOne(left, right, seed, config)).ToList();
    }

    private static bool ComputeComeback(
        BattleOutcome outcome, List<float> leftSpire, List<float> rightSpire)
    {
        if (leftSpire.Count < 2)
        {
            return false;
        }
        var half = leftSpire.Count / 2;
        return outcome switch
        {
            BattleOutcome.LeftVictory => leftSpire[half] < rightSpire[half],
            BattleOutcome.RightVictory => rightSpire[half] < leftSpire[half],
            _ => false,
        };
    }
}
