namespace DraconicWars.Tests.Logic.Sim;

using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BattleTimelineTest
{
    private static BattleConfig ShortTimeline => BattleConfig.Default with
    {
        CrescendoStartTick = 100,
        SuddenDeathStartTick = 200,
        HardEndTick = 300,
        SuddenDeathEscalationTicks = 30,
    };

    private static (BattleSim sim, BattleState state) CreateBattle(BattleConfig config)
    {
        var sim = new BattleSim(config, new[] { TestUnits.Grunt() });
        var state = sim.CreateInitialState(1UL);
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
    public void CrescendoDoublesManaDrip()
    {
        var config = ShortTimeline with { StartingWalletCap = 100000f, StartingMana = 0f };
        var (sim, state) = CreateBattle(config);

        AdvanceTicks(sim, state, 100);
        var manaAtCrescendo = state.Left.Mana;
        AssertThat(manaAtCrescendo).IsEqualApprox(100 * config.DripPerTick, 0.05f);

        AdvanceTicks(sim, state, 50);
        var gained = state.Left.Mana - manaAtCrescendo;
        AssertThat(gained).IsEqualApprox(50 * config.DripPerTick * 2f, 0.05f);
    }

    [TestCase]
    public void SuddenDeathDecaysBothSpires()
    {
        var (sim, state) = CreateBattle(ShortTimeline);

        AdvanceTicks(sim, state, 200);
        var leftBefore = state.LeftSpireHp;
        var rightBefore = state.RightSpireHp;
        AssertThat(leftBefore).IsEqualApprox(ShortTimeline.SpireMaxHp, 0.01f);

        AdvanceTicks(sim, state, 60);
        AssertThat(state.LeftSpireHp < leftBefore).IsTrue();
        AssertThat(state.RightSpireHp < rightBefore).IsTrue();
        AssertThat(state.LeftSpireHp).IsEqualApprox(state.RightSpireHp, 0.01f);
    }

    [TestCase]
    public void HardEndAwardsVictoryToHigherSpire()
    {
        var (sim, state) = CreateBattle(ShortTimeline);
        state.LeftSpireHp = 3000f;
        state.RightSpireHp = 2000f;

        AdvanceTicks(sim, state, 301);

        AssertThat(state.Outcome).IsEqual(BattleOutcome.LeftVictory);
    }

    [TestCase]
    public void HardEndWithEqualSpiresIsADraw()
    {
        var (sim, state) = CreateBattle(ShortTimeline);

        AdvanceTicks(sim, state, 301);

        AssertThat(state.Outcome).IsEqual(BattleOutcome.Draw);
    }

    [TestCase]
    public void DefaultTimelineMatchesDesignTimestamps()
    {
        var config = BattleConfig.Default;
        AssertThat(config.CrescendoStartTick).IsEqual(8 * 60 * 30);
        AssertThat(config.SuddenDeathStartTick).IsEqual(10 * 60 * 30);
        AssertThat(config.HardEndTick).IsEqual(12 * 60 * 30);
    }
}
