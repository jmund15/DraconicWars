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
    private const int ChannelPerTick = 15;
    private const int RetreatUnlockTicks = 60 * 30;

    [Export, RequiredExport] public BattleRunner Runner { get; set; } = null!;

    [Export, RequiredExport] public Node2D UnitLayer { get; set; } = null!;

    [Export, RequiredExport] public BattleHud Hud { get; set; } = null!;

    [Export, RequiredExport] public PanelContainer OutcomePanel { get; set; } = null!;

    [Export, RequiredExport] public Label OutcomeLabel { get; set; } = null!;

    [Export, RequiredExport] public Button ContinueButton { get; set; } = null!;

    private readonly Dictionary<int, UnitView> _views = new();
    private UnitSpriteLibrary _sprites = null!;
    private CampaignLevelDef _level = null!;
    private PlayerSide _localSide = PlayerSide.Left;
    private bool _resultApplied;
    private bool _fastForward;
    private BreathBeamView _breathBeam = null!;
    private int _lastWrathCooldown;
    private int _lastAscensionTier = 1;
    private PanelContainer? _draftPanel;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        GameSession.EnsureProfileLoaded();
        _sprites = new UnitSpriteLibrary(SheetRoot);

        var levelIndex = Mathf.Clamp(
            GameSession.SelectedLevelIndex, 0, CampaignCatalog.Levels.Count - 1);
        _level = CampaignCatalog.Levels[levelIndex];

        Runner.UnitSpawned += OnUnitSpawned;
        Runner.UnitDied += OnUnitDied;
        Runner.TickAdvanced += OnTickAdvanced;
        Runner.Initialize(
            _level.Config,
            CampaignCatalog.BuildBattleDefs(_level, GameSession.Profile),
            ConduitDefs.All,
            seed: LevelSeed(_level.Id),
            DraconicWars.Sim.Pacts.PactCatalog.All);
        Runner.Director = new WaveDirector(_level.Waves, _level.RepeatingWaves);
        Runner.State.Left.EquippedDragonId =
            GameSession.Profile.UnitLevels.ContainsKey("pyraxis")
                ? "pyraxis"
                : UnitCatalog.RentalDragonId;
        Runner.State.Right.EquippedDragonId = UnitCatalog.RentalDragonId;

        Hud.Initialize(Runner, _localSide, UnitCatalog.FirstPlayable, ConduitDefs.All);
        Hud.DeployRequested += id => Runner.EnqueueCommand(SimCommand.Deploy(_localSide, id));
        Hud.ConduitBuildRequested += OnConduitBuildRequested;
        Hud.ConduitSellRequested += id => Runner.EnqueueCommand(SimCommand.SellConduit(_localSide, id));

        _breathBeam = new BreathBeamView();
        UnitLayer.AddChild(_breathBeam);

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
        var breathHeld = Input.IsMouseButtonPressed(MouseButton.Right);
        var player = Runner.State.Player(_localSide);
        var hasEnergy = player.BreathEnergySeconds >= 1f / Runner.State.Config.TickRate;
        if (breathHeld)
        {
            var laneX = GetGlobalMousePosition().X / LaneGeometry.PixelsPerMeter;
            Runner.EnqueueCommand(SimCommand.FireBreath(_localSide, laneX));
        }
        var perchX = _localSide == PlayerSide.Left ? 1.2f : Runner.State.Config.LaneLength - 1.2f;
        var perch = new Vector2(perchX * LaneGeometry.PixelsPerMeter, LaneGeometry.AirY - 36f);
        var aim = GetGlobalMousePosition();
        _breathBeam.UpdateBeam(perch, aim, breathHeld && hasEnergy, delta);
        if (Input.IsKeyPressed(Key.E))
        {
            Runner.EnqueueCommand(SimCommand.ChannelMana(_localSide, ChannelPerTick));
        }
        // PvE 2x speed toggle (design.md §12) — drops to 1x while breath is aimed.
        Runner.SpeedMultiplier = _fastForward && !breathHeld ? 2f : 1f;
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
        if (key.Keycode == Key.T)
        {
            _fastForward = !_fastForward;
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
        var player = Runner.State.Player(_localSide);
        UpdateDraftState();

        if (player.WrathCooldownTicks > _lastWrathCooldown)
        {
            FlashScreen(new Color(0.95f, 0.76f, 0.17f, 0.28f));
        }
        _lastWrathCooldown = player.WrathCooldownTicks;

        if (player.AscensionTier > _lastAscensionTier)
        {
            _lastAscensionTier = player.AscensionTier;
            FlashScreen(new Color(0.66f, 0.52f, 0.95f, 0.35f));
        }

        if (Runner.State.Outcome != BattleOutcome.Ongoing && !_resultApplied)
        {
            ApplyResultAndShowOutcome(
                won: Runner.State.Outcome == BattleOutcome.LeftVictory, retreated: false);
        }
    }

    private void UpdateDraftState()
    {
        var state = Runner.State;
        var opponent = state.Player(
            _localSide == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left);
        if (opponent.AwaitingParley && opponent.PendingOffers.Count > 0)
        {
            // Scripted opponent picks its first offer (persona AI is a later Part).
            Runner.EnqueueCommand(SimCommand.SealPact(
                _localSide == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left,
                opponent.PendingOffers[0]));
        }

        var local = state.Player(_localSide);
        if (local.AwaitingParley && _draftPanel is null)
        {
            ShowDraftPanel(local);
        }
        else if (!local.AwaitingParley && _draftPanel is not null)
        {
            _draftPanel.QueueFree();
            _draftPanel = null;
        }
    }

    private void ShowDraftPanel(PlayerState local)
    {
        var panel = new PanelContainer();
        var vbox = new VBoxContainer();
        panel.AddChild(vbox);
        vbox.AddChild(new Label
        {
            Text = "PARLEY — the Broker offers terms",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        foreach (var offerId in local.PendingOffers)
        {
            var def = DraconicWars.Sim.Pacts.PactCatalog.ById(offerId);
            var price = PriceText(def);
            var button = new Button
            {
                Text = $"[{def.Tier}] {def.DisplayName}{price}\n{def.Lore}",
                CustomMinimumSize = new Vector2(280, 32),
                Modulate = def.Tier switch
                {
                    DraconicWars.Sim.Pacts.PactTier.Drake => Color.FromHtml("4fa4f9"),
                    DraconicWars.Sim.Pacts.PactTier.Wyrm => Color.FromHtml("a884f3"),
                    _ => Color.FromHtml("f9a875"),
                },
            };
            var pickedId = offerId;
            button.Pressed += () =>
                Runner.EnqueueCommand(SimCommand.SealPact(_localSide, pickedId));
            vbox.AddChild(button);
        }

        var titheCost = Runner.State.Config.TitheCostMana;
        var tithe = new Button
        {
            Text = $"Tithe the Broker — fresh terms ({(int)titheCost} mana)",
            Disabled = local.Mana < titheCost,
            CustomMinimumSize = new Vector2(280, 22),
        };
        tithe.Pressed += () =>
        {
            Runner.EnqueueCommand(SimCommand.PayTithe(_localSide));
            _draftPanel?.QueueFree();
            _draftPanel = null;
        };
        vbox.AddChild(tithe);

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
        };
        center.AddChild(panel);
        Hud.AddChild(center);
        _draftPanel = panel;

        panel.TreeExited += () =>
        {
            if (IsInstanceValid(center))
            {
                center.QueueFree();
            }
        };
    }

    private static string PriceText(DraconicWars.Sim.Pacts.PactDef def)
    {
        var parts = new List<string>(2);
        if (def.PriceSpireHpPct > 0f)
        {
            parts.Add($"{(int)(def.PriceSpireHpPct * 100)}% spire blood");
        }
        if (def.PriceDripPerSecond > 0f)
        {
            parts.Add($"-{def.PriceDripPerSecond:0.#} drip");
        }
        return parts.Count == 0 ? string.Empty : $"  — PRICE: {string.Join(", ", parts)}";
    }

    private void FlashScreen(Color color)
    {
        var flash = new ColorRect
        {
            Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f,
        };
        Hud.AddChild(flash);
        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "modulate:a", 0f, 0.45);
        tween.TweenCallback(Callable.From(flash.QueueFree));
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
        var frames = _sprites.Load(unit.Def, StripEnemyPrefix(unit.Def.Id), validateTiming: true);
        if (frames is null)
        {
            return;
        }
        var view = new UnitView();
        UnitLayer.AddChild(view);
        view.Bind(unit, frames);
        _views[unit.InstanceId] = view;
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
