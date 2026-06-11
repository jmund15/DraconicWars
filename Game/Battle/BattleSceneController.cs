namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using DraconicWars.Game.Battle.Hud;
using DraconicWars.Game.Campaign;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;

/// <summary>
/// Battle scene root: builds the battle from the selected campaign level, wires sim
/// diffs to UnitViews and HUD intents to SimCommands, applies rewards on outcome.
/// Verbs: cards deploy, conduit row builds/sells, RMB-hold aims breath, Q casts
/// Wrath, E channels the summoning, R retreats (after the first minute).
/// </summary>
public partial class BattleSceneController : Node2D
{
    private const string SheetRoot = "res://art_pipeline/output/units";
    private const string BackgroundPath = "res://art_pipeline/output/backgrounds/battle_bg.png";
    private const int ChannelPerTick = 15;
    private const int RetreatUnlockTicks = 60 * 30;

    [Export, RequiredExport] public BattleRunner Runner { get; set; } = null!;

    [Export, RequiredExport] public Node2D UnitLayer { get; set; } = null!;

    [Export, RequiredExport] public Sprite2D Background { get; set; } = null!;

    [Export, RequiredExport] public BattleHud Hud { get; set; } = null!;

    [Export, RequiredExport] public PanelContainer OutcomePanel { get; set; } = null!;

    [Export, RequiredExport] public Label OutcomeLabel { get; set; } = null!;

    [Export, RequiredExport] public Button ContinueButton { get; set; } = null!;

    private readonly Dictionary<int, UnitView> _views = new();
    private UnitSpriteLibrary _sprites = null!;
    private CampaignLevelDef _level = null!;
    private PlayerSide _localSide = PlayerSide.Left;
    private bool _resultApplied;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        GameSession.EnsureProfileLoaded();
        _sprites = new UnitSpriteLibrary(SheetRoot);

        var levelIndex = Mathf.Clamp(
            GameSession.SelectedLevelIndex, 0, CampaignCatalog.Levels.Count - 1);
        _level = CampaignCatalog.Levels[levelIndex];

        if (FileAccess.FileExists(BackgroundPath))
        {
            Background.Texture = UnitSpriteLibrary.LoadTexture(BackgroundPath);
        }

        Runner.UnitSpawned += OnUnitSpawned;
        Runner.UnitDied += OnUnitDied;
        Runner.TickAdvanced += OnTickAdvanced;
        Runner.Initialize(
            _level.Config,
            CampaignCatalog.BuildBattleDefs(_level),
            ConduitDefs.All,
            seed: LevelSeed(_level.Id));
        Runner.Director = new WaveDirector(_level.Waves, _level.RepeatingWaves);
        Runner.State.Left.EquippedDragonId = UnitCatalog.RentalDragonId;
        Runner.State.Right.EquippedDragonId = UnitCatalog.RentalDragonId;

        Hud.Initialize(Runner, _localSide, UnitCatalog.FirstPlayable, ConduitDefs.All);
        Hud.DeployRequested += id => Runner.EnqueueCommand(SimCommand.Deploy(_localSide, id));
        Hud.ConduitBuildRequested += OnConduitBuildRequested;
        Hud.ConduitSellRequested += id => Runner.EnqueueCommand(SimCommand.SellConduit(_localSide, id));

        ContinueButton.Pressed += () =>
            GetTree().ChangeSceneToFile("res://scenes/menu/level_select.tscn");
        OutcomePanel.Visible = false;
        JmoLogger.Info(this, $"[Battle] Level '{_level.Id}' ready");
    }

    public override void _Process(double delta)
    {
        if (Runner.State.Outcome != BattleOutcome.Ongoing)
        {
            return;
        }
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
        if (key.Keycode == Key.R
            && Runner.State.Outcome == BattleOutcome.Ongoing
            && Runner.State.Tick >= RetreatUnlockTicks)
        {
            ApplyResultAndShowOutcome(won: false, retreated: true);
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

    private void OnTickAdvanced()
    {
        if (Runner.State.Outcome != BattleOutcome.Ongoing && !_resultApplied)
        {
            ApplyResultAndShowOutcome(
                won: Runner.State.Outcome == BattleOutcome.LeftVictory, retreated: false);
        }
    }

    private void ApplyResultAndShowOutcome(bool won, bool retreated)
    {
        if (_resultApplied)
        {
            return;
        }
        _resultApplied = true;
        Runner.Paused = true;

        var state = Runner.State;
        var goldBefore = GameSession.Profile.Gold;
        var damagePct = Mathf.Clamp(
            1f - state.RightSpireHp / state.Config.SpireMaxHp, 0f, 1f);
        var firstClear = CampaignProgress.ApplyBattleResult(
            GameSession.Profile, _level, won, state.Tick, damagePct);
        GameSession.SaveProfile();

        var goldEarned = GameSession.Profile.Gold - goldBefore;
        var headline = won ? "VICTORY" : retreated ? "RETREAT" : "DEFEAT";
        OutcomeLabel.Text = $"{headline}\n+{goldEarned} gold"
            + (firstClear ? "\nFirst clear! Next level unlocked." : string.Empty);
        OutcomePanel.Visible = true;
        JmoLogger.Info(this, $"[Battle] Result applied: {headline}, +{goldEarned}g");
    }

    private static ulong LevelSeed(string levelId)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in levelId)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private void OnUnitSpawned(SimUnit unit)
    {
        var sheetName = SheetNameFor(unit.Def.Id);
        var isStandIn = sheetName != StripEnemyPrefix(unit.Def.Id);
        var frames = _sprites.Load(unit.Def, sheetName, validateTiming: !isStandIn);
        if (frames is null)
        {
            return;
        }
        var view = new UnitView();
        UnitLayer.AddChild(view);
        view.Bind(unit, frames);
        _views[unit.InstanceId] = view;
    }

    /// <summary>Enemy defs are id-prefixed; the drake uses the whelp sheet until its art lands.</summary>
    private static string SheetNameFor(string defId)
    {
        var baseName = StripEnemyPrefix(defId);
        return baseName == UnitCatalog.RentalDragonId ? "frost_whelp" : baseName;
    }

    private static string StripEnemyPrefix(string defId)
    {
        return defId.StartsWith(CampaignLevelDef.EnemyIdPrefix)
            ? defId[CampaignLevelDef.EnemyIdPrefix.Length..]
            : defId;
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
