namespace DraconicWars.Game.Balance;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;

/// <summary>
/// Per-battle measurements emitted by <see cref="BatchRunner"/>. Tier ticks are -1
/// when the side never reached Dragon Tier; mana curves sample every
/// <see cref="BatchRunner.SampleEveryTicks"/> ticks; ComebackWin means the winner
/// trailed on spire HP at the battle's halfway tick.
/// </summary>
public sealed record BattleTelemetry(
    ulong Seed,
    BattleOutcome Outcome,
    int DurationTicks,
    int LeftTier4Tick,
    int RightTier4Tick,
    bool LeftSummoned,
    bool RightSummoned,
    int LeftDeploys,
    int RightDeploys,
    float LeftFinalSpirePct,
    float RightFinalSpirePct,
    bool ComebackWin,
    IReadOnlyList<float> LeftManaCurve,
    IReadOnlyList<float> RightManaCurve,
    IReadOnlyDictionary<string, int> DeploysByUnit);
