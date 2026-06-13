namespace DraconicWars.Tests.Integration.Battle;

using System.Threading.Tasks;
using DraconicWars.Game.Battle;
using DraconicWars.Sim.Battle;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public class SignatureVfxTest
{
    [TestCase]
    public async Task DeployedUnitGetsEmissiveMaterialAndAuraWithElementTintIntact()
    {
        var runner = ISceneRunner.Load("res://scenes/battle/battle_scene.tscn");
        var controller = (BattleSceneController)runner.Scene();

        controller.Runner.EnqueueCommand(SimCommand.Deploy(PlayerSide.Left, "kobold_spearman"));
        await runner.SimulateFrames(30, 16);

        var view = System.Linq.Enumerable.First(controller._TestViews.Values);
        var sprite = FindChild<AnimatedSprite2D>(view);

        AssertThat(sprite).IsNotNull();
        // Emissive rides the material; the element tint stays on modulate (compose, don't stomp).
        AssertThat(sprite!.Material is ShaderMaterial).IsTrue();
        AssertThat(sprite.Modulate != Colors.White).IsTrue();
        // Element particle aura attached.
        AssertThat(FindChild<CpuParticles2D>(view)).IsNotNull();
    }

    private static T? FindChild<T>(Node node)
        where T : class
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match)
            {
                return match;
            }
        }

        return null;
    }
}
