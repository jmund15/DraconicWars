namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using DraconicWars.Game.Battle.Hud;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;

/// <summary>
/// Battle scene root: wires the BattleRunner's sim diffs to UnitView nodes and the
/// HUD's intents to SimCommands. Local player verbs: cards/keys deploy, conduit row
/// builds/sells, right-mouse-hold aims breath, Q casts Wrath, E channels summoning.
/// </summary>
public partial class BattleSceneController : Node2D
{
    private const string SheetRoot = "res://art_pipeline/output/units";
    private const string BackgroundPath = "res://art_pipeline/output/backgrounds/battle_bg.png";
    private const int ChannelPerTick = 15;

    [Export, RequiredExport] public BattleRunner Runner { get; set; } = null!;

    [Export, RequiredExport] public Node2D UnitLayer { get; set; } = null!;

    [Export, RequiredExport] public Sprite2D Background { get; set; } = null!;

    [Export, RequiredExport] public BattleHud Hud { get; set; } = null!;

    private readonly Dictionary<int, UnitView> _views = new();
    private UnitSpriteLibrary _sprites = null!;
    private PlayerSide _localSide = PlayerSide.Left;

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
        Runner.Initialize(
            BattleConfig.Default, UnitCatalog.FirstPlayable, ConduitDefs.All, seed: 1UL);

        Hud.Initialize(Runner, _localSide, UnitCatalog.FirstPlayable, ConduitDefs.All);
        Hud.DeployRequested += id => Runner.EnqueueCommand(SimCommand.Deploy(_localSide, id));
        Hud.ConduitBuildRequested += OnConduitBuildRequested;
        Hud.ConduitSellRequested += id => Runner.EnqueueCommand(SimCommand.SellConduit(_localSide, id));
        JmoLogger.Info(this, "[Battle] Scene ready");
    }

    public override void _Process(double delta)
    {
        if (Input.IsMouseButtonPressed(MouseButton.Right))
        {
            var laneX = GetGlobalMousePosition().X / LaneGeometry.PixelsPerMeter;
            Runner.EnqueueCommand(SimCommand.FireBreath(_localSide, laneX));
        }
        if (Input.IsKeyPressed(Key.E))
        {
            Runner.EnqueueCommand(SimCommand.ChannelMana(_localSide, ChannelPerTick));
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        if (key.Keycode == Key.Q)
        {
            Runner.EnqueueCommand(SimCommand.CastWrath(_localSide));
        }
    }

    private void OnConduitBuildRequested(string conduitId)
    {
        var player = Runner.State.Player(_localSide);
        var command = player.Conduits.ContainsKey(conduitId)
            ? SimCommand.UpgradeConduit(_localSide, conduitId)
            : SimCommand.BuildConduit(_localSide, conduitId);
        Runner.EnqueueCommand(command);
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

    #region Test Helpers
#if TOOLS
    internal IReadOnlyDictionary<int, UnitView> _TestViews => _views;
#endif
    #endregion
}
