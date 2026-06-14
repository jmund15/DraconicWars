namespace DraconicWars.Tests.Integration.Battle;

using DraconicWars.Game.Battle;
using DraconicWars.Sim.Battle;
using DraconicWars.Tests.Logic.Sim;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Feet-anchor contract: a UnitView's sprite stands ON the lane line regardless of
/// frame height, so varied-size units share one ground baseline (center-anchoring
/// sank taller units half-a-height deeper). Geometry is deterministic — automated
/// per Hybrid TDD rather than left to manual inspection.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UnitViewAnchorTest
{
    private static SpriteFrames MakeFrames(int w, int h)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        var frames = new SpriteFrames();
        frames.AddAnimation("idle");
        frames.AddFrame("idle", ImageTexture.CreateFromImage(img));
        return frames;
    }

    private static UnitView BindView(int w, int h, int instanceId)
    {
        var unit = new SimUnit
        {
            InstanceId = instanceId,
            Def = TestUnits.Grunt(id: $"anchor_{instanceId}"),
            Side = PlayerSide.Left,
            Hp = 100,
            X = 0f,
        };
        var view = AutoFree(new UnitView())!;
        view.Bind(unit, MakeFrames(w, h));
        return view;
    }

    // Bottom edge of the drawn frame in the view's local space; 0 == standing on the lane line.
    private static float FeetOffsetFromLaneLine(UnitView view, float frameH)
    {
        var s = view._TestSprite;
        return s.Offset.Y + (s.Centered ? frameH / 2f : frameH);
    }

    [TestCase]
    public void VariedHeightSpritesShareTheLaneBaseline()
    {
        var shortFeet = FeetOffsetFromLaneLine(BindView(24, 24, 1), 24f);
        var tallFeet = FeetOffsetFromLaneLine(BindView(48, 64, 2), 64f);

        AssertThat(Mathf.Abs(shortFeet) < 0.01f)
            .OverrideFailureMessage($"short (24px) feet off the lane line by {shortFeet}px (expected 0)")
            .IsTrue();
        AssertThat(Mathf.Abs(tallFeet) < 0.01f)
            .OverrideFailureMessage($"tall (64px) feet off the lane line by {tallFeet}px (expected 0)")
            .IsTrue();
    }
}
