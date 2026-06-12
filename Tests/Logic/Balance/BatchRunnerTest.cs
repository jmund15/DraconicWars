namespace DraconicWars.Tests.Logic.Balance;

using System.Linq;
using DraconicWars.Game.Balance;
using DraconicWars.Game.Campaign;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BatchRunnerTest
{
    [TestCase]
    public void SameSeedYieldsIdenticalTelemetry()
    {
        var a = BatchRunner.RunOne(AiPersona.Rusher, AiPersona.Powerhouse, seed: 11UL);
        var b = BatchRunner.RunOne(AiPersona.Rusher, AiPersona.Powerhouse, seed: 11UL);

        AssertThat(a.Outcome).IsEqual(b.Outcome);
        AssertThat(a.DurationTicks).IsEqual(b.DurationTicks);
        AssertThat(a.LeftTier4Tick).IsEqual(b.LeftTier4Tick);
        AssertThat(a.RightTier4Tick).IsEqual(b.RightTier4Tick);
        AssertThat(a.LeftDeploys).IsEqual(b.LeftDeploys);
        AssertThat(a.RightDeploys).IsEqual(b.RightDeploys);
        AssertThat(a.LeftManaCurve.SequenceEqual(b.LeftManaCurve)).IsTrue();
    }

    [TestCase]
    public void BattleResolvesAndTelemetryIsPopulated()
    {
        var telemetry = BatchRunner.RunOne(AiPersona.Streamer, AiPersona.Rusher, seed: 3UL);

        AssertThat(telemetry.Outcome != BattleOutcome.Ongoing).IsTrue();
        AssertThat(telemetry.DurationTicks > 0).IsTrue();
        AssertThat(telemetry.LeftManaCurve.Count > 0).IsTrue();
        AssertThat(telemetry.LeftDeploys + telemetry.RightDeploys > 0).IsTrue();
        AssertThat(telemetry.DeploysByUnit.Values.Sum())
            .IsEqual(telemetry.LeftDeploys + telemetry.RightDeploys);
    }

    [TestCase]
    public void TierFourTickIsMinusOneWhenNeverReached()
    {
        // A 60-second hard cap cannot reach Dragon Tier (first threshold alone needs
        // ~100 meter at ~1 meter/s trickle rates).
        var shortConfig = BattleConfig.Default with
        {
            CrescendoStartTick = 1500,
            SuddenDeathStartTick = 1650,
            HardEndTick = 1800,
            ParleyTicks = System.Array.Empty<int>(),
        };
        var telemetry = BatchRunner.RunOne(
            AiPersona.Rusher, AiPersona.Rusher, seed: 5UL, config: shortConfig);

        AssertThat(telemetry.LeftTier4Tick).IsEqual(-1);
        AssertThat(telemetry.RightTier4Tick).IsEqual(-1);
    }
}
