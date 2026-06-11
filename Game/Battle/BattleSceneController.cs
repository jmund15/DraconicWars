namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;

/// <summary>
/// Battle scene root: wires the BattleRunner's sim diffs to UnitView nodes, loads the
/// generated background, and provides debug deploy keys until the HUD Part lands.
/// The scene owns structure; this script owns behavior (scene_authoring rule).
/// </summary>
public partial class BattleSceneController : Node2D
{
    private const string SheetRoot = "res://art_pipeline/output/units";
    private const string BackgroundPath = "res://art_pipeline/output/backgrounds/battle_bg.png";

    [Export, RequiredExport] public BattleRunner Runner { get; set; } = null!;

    [Export, RequiredExport] public Node2D UnitLayer { get; set; } = null!;

    [Export, RequiredExport] public Sprite2D Background { get; set; } = null!;

    [Export, RequiredExport] public Label DebugLabel { get; set; } = null!;

    private readonly Dictionary<int, UnitView> _views = new();
    private UnitSpriteLibrary _sprites = null!;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        _sprites = new UnitSpriteLibrary(SheetRoot);

        if (FileAccess.FileExists(BackgroundPath))
        {
            var image = Image.LoadFromFile(BackgroundPath);
            Background.Texture = ImageTexture.CreateFromImage(image);
        }

        Runner.UnitSpawned += OnUnitSpawned;
        Runner.UnitDied += OnUnitDied;
        Runner.TickAdvanced += OnTickAdvanced;
        Runner.Initialize(
            BattleConfig.Default, UnitCatalog.FirstPlayable, ConduitDefs.All, seed: 1UL);
        JmoLogger.Info(this, "[Battle] Scene ready");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        SimCommand? command = key.Keycode switch
        {
            Key.Key1 => SimCommand.Deploy(PlayerSide.Left, "kobold_spearman"),
            Key.Key2 => SimCommand.Deploy(PlayerSide.Left, "forest_archer"),
            Key.Key3 => SimCommand.Deploy(PlayerSide.Left, "frost_whelp"),
            Key.Key8 => SimCommand.Deploy(PlayerSide.Right, "kobold_spearman"),
            Key.Key9 => SimCommand.Deploy(PlayerSide.Right, "forest_archer"),
            Key.Key0 => SimCommand.Deploy(PlayerSide.Right, "frost_whelp"),
            _ => null,
        };
        if (command is { } deploy)
        {
            Runner.EnqueueCommand(deploy);
        }
    }

    private void OnUnitSpawned(SimUnit unit)
    {
        var frames = _sprites.Load(unit.Def, unit.Def.Id);
        if (frames is null)
        {
            return;
        }
        var view = new UnitView();
        UnitLayer.AddChild(view);
        view.Bind(unit, frames);
        _views[unit.InstanceId] = view;
    }

    private void OnUnitDied(int instanceId, float laneX, Sim.Units.Stratum stratum, PlayerSide side)
    {
        if (!_views.Remove(instanceId, out var view))
        {
            return;
        }
        view.PlayDeathAndFree(laneX, stratum);
    }

    private void OnTickAdvanced()
    {
        var state = Runner.State;
        DebugLabel.Text =
            $"t {state.Tick / state.Config.TickRate}s  "
            + $"L mana {(int)state.Left.Mana} spire {(int)state.LeftSpireHp}  "
            + $"R mana {(int)state.Right.Mana} spire {(int)state.RightSpireHp}  "
            + $"asc {state.Left.AscensionTier}/{state.Right.AscensionTier}";
    }

    #region Test Helpers
#if TOOLS
    internal IReadOnlyDictionary<int, UnitView> _TestViews => _views;
#endif
    #endregion
}
