namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// A cosmetic attack form (Attack Archetype System §6): the baked element core a
/// Magic-archetype unit conjures, flown caster->target. Sim-pure — purely view
/// juice riding the sim's foreswing contact pulse (SimUnit.ContactTriggered); it
/// deals no damage (the hitscan sim already resolved the hit). Programmatic
/// construction is correct (variable-count runtime composition, scene_authoring rule).
/// </summary>
public partial class FormView : Node2D
{
    private const string FormsRoot = "res://art_pipeline/output/forms";
    private const float FlightSeconds = 0.34f;
    private static readonly StringName FlyAnim = new("fly");

    // Built SpriteFrames are immutable + shareable across spawns -> cache by name.
    private static readonly Dictionary<string, SpriteFrames?> Cache = new();

    private AnimatedSprite2D _sprite = null!;
    private Vector2 _target;
    private bool _flightStarted;

    /// <summary>Load (cached) the 3-frame core for an element x shape, as one
    /// non-looping "fly" animation (spawn -> travel -> impact). Returns null when no
    /// form sheet is baked for that pair (caller degrades gracefully -- no form).</summary>
    public static SpriteFrames? LoadFrames(string element, string shape)
    {
        var name = $"{element}_{shape}";
        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var sheetPath = $"{FormsRoot}/{name}_sheet.png";
        var manifestPath = $"{FormsRoot}/{name}.manifest.json";
        if (!FileAccess.FileExists(sheetPath) || !FileAccess.FileExists(manifestPath))
        {
            Cache[name] = null;
            return null;
        }

        var manifest = JsonSerializer.Deserialize<FormManifest>(
            FileAccess.GetFileAsString(manifestPath),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (manifest is null || manifest.Frames.Count == 0)
        {
            Cache[name] = null;
            return null;
        }

        var texture = UnitSpriteLibrary.LoadTexture(sheetPath);
        var frames = new SpriteFrames();
        frames.AddAnimation(FlyAnim);
        frames.SetAnimationLoop(FlyAnim, false);
        frames.SetAnimationSpeed(FlyAnim, manifest.Frames.Count / FlightSeconds);
        for (var i = 0; i < manifest.Frames.Count; i++)
        {
            frames.AddFrame(FlyAnim, new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(i * manifest.FrameW, 0, manifest.FrameW, manifest.FrameH),
            });
        }
        frames.RemoveAnimation("default");

        Cache[name] = frames;
        return frames;
    }

    /// <summary>Place the form at <paramref name="from"/>, build + play its core sprite, and
    /// (if already in the tree) start the flight tween. Order-independent: the caller may
    /// Launch before OR after AddChild — the tween starts here when in-tree, else _Ready
    /// starts it on tree-entry. (A prior version relied on _Ready firing AFTER Launch; the
    /// caller did AddChild first, so _Ready bailed on a null sprite and the form never flew.)</summary>
    public void Launch(Vector2 from, Vector2 to, SpriteFrames frames)
    {
        Position = from;
        _target = to;
        _sprite = new AnimatedSprite2D { SpriteFrames = frames, Centered = true };
        AddChild(_sprite);
        _sprite.Play(FlyAnim);
        _sprite.AnimationFinished += QueueFree;   // free after spawn->travel->impact plays once
        if (IsInsideTree())
        {
            StartFlight();
        }
    }

    public override void _Ready()
    {
        if (_sprite is not null)
        {
            StartFlight();   // Launch ran before AddChild — start the tween now that we're in-tree
        }
    }

    private void StartFlight()
    {
        if (_flightStarted)
        {
            return;
        }
        _flightStarted = true;
        CreateTween().TweenProperty(this, "position", _target, FlightSeconds);
    }

    private sealed class FormManifest
    {
        public int FrameW { get; set; }

        public int FrameH { get; set; }

        public List<string> Frames { get; set; } = new();

        public int LoopFrame { get; set; }
    }
}
