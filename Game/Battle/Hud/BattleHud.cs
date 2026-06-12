namespace DraconicWars.Game.Battle.Hud;

using System;
using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using DraconicWars.Sim.Units;
using Godot;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// The battle HUD for the local player side: deploy bar, conduit row, readouts.
/// Reads sim state every tick; never mutates it — all player intents surface as
/// events the scene controller turns into SimCommands.
/// </summary>
public partial class BattleHud : CanvasLayer
{
    public event Action<string> DeployRequested = delegate { };

    public event Action<string> AttuneRequested = delegate { };

    public event Action<string> ConduitBuildRequested = delegate { };

    public event Action<string> ConduitSellRequested = delegate { };

    public event Action SocketPurchaseRequested = delegate { };

    [Export, RequiredExport] public Label ManaLabel { get; set; } = null!;

    [Export, RequiredExport] public Label ClockLabel { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar LeftSpireBar { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar RightSpireBar { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar AscensionBar { get; set; } = null!;

    [Export, RequiredExport] public Label AscensionLabel { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar BreathBar { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar SummonBar { get; set; } = null!;

    [Export, RequiredExport] public Label SynergyLabel { get; set; } = null!;

    [Export, RequiredExport] public Label EdictLabel { get; set; } = null!;

    [Export, RequiredExport] public HBoxContainer DeployBar { get; set; } = null!;

    [Export, RequiredExport] public HBoxContainer ConduitRow { get; set; } = null!;

    [Export, RequiredExport] public PackedScene UnitCardScene { get; set; } = null!;

    private readonly List<UnitCard> _cards = new();
    private readonly Dictionary<string, Button> _conduitButtons = new();
    private readonly List<string> _conduitOrder = new();
    private Button _socketButton = null!;

    /// <summary>Deploy cards in bar order (PvP cursor + tests).</summary>
    public IReadOnlyList<UnitCard> Cards => _cards;

    /// <summary>Conduit ids in strip order (PvP cursor).</summary>
    public IReadOnlyList<string> ConduitOrder => _conduitOrder;

    public Button? ConduitButton(string conduitId)
    {
        return _conduitButtons.GetValueOrDefault(conduitId);
    }
    private readonly Dictionary<string, UnitDef> _loadoutDefs = new();
    private BattleRunner _runner = null!;
    private PlayerSide _side;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
    }

    public void Initialize(
        BattleRunner runner,
        PlayerSide side,
        IEnumerable<UnitDef> loadout,
        IEnumerable<ConduitDef> conduits,
        IReadOnlyDictionary<string, int>? unitLevels = null)
    {
        _runner = runner;
        _side = side;

        foreach (var def in loadout)
        {
            if (def.Tier >= 4)
            {
                continue;
            }
            _loadoutDefs[def.Id] = def;
            var card = UnitCardScene.Instantiate<UnitCard>();
            DeployBar.AddChild(card);
            card.Bind(def, unitLevels?.GetValueOrDefault(def.Id) ?? 0);
            card.DeployRequested += id => DeployRequested(id);
            card.AttuneRequested += id => AttuneRequested(id);
            _cards.Add(card);
        }

        foreach (var conduit in conduits)
        {
            var button = new Button
            {
                Text = $"{conduit.DisplayName}\n{conduit.CostForTier(1)}",
                CustomMinimumSize = new Vector2(54, 20),
            };
            var conduitId = conduit.Id;
            button.Pressed += () => ConduitBuildRequested(conduitId);
            button.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                {
                    ConduitSellRequested(conduitId);
                }
            };
            ConduitRow.AddChild(button);
            _conduitButtons[conduitId] = button;
            _conduitOrder.Add(conduitId);
        }

        _socketButton = new Button
        {
            CustomMinimumSize = new Vector2(54, 20),
            Visible = false,
        };
        _socketButton.Pressed += () => SocketPurchaseRequested();
        ConduitRow.AddChild(_socketButton);

        _runner.TickAdvanced += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var state = _runner.State;
        var player = state.Player(_side);
        var config = state.Config;

        var fielded = 0;
        foreach (var unit in state.Units)
        {
            if (unit.Side == _side && unit.IsAlive)
            {
                fielded++;
            }
        }
        ManaLabel.Text = $"{(int)player.Mana}/{(int)player.EffectiveWalletCap}"
            + $"  F{fielded}/{config.MaxFieldedPerSide}";
        var seconds = state.Tick / config.TickRate;
        ClockLabel.Text = $"{seconds / 60}:{seconds % 60:00}";

        LeftSpireBar.Value = 100.0 * state.LeftSpireHp / config.SpireMaxHp;
        RightSpireBar.Value = 100.0 * state.RightSpireHp / config.SpireMaxHp;

        var tier = player.AscensionTier;
        AscensionLabel.Text = tier >= 4 ? "DRAGON" : $"Tier {tier}";
        if (tier >= 4)
        {
            AscensionBar.Value = 100.0;
        }
        else
        {
            var previous = tier >= 2 ? config.AscensionThresholds[tier - 2] : 0f;
            var target = config.AscensionThresholds[tier - 1];
            AscensionBar.Value = 100.0 * (player.AscensionMeter - previous) / (target - previous);
        }

        BreathBar.Value = 100.0 * player.BreathEnergySeconds / config.BreathMaxSeconds;

        _socketButton.Visible = player.BonusSockets == 0
            && player.AscensionTier >= config.SocketPurchaseTierGate;
        if (_socketButton.Visible)
        {
            _socketButton.Text = $"+Socket\n{(int)config.SocketPurchaseCost}";
            _socketButton.Disabled = player.Mana < config.SocketPurchaseCost;
            _socketButton.TooltipText =
                "Graft a 4th utility socket onto the spire (once per battle)";
        }
        SummonBar.Value = 100.0 * player.SummoningProgress / config.SummoningCost;
        SummonBar.Visible = tier >= 4;

        var synergyText = string.Empty;
        foreach (var element in System.Enum.GetValues<DraconicWars.Sim.Units.Element>())
        {
            var synergyTier = ElementSynergies.TierFor(state, _side, element);
            if (synergyTier > 0)
            {
                synergyText += $"{element} {(synergyTier >= 2 ? "II" : "I")}  ";
            }
        }
        SynergyLabel.Text = synergyText;
        if (SynergyLabel.GetParent() is Control synergyChip)
        {
            synergyChip.Visible = synergyText.Length > 0;
        }

        RefreshEdicts(state, player);

        foreach (var card in _cards)
        {
            var defId = card.UnitDefId;
            player.DeployCooldowns.TryGetValue(defId, out var cooldownLeft);
            var def = FindDef(defId);
            card.UpdateState(
                player.Mana, cooldownLeft,
                def?.DeployCooldownTicks ?? 0,
                def is not null && def.Tier > tier);
            if (player.AttunedThisBattle.TryGetValue(defId, out var attunedElement))
            {
                card.ShowAttuned(attunedElement);
            }
        }

        foreach (var (conduitId, button) in _conduitButtons)
        {
            var built = player.Conduits.TryGetValue(conduitId, out var conduitTier);
            var def = ConduitDefs.ById(conduitId);
            button.Text = built
                ? conduitTier >= ConduitDef.MaxTier
                    ? $"{def.DisplayName}\nT{conduitTier} MAX"
                    : $"{def.DisplayName}\nT{conduitTier}→{def.CostForTier(conduitTier + 1)}"
                : $"{def.DisplayName}\n{def.CostForTier(1)}";
            button.TooltipText = built
                ? $"{def.DisplayName} T{conduitTier}: {EffectText.ForConduit(def, conduitTier)}"
                    + (conduitTier < ConduitDef.MaxTier
                        ? $"\nUpgrade T{conduitTier + 1}: {EffectText.ForConduit(def, conduitTier + 1)}"
                            + $" ({def.CostForTier(conduitTier + 1)} mana)"
                        : string.Empty)
                    + "\nRight-click sells for 50% of spent mana"
                : $"{def.DisplayName} T1: {EffectText.ForConduit(def, 1)}"
                    + $" ({def.CostForTier(1)} mana)\nOne socket per conduit type";
        }
    }

    private UnitDef? FindDef(string defId)
    {
        return _loadoutDefs.GetValueOrDefault(defId);
    }

    /// <summary>Published trials for the player's CURRENT segment: progress while
    /// unclaimed, a check once claimed, "(half)" when racing for the runner-up share.</summary>
    private void RefreshEdicts(BattleState state, PlayerState player)
    {
        var segment = System.Math.Min(player.AscensionTier - 1, 2);
        var parts = new List<string>(2);
        foreach (var edict in state.Edicts)
        {
            if (edict.TierIndex != segment)
            {
                continue;
            }
            if (edict.ClaimedBy(_side))
            {
                parts.Add($"✓ {edict.Def.DisplayName.Replace("Edict of the ", "")}");
                continue;
            }
            var progress = DraconicWars.Sim.Edicts.EdictProgress.Of(player, edict.Def);
            var halfNote = edict.FirstClaimant is not null ? " (half)" : string.Empty;
            parts.Add($"{edict.Def.DisplayName.Replace("Edict of the ", "")}"
                + $" {progress:0}/{edict.Def.Threshold:0}{halfNote}");
        }
        EdictLabel.Text = string.Join("  ·  ", parts);
        if (EdictLabel.GetParent() is Control edictChip)
        {
            edictChip.Visible = parts.Count > 0 && player.AscensionTier < 4;
        }
    }

    #region Test Helpers
#if TOOLS
    internal IReadOnlyList<UnitCard> _TestCards => _cards;
#endif
    #endregion
}
