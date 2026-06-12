namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using System.Linq;
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

    [Export, RequiredExport] public Sprite2D LeftSpire { get; set; } = null!;

    [Export, RequiredExport] public Sprite2D RightSpire { get; set; } = null!;

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
    private Label? _parleyCountdown;
    private bool _breathHintShown;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        GameSession.EnsureProfileLoaded();
        _sprites = new UnitSpriteLibrary(SheetRoot);

        Runner.UnitSpawned += OnUnitSpawned;
        Runner.UnitDied += OnUnitDied;
        Runner.TickAdvanced += OnTickAdvanced;

        if (GameSession.LocalPvp)
        {
            // War Standard: clamped defs, full library, rental dragons, no rewards.
            var pvpDefs = Meta.WarStandard.BuildPvpDefs(UnitCatalog.FirstPlayable);
            Runner.Initialize(
                BattleConfig.Default,
                pvpDefs,
                ConduitDefs.All,
                seed: (ulong)Time.GetTicksUsec(),
                DraconicWars.Sim.Pacts.PactCatalog.All);
            Runner.State.Left.EquippedDragonId = UnitCatalog.RentalDragonId;
            Runner.State.Right.EquippedDragonId = UnitCatalog.RentalDragonId;
            Hud.Initialize(Runner, _localSide, pvpDefs, ConduitDefs.All);
            SetupPvp();
        }
        else
        {
            var levelIndex = Mathf.Clamp(
                GameSession.SelectedLevelIndex, 0, CampaignCatalog.Levels.Count - 1);
            _level = CampaignCatalog.Levels[levelIndex];
            var battleDefs = CampaignCatalog.BuildBattleDefs(_level, GameSession.Profile);
            Runner.Initialize(
                _level.Config,
                battleDefs,
                ConduitDefs.All,
                seed: LevelSeed(_level.Id),
                DraconicWars.Sim.Pacts.PactCatalog.All);
            Runner.Director = new WaveDirector(_level.Waves, _level.RepeatingWaves);
            Runner.State.Left.EquippedDragonId =
                GameSession.Profile.UnitLevels.ContainsKey("pyraxis")
                    ? "pyraxis"
                    : UnitCatalog.RentalDragonId;
            Runner.State.Right.EquippedDragonId = UnitCatalog.RentalDragonId;

            // Deploy cards show the LEVEL-SCALED defs the sim actually fights with —
            // base-catalog stats on the tooltip would misreport every upgraded unit.
            var playerLoadout = new List<DraconicWars.Sim.Units.UnitDef>();
            foreach (var def in battleDefs)
            {
                if (!def.Id.StartsWith(CampaignLevelDef.EnemyIdPrefix))
                {
                    playerLoadout.Add(def);
                }
            }
            CampaignProgress.EnsureBaseConduits(GameSession.Profile);
            var conduitLibrary = ConduitDefs.All
                .Where(def => GameSession.Profile.ConduitsUnlocked.Contains(def.Id))
                .ToList();
            Hud.Initialize(
                Runner, _localSide, playerLoadout, conduitLibrary,
                GameSession.Profile.UnitLevels);
        }
        Hud.DeployRequested += id => Runner.EnqueueCommand(SimCommand.Deploy(_localSide, id));
        Hud.AttuneRequested += OnAttuneRequested;
        Hud.ConduitBuildRequested += OnConduitBuildRequested;
        Hud.ConduitSellRequested += id => Runner.EnqueueCommand(SimCommand.SellConduit(_localSide, id));
        Hud.SocketPurchaseRequested += () => Runner.EnqueueCommand(SimCommand.BuySocket(_localSide));

        _breathBeam = new BreathBeamView();
        UnitLayer.AddChild(_breathBeam);

        ContinueButton.Pressed += () =>
            GetTree().ChangeSceneToFile(GameSession.LocalPvp
                ? "res://scenes/menu/main_menu.tscn"
                : "res://scenes/menu/level_select.tscn");
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
        Runner.SpeedMultiplier = !GameSession.LocalPvp && _fastForward && !breathHeld ? 2f : 1f;
        if (GameSession.LocalPvp)
        {
            ProcessPvpInput(delta);
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

    /// <summary>Rebreathing menu: lists the profile-OWNED attunements for the company
    /// (the sim doesn't know the meta — ownership is enforced here at the source).</summary>
    private void OnAttuneRequested(string defId)
    {
        var player = Runner.State.Player(_localSide);
        if (player.AttunedThisBattle.ContainsKey(defId))
        {
            ShowToast("That company has already re-sworn its Breath this battle",
                new Color(0.8f, 0.8f, 0.8f));
            return;
        }
        var baseDef = UnitCatalog.FirstPlayable.FirstOrDefault(d => d.Id == defId);
        if (baseDef is null || baseDef.Tier >= 4)
        {
            return;
        }
        var cost = (int)(baseDef.DeployCost * Runner.State.Config.RebreathCostFactor);
        var options = new List<Sim.Units.Element>();
        foreach (var key in GameSession.Profile.AttunementsOwned)
        {
            if (key.StartsWith(defId + ":")
                && System.Enum.TryParse<Sim.Units.Element>(
                    key[(defId.Length + 1)..], out var element)
                && element != baseDef.Element)
            {
                options.Add(element);
            }
        }
        if (options.Count == 0)
        {
            ShowToast($"No attunements unlocked for {baseDef.DisplayName} — see the Warband screen",
                new Color(0.8f, 0.8f, 0.8f));
            return;
        }

        var menu = new PopupMenu();
        for (var i = 0; i < options.Count; i++)
        {
            menu.AddItem($"Re-swear to {options[i]}  ({cost} mana, once per battle)", i);
        }
        var captured = options;
        menu.IdPressed += id => Runner.EnqueueCommand(
            SimCommand.AttuneUnit(_localSide, defId, captured[(int)id]));
        menu.PopupHide += menu.QueueFree;
        Hud.AddChild(menu);
        menu.Position = (Vector2I)GetViewport().GetMousePosition();
        menu.Popup();
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
        UpdateSpires();
        if (GameSession.LocalPvp)
        {
            UpdatePvpHud();
        }

        if (player.WrathCooldownTicks > _lastWrathCooldown)
        {
            FlashScreen(new Color(0.95f, 0.76f, 0.17f, 0.28f));
        }
        _lastWrathCooldown = player.WrathCooldownTicks;

        if (player.AscensionTier > _lastAscensionTier)
        {
            _lastAscensionTier = player.AscensionTier;
            FlashScreen(new Color(0.66f, 0.52f, 0.95f, 0.35f));
            ShowToast(TierUpText(player.AscensionTier), new Color(0.78f, 0.65f, 1f));
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
        if (!GameSession.LocalPvp && opponent.AwaitingParley && opponent.PendingOffers.Count > 0)
        {
            // Scripted opponent picks its first offer (persona AI is a later Part).
            Runner.EnqueueCommand(SimCommand.SealPact(
                _localSide == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left,
                opponent.PendingOffers[0]));
        }

        var local = state.Player(_localSide);
        if (local.AwaitingParley && _draftPanel is null)
        {
            FlashScreen(new Color(0.95f, 0.72f, 0.35f, 0.25f));
            ShowToast("A war-bell tolls — the Broker attends your ascent",
                new Color(0.98f, 0.82f, 0.5f));
            ShowDraftPanel(local);
        }
        else if (!local.AwaitingParley && _draftPanel is not null)
        {
            _draftPanel.QueueFree();
            _draftPanel = null;
            _parleyCountdown = null;
        }
        if (local.AwaitingParley && _parleyCountdown is not null)
        {
            var ticksLeft = Mathf.Max(0, local.ParleyDeadlineTick - state.Tick);
            _parleyCountdown.Text =
                $"Seal within {ticksLeft / state.Config.TickRate + 1}s — or the Broker chooses";
        }
    }

    private string TierUpText(int tier)
    {
        if (tier >= 4)
        {
            return "DRAGON TIER — the Crossing Toll may be paid (hold E)";
        }
        var unlocked = new List<string>();
        foreach (var def in UnitCatalog.FirstPlayable)
        {
            if (def.Tier == tier)
            {
                unlocked.Add(def.DisplayName);
            }
        }
        var roman = tier == 2 ? "II" : "III";
        return unlocked.Count > 0
            ? $"TIER {roman} — unlocked: {string.Join(", ", unlocked)}"
            : $"TIER {roman}";
    }

    private void ShowToast(string text, Color color)
    {
        var toast = new Label
        {
            Text = text,
            Modulate = color,
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorRight = 1f,
            OffsetTop = 48f,
            OffsetBottom = 64f,
        };
        Hud.AddChild(toast);
        var tween = toast.CreateTween();
        tween.TweenInterval(1.6);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.8);
        tween.Parallel().TweenProperty(toast, "offset_top", 30f, 0.8);
        tween.TweenCallback(Callable.From(toast.QueueFree));
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
        _parleyCountdown = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(0.98f, 0.82f, 0.5f),
        };
        vbox.AddChild(_parleyCountdown);

        foreach (var offerId in local.PendingOffers)
        {
            var def = DraconicWars.Sim.Pacts.PactCatalog.ById(offerId);
            var price = EffectText.ForPactPrice(def);
            var priceLine = price.Length > 0 ? $"  — PRICE: {price}" : string.Empty;
            var button = new Button
            {
                Text = $"[{def.Tier}] {def.DisplayName}{priceLine}\n"
                    + $"{EffectText.ForPact(def)}\n{def.Lore}",
                CustomMinimumSize = new Vector2(300, 40),
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

    /// <summary>Sheet layout: 4 tier columns x 3 damage rows. The spire reads the
    /// battle — its crown shows the side's Ascension, its wounds show its HP thirds.</summary>
    private void UpdateSpires()
    {
        var state = Runner.State;
        LeftSpire.Frame = SpireFrame(state.Left.AscensionTier, state.LeftSpireHp, state.Config.SpireMaxHp);
        RightSpire.Frame = SpireFrame(state.Right.AscensionTier, state.RightSpireHp, state.Config.SpireMaxHp);
    }

    private static int SpireFrame(int tier, float hp, float maxHp)
    {
        var damageRow = hp >= maxHp * 2f / 3f ? 0 : hp >= maxHp / 3f ? 1 : 2;
        return damageRow * 4 + Mathf.Clamp(tier, 1, 4) - 1;
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
        if (GameSession.LocalPvp)
        {
            // War Standard: no profile rewards; just call the field.
            var pvpHeadline = Runner.State.Outcome switch
            {
                BattleOutcome.LeftVictory => "PLAYER 1 TAKES THE SPAN",
                BattleOutcome.RightVictory => "PLAYER 2 TAKES THE SPAN",
                _ => retreated ? (won ? "PLAYER 2 CONCEDES" : "PLAYER 1 CONCEDES")
                    : "THE COURT CALLS THE FIELD — DRAW",
            };
            OutcomeLabel.Text = pvpHeadline;
            OutcomePanel.Visible = true;
            JmoLogger.Info(this, $"[Battle] PvP result: {pvpHeadline}");
            return;
        }
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
            + (won ? string.Empty : "\nWar salvage — defeat still pays for time held and spire damage dealt")
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

        // Breath onboarding: most ground units cannot strike the air lane — the
        // first hostile flyer teaches the verb (full tank = breath never fired).
        if (!_breathHintShown
            && unit.Side != _localSide
            && unit.Def.Stratum == Sim.Units.Stratum.Air
            && Runner.State.Player(_localSide).BreathEnergySeconds
                >= Runner.State.Config.BreathMaxSeconds - 0.01f)
        {
            _breathHintShown = true;
            ShowToast("Aerial raiders! Hold RIGHT-CLICK to aim your dragon's breath",
                new Color(0.62f, 0.86f, 1f));
        }
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
