namespace DraconicWars.Game.Battle;

using DraconicWars.Sim.Battle;
using Godot;

/// <summary>
/// Renders one sim unit: AnimatedSprite2D bound to a SimUnit instance. Programmatic
/// construction is correct here — variable-count runtime composition (scene_authoring
/// rule). Position snaps to the sim each frame; animation follows the attack phase.
/// </summary>
public partial class UnitView : Node2D
{
    private static readonly StringName IdleAnim = new("idle");
    private static readonly StringName WalkAnim = new("walk");
    private static readonly StringName FlyAnim = new("fly");
    private static readonly StringName AttackAnim = new("attack");
    private static readonly StringName DeathAnim = new("death");

    private SimUnit _unit = null!;
    private AnimatedSprite2D _sprite = null!;
    private StringName _moveAnim = WalkAnim;
    private float _lastX;

    public int InstanceId { get; private set; }

    public void Bind(SimUnit unit, SpriteFrames frames)
    {
        _unit = unit;
        InstanceId = unit.InstanceId;
        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Centered = true,
            FlipH = unit.Side == PlayerSide.Right,
        };
        AddChild(_sprite);
        _moveAnim = frames.HasAnimation(FlyAnim) ? FlyAnim : WalkAnim;
        _lastX = unit.X;
        Position = LaneGeometry.ToWorld(unit.X, unit.Stratum);
        PlayIfAvailable(IdleAnim);
    }

    public override void _Process(double delta)
    {
        if (_unit is null || !_unit.IsAlive)
        {
            return;
        }

        Position = LaneGeometry.ToWorld(_unit.X, _unit.Stratum);

        if (_unit.AttackPhase != AttackPhase.None)
        {
            PlayIfAvailable(AttackAnim);
        }
        else if (Mathf.Abs(_unit.X - _lastX) > 0.0005f)
        {
            PlayIfAvailable(_moveAnim);
        }
        else
        {
            PlayIfAvailable(IdleAnim);
        }
        _lastX = _unit.X;
    }

    /// <summary>Detaches from sim state, plays death, frees itself.</summary>
    public void PlayDeathAndFree(float laneX, DraconicWars.Sim.Units.Stratum stratum)
    {
        _unit = null!;
        Position = LaneGeometry.ToWorld(laneX, stratum);
        if (_sprite is not null && _sprite.SpriteFrames.HasAnimation(DeathAnim))
        {
            _sprite.Play(DeathAnim);
            _sprite.AnimationFinished += QueueFree;
        }
        else
        {
            QueueFree();
        }
    }

    private void PlayIfAvailable(StringName animation)
    {
        if (_sprite.Animation == animation)
        {
            return;
        }
        if (_sprite.SpriteFrames.HasAnimation(animation))
        {
            _sprite.Play(animation);
        }
    }
}
