namespace DraconicWars.Tests.Logic.Balance;

using System;
using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Balance;
using DraconicWars.Game.Campaign;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Distributional balance invariants over evenly-matched AI battles (directive items
/// 4+6). Bands are deliberately WIDE — they catch balance regressions (a change that
/// makes dragons unreachable or battles degenerate), they do NOT homogenize: variance
/// across seeds is a design feature.
/// </summary>
[TestSuite]
public class BalanceInvariantsTest
{
    private const int Battles = 24;

    private static List<BattleTelemetry> RunEvenBatch()
    {
        var matchups = new (AiPersona, AiPersona)[]
        {
            (AiPersona.Rusher, AiPersona.Rusher),
            (AiPersona.Powerhouse, AiPersona.Powerhouse),
            (AiPersona.Streamer, AiPersona.Streamer),
        };
        var telemetry = new List<BattleTelemetry>(Battles);
        for (var i = 0; i < Battles; i++)
        {
            var (left, right) = matchups[i % matchups.Length];
            telemetry.Add(BatchRunner.RunOne(left, right, seed: 1000UL + (ulong)i));
        }
        return telemetry;
    }

    [TestCase(Timeout = 120000)]
    public void EvenBattles_StayInsideTheFeelBands()
    {
        var batch = RunEvenBatch();

        var tier4Rate = batch.Count(t => t.LeftTier4Tick >= 0 || t.RightTier4Tick >= 0)
            / (float)Battles;
        var summonRate = batch.Count(t => t.LeftSummoned || t.RightSummoned) / (float)Battles;
        var durations = batch.Select(t => t.DurationTicks).OrderBy(d => d).ToList();
        var medianTicks = durations[durations.Count / 2];
        var decisive = batch.Count(t =>
            t.Outcome is BattleOutcome.LeftVictory or BattleOutcome.RightVictory);
        var comebacks = batch.Count(t => t.ComebackWin);
        var deploysPerBattle = batch.Average(t => t.LeftDeploys + t.RightDeploys);
        var usage = batch
            .SelectMany(t => t.DeploysByUnit)
            .GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

        var report =
            $"tier4Rate={tier4Rate:0.00} summonRate={summonRate:0.00}"
            + $" medianTicks={medianTicks} decisive={decisive}/{Battles}"
            + $" comebacks={comebacks} deploys/battle={deploysPerBattle:0.0}"
            + $" usage=[{string.Join(", ", usage.OrderByDescending(p => p.Value).Select(p => $"{p.Key}:{p.Value}"))}]";
        Console.WriteLine($"[BalanceProbe] {report}");

        // Dragon crescendo must stay central (pillar 1). In even max-length AI battles
        // tier 4 SHOULD nearly always arrive (playtest: humans never saw a dragon —
        // pacing pulled in); the floor catches regressions that strand the meter.
        AssertThat(tier4Rate >= 0.6f)
            .OverrideFailureMessage($"tier4 reach rate outside band: {report}").IsTrue();
        // The fix the band pins: someone actually SUMMONS in most even battles.
        AssertThat(summonRate >= 0.5f)
            .OverrideFailureMessage($"dragon summon rate below floor: {report}").IsTrue();
        // Battles end by spire kill or clock inside the designed arc.
        AssertThat(medianTicks >= 6000 && medianTicks <= 21600)
            .OverrideFailureMessage($"median duration outside band: {report}").IsTrue();
        // The lane fight must involve real deployment volume on both sides.
        AssertThat(deploysPerBattle >= 10)
            .OverrideFailureMessage($"deploy volume degenerate: {report}").IsTrue();
    }
}
