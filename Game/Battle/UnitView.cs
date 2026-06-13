namespace DraconicWars.Game.Battle;

using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using DraconicWars.Game.Battle.Vfx;
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
        // Element identity is always-on (playtest: tints only on re-sworn units made
        // both elements and Rebreathing illegible). Re-sworn companies read louder.
        var attuned = unit.Def.NativeElement is { } native && native != unit.Def.Element;
        _sprite.Modulate = Colors.White.Lerp(
            ElementColors.Of(unit.Def.Element), attuned ? 0.55f : 0.35f);
        AddChild(_sprite);
        ApplySignatureVfx(unit.Def.Element, unit.Def.Tier);
        _moveAnim = frames.HasAnimation(FlyAnim) ? FlyAnim : WalkAnim;
        _lastX = unit.X;
        Position = LaneGeometry.ToWorld(unit.X, unit.Stratum);
        PlayIfAvailable(IdleAnim);
    }

    /// <summary>Hover popup body: name, element provenance, live HP.</summary>
    public string HoverText()
    {
        if (_unit is null || !_unit.IsAlive)
        {
            return string.Empty;
        }
        var def = _unit.Def;
        var elementLine = def.NativeElement is { } native && native != def.Element
            ? $"{def.Element} (re-sworn from {native})"
            : def.Element.ToString();
        return $"{def.DisplayName}\nT{def.Tier} {elementLine}\nHP {_unit.Hp}/{def.MaxHp}";
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

    private static Shader? _emissiveShader;
    private static Texture2D? _dotTexture;

    /// <summary>Attaches the element + tier-scaled signature look: an emissive-bloom shader
    /// on the sprite (separate lane from the modulate tint), an element particle aura, and —
    /// for the highest tiers — an additive light halo. View-only; reads sim def, never mutates.</summary>
    private void ApplySignatureVfx(Element element, int tier)
    {
        var profile = SignatureVfxProfiles.For(element);
        if (profile is null)
        {
            return;
        }
        var resolved = SignatureVfxResolver.Resolve(tier, profile.ToResolverConfig());

        if (profile.EmissiveColors.Length > 0)
        {
            _emissiveShader ??= GD.Load<Shader>("res://assets/shaders/emissive_bloom.gdshader");
            var mat = new ShaderMaterial { Shader = _emissiveShader };
            mat.SetShaderParameter("emissive_a", profile.EmissiveColors[0]);
            mat.SetShaderParameter("emissive_b", profile.EmissiveColors[profile.EmissiveColors.Length > 1 ? 1 : 0]);
            mat.SetShaderParameter("boost", resolved.EmissiveBoost);
            _sprite.Material = mat;
        }

        var aura = new CpuParticles2D
        {
            Texture = DotTexture(),
            Amount = Mathf.Clamp((int)resolved.AuraDensity, 1, 32),
            Lifetime = 1.3f,
            Direction = Vector2.Up,
            Spread = 35f,
            Gravity = new Vector2(0f, -8f),
            InitialVelocityMin = 4f,
            InitialVelocityMax = 10f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.2f,
            Modulate = profile.LightColor,
            ZIndex = -1,
            Emitting = true,
        };
        AddChild(aura);

        if (resolved.SpawnLight)
        {
            var light = new PointLight2D
            {
                Texture = DotTexture(),
                Color = profile.LightColor,
                Energy = resolved.LightEnergy,
                TextureScale = 6f,
                BlendMode = Light2D.BlendModeEnum.Add,
                ZIndex = -1,
            };
            AddChild(light);
        }
    }

    private static Texture2D DotTexture()
    {
        if (_dotTexture is not null)
        {
            return _dotTexture;
        }
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, 1f },
            Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) },
        };
        _dotTexture = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 8,
            Height = 8,
        };
        return _dotTexture;
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
