namespace DraconicWars.Tests.Integration.Battle;

using System.Threading.Tasks;
using DraconicWars.Game.Battle;
using DraconicWars.Sim.Battle;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public class BattleSceneTest
{
    [TestCase]
    public async Task DeployedUnitGetsAViewThatAdvances()
    {
        var runner = ISceneRunner.Load("res://scenes/battle/battle_scene.tscn");
        var controller = (BattleSceneController)runner.Scene();

        controller.Runner.EnqueueCommand(SimCommand.Deploy(PlayerSide.Left, "kobold_spearman"));
        await runner.SimulateFrames(30, 16);

        AssertThat(controller._TestViews.Count).IsEqual(1);
        var view = System.Linq.Enumerable.First(controller._TestViews.Values);
        var xAfterHalfSecond = view.Position.X;

        await runner.SimulateFrames(60, 16);
        AssertThat(view.Position.X > xAfterHalfSecond).IsTrue();
    }

    [TestCase]
    public async Task HudBuildsCardsAndTracksMana()
    {
        var runner = ISceneRunner.Load("res://scenes/battle/battle_scene.tscn");
        var controller = (BattleSceneController)runner.Scene();

        await runner.SimulateFrames(10, 16);
        var deployableCount = System.Linq.Enumerable.Count(
            DraconicWars.Game.Content.UnitCatalog.FirstPlayable, d => d.Tier < 4);
        AssertThat(controller.Hud._TestCards.Count).IsEqual(deployableCount);
        var manaBefore = controller.Hud.ManaLabel.Text;

        await runner.SimulateFrames(90, 16);
        AssertThat(controller.Hud.ManaLabel.Text != manaBefore).IsTrue();
    }

    [TestCase]
    public async Task SimAdvancesAtThirtyTicksPerSecond()
    {
        var runner = ISceneRunner.Load("res://scenes/battle/battle_scene.tscn");
        var controller = (BattleSceneController)runner.Scene();
        var tickBefore = controller.Runner.State.Tick;

        await runner.SimulateFrames(60, 16);

        var elapsed = controller.Runner.State.Tick - tickBefore;
        AssertThat(elapsed >= 25 && elapsed <= 35).IsTrue();
    }
}
