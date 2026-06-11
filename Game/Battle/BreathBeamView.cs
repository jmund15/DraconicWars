namespace DraconicWars.Game.Battle;

using Godot;

/// <summary>
/// The breath verb's visual: a tapered beam from the Dragonspire perch to the aim
/// point plus an impact glow. Programmatic construction is correct — transient VFX
/// (scene_authoring carve-out). Colors come from the fire ramp until breath variants
/// carry their own element ramp.
/// </summary>
public partial class BreathBeamView : Node2D
{
    private static readonly Color BeamCore = Color.FromHtml("f9c22b");
    private static readonly Color BeamEdge = Color.FromHtml("fb6b1d");
    private static readonly Color Impact = Color.FromHtml("e83b3b");

    private Line2D _outer = null!;
    private Line2D _inner = null!;
    private Polygon2D _impact = null!;
    private float _pulse;

    public override void _Ready()
    {
        _outer = new Line2D
        {
            Width = 6f,
            DefaultColor = new Color(BeamEdge, 0.55f),
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        _inner = new Line2D
        {
            Width = 2.5f,
            DefaultColor = BeamCore,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        _impact = new Polygon2D { Color = new Color(Impact, 0.6f) };
        AddChild(_outer);
        AddChild(_inner);
        AddChild(_impact);
        Visible = false;
    }

    public void UpdateBeam(Vector2 origin, Vector2 target, bool active, double delta)
    {
        Visible = active;
        if (!active)
        {
            return;
        }

        _pulse += (float)delta * 18f;
        var wobble = Mathf.Sin(_pulse) * 1.2f;
        var points = new[] { origin, (origin + target) * 0.5f + new Vector2(0, wobble), target };
        _outer.Points = points;
        _inner.Points = points;

        var radius = 5f + Mathf.Sin(_pulse * 1.7f) * 1.5f;
        var circle = new Vector2[10];
        for (var i = 0; i < circle.Length; i++)
        {
            var angle = Mathf.Tau * i / circle.Length;
            circle[i] = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        _impact.Polygon = circle;
    }
}
