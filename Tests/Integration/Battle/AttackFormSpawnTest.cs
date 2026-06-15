namespace DraconicWars.Tests.Integration.Battle;

using System.Threading.Tasks;
using DraconicWars.Game.Battle;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using DraconicWars.Tests.Logic.Sim;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Attack-Form View Spawn (POB): a Magic-archetype unit conjures a cosmetic element
/// form at its sim contact moment; a Physical unit conjures nothing; and the spawn
/// fires once per contact (rising-edge), not once per render frame. The flight tween
/// is visual (playtest); this pins the spawn decision + form load.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AttackFormSpawnTest
{
    private static SpriteFrames IdleFrames()
    {
        var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        var frames = new SpriteFrames();
        frames.AddAnimation("idle");
        frames.AddFrame("idle", ImageTexture.CreateFromImage(img));
        return frames;
    }

    private static (Node root, UnitView view, SimUnit unit) Setup(AttackArchetype attack, Element element)
    {
        var root = AutoFree(new Node())!;
        var unit = new SimUnit
        {
            InstanceId = 1,
            Def = TestUnits.Grunt(id: "form_test") with { Attack = attack, Element = element },
            Side = PlayerSide.Left,
            Hp = 100,
            X = 5f,
        };
        var view = new UnitView();
        root.AddChild(view);
        view.Bind(unit, IdleFrames());
        return (root, view, unit);
    }

    private static int CountForms(Node root)
    {
        var n = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is FormView)
            {
                n++;
            }
        }
        return n;
    }

    [TestCase]
    public void MagicUnit_SpawnsFormOnContact()
    {
        var (root, view, unit) = Setup(
            new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Ball), Element.Fire);
        unit.ContactTriggered = true;
        view._Process(0.016);
        AssertThat(CountForms(root))
            .OverrideFailureMessage("a magic unit at contact should spawn exactly one form")
            .IsEqual(1);
    }

    [TestCase]
    public void PhysicalUnit_SpawnsNoForm()
    {
        var (root, view, unit) = Setup(AttackArchetype.MeleePhysical, Element.Fire);
        unit.ContactTriggered = true;
        view._Process(0.016);
        AssertThat(CountForms(root))
            .OverrideFailureMessage("a physical unit must not spawn a form")
            .IsEqual(0);
    }

    [TestCase]
    public void Form_SpawnsOncePerContact_RisingEdge()
    {
        var (root, view, unit) = Setup(
            new AttackArchetype(AttackClass.Magic, AttackPose.Cast, AttackForm.Ball), Element.Fire);
        unit.ContactTriggered = true;
        view._Process(0.016);
        view._Process(0.016);   // flag still true (sim hasn't ticked) — must NOT spawn again
        AssertThat(CountForms(root))
            .OverrideFailureMessage("ContactTriggered held across frames must spawn only one form")
            .IsEqual(1);
    }

    private static SpriteFrames FlyFrames()
    {
        var frames = new SpriteFrames();
        frames.AddAnimation("fly");
        frames.SetAnimationLoop("fly", false);
        frames.SetAnimationSpeed("fly", 9.0);
        for (var i = 0; i < 3; i++)
        {
            var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            img.Fill(Colors.White);
            frames.AddFrame("fly", ImageTexture.CreateFromImage(img));
        }
        frames.RemoveAnimation("default");
        return frames;
    }

    // Regression: forms once spawned BELOW the caster and never moved — _Ready fired on
    // AddChild before Launch set the sprite, so the flight tween was never wired. This
    // exercises a LIVE tree (the spawn-count tests above run on a detached root, where
    // _Ready never fires — which is exactly why they missed the bug).
    [TestCase]
    public async Task Form_FliesTowardTarget_InLiveTree()
    {
        var runner = ISceneRunner.Load("res://scenes/battle/battle_scene.tscn");
        var root = runner.Scene();
        var form = AutoFree(new FormView())!;
        root.AddChild(form);
        form.Launch(new Vector2(0f, 100f), new Vector2(240f, 100f), FlyFrames());
        var startX = form.Position.X;

        await runner.SimulateFrames(6, 16);

        AssertThat(form.Position.X > startX + 1f)
            .OverrideFailureMessage($"form did not fly: x stayed at {form.Position.X} (start {startX})")
            .IsTrue();
    }
}
