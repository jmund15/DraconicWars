namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using DraconicWars.Game.Battle.Hud;
using DraconicWars.Sim.Battle;
using Godot;

/// <summary>
/// Player 2's control surface for local PvP (gamepad, War Standard). P2 shares the
/// same deploy/conduit strips as P1 (identical clamped defs make that honest) via a
/// cursor overlay: LB/RB cycle cards, A deploys, Y cycles conduits, X builds/upgrades
/// (or tithes during a parley), B casts Wrath, Start buys the T3 socket, Back
/// concedes; right stick sweeps the breath reticle and RT holds the flame; LT
/// channels the Crossing Toll; D-pad picks parley offers.
/// </summary>
public partial class BattleSceneController
{
    private const float ReticleSpeed = 22f;

    private int _p2CardIndex;
    private int _p2ConduitIndex;
    private int _p2OfferIndex;
    private float _p2AimX;
    private ColorRect _p2Reticle = null!;
    private ColorRect _p2CardCursor = null!;
    private ColorRect _p2ConduitCursor = null!;
    private Label _p2Readout = null!;
    private PanelContainer? _p2ParleyPanel;
    private readonly List<Label> _p2OfferLabels = new();

    private void SetupPvp()
    {
        _p2AimX = Runner.State.Config.LaneLength - 4f;

        _p2Reticle = new ColorRect
        {
            Color = new Color(0.95f, 0.35f, 0.3f, 0.8f),
            Size = new Vector2(2, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_p2Reticle);

        _p2CardCursor = MakeCursor(new Color(0.95f, 0.4f, 0.35f));
        _p2ConduitCursor = MakeCursor(new Color(0.95f, 0.4f, 0.35f));

        _p2Readout = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -150f,
            OffsetRight = -4f,
            OffsetTop = 46f,
            OffsetBottom = 58f,
            Modulate = new Color(0.98f, 0.7f, 0.65f),
        };
        Hud.AddChild(_p2Readout);
    }

    private ColorRect MakeCursor(Color color)
    {
        var cursor = new ColorRect
        {
            Color = color,
            Size = new Vector2(56, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Hud.AddChild(cursor);
        return cursor;
    }

    private void ProcessPvpInput(double delta)
    {
        if (Runner.State.Outcome != BattleOutcome.Ongoing)
        {
            return;
        }
        var stick = Input.GetJoyAxis(0, JoyAxis.RightX);
        if (Mathf.Abs(stick) > 0.2f)
        {
            _p2AimX = Mathf.Clamp(
                _p2AimX + stick * ReticleSpeed * (float)delta,
                0f, Runner.State.Config.LaneLength);
        }
        _p2Reticle.Position = new Vector2(
            _p2AimX * LaneGeometry.PixelsPerMeter - 1, LaneGeometry.AirY - 24);

        if (Input.GetJoyAxis(0, JoyAxis.TriggerRight) > 0.5f)
        {
            Runner.EnqueueCommand(SimCommand.FireBreath(PlayerSide.Right, _p2AimX));
        }
        if (Input.GetJoyAxis(0, JoyAxis.TriggerLeft) > 0.5f)
        {
            Runner.EnqueueCommand(SimCommand.ChannelMana(PlayerSide.Right, ChannelPerTick));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!GameSession.LocalPvp
            || @event is not InputEventJoypadButton { Pressed: true } joy)
        {
            return;
        }
        var right = Runner.State.Right;
        switch (joy.ButtonIndex)
        {
            case JoyButton.LeftShoulder:
                _p2CardIndex = Wrap(_p2CardIndex - 1, Hud.Cards.Count);
                break;
            case JoyButton.RightShoulder:
                _p2CardIndex = Wrap(_p2CardIndex + 1, Hud.Cards.Count);
                break;
            case JoyButton.A when right.AwaitingParley && right.PendingOffers.Count > 0:
                Runner.EnqueueCommand(SimCommand.SealPact(
                    PlayerSide.Right,
                    right.PendingOffers[Mathf.Clamp(_p2OfferIndex, 0, right.PendingOffers.Count - 1)]));
                break;
            case JoyButton.A when Hud.Cards.Count > 0:
                Runner.EnqueueCommand(SimCommand.Deploy(
                    PlayerSide.Right, Hud.Cards[_p2CardIndex].UnitDefId));
                break;
            case JoyButton.X when right.AwaitingParley:
                Runner.EnqueueCommand(SimCommand.PayTithe(PlayerSide.Right));
                break;
            case JoyButton.X when Hud.ConduitOrder.Count > 0:
                var conduitId = Hud.ConduitOrder[_p2ConduitIndex];
                Runner.EnqueueCommand(right.Conduits.ContainsKey(conduitId)
                    ? SimCommand.UpgradeConduit(PlayerSide.Right, conduitId)
                    : SimCommand.BuildConduit(PlayerSide.Right, conduitId));
                break;
            case JoyButton.Y:
                _p2ConduitIndex = Wrap(_p2ConduitIndex + 1, Hud.ConduitOrder.Count);
                break;
            case JoyButton.B:
                Runner.EnqueueCommand(SimCommand.CastWrath(PlayerSide.Right));
                break;
            case JoyButton.DpadUp:
                _p2OfferIndex = Wrap(_p2OfferIndex - 1, right.PendingOffers.Count);
                break;
            case JoyButton.DpadDown:
                _p2OfferIndex = Wrap(_p2OfferIndex + 1, right.PendingOffers.Count);
                break;
            case JoyButton.Start:
                Runner.EnqueueCommand(SimCommand.BuySocket(PlayerSide.Right));
                break;
            case JoyButton.Back when Runner.State.Tick >= RetreatUnlockTicks:
                ApplyResultAndShowOutcome(won: true, retreated: true);
                break;
        }
    }

    private static int Wrap(int value, int count)
    {
        return count <= 0 ? 0 : (value % count + count) % count;
    }

    private void UpdatePvpHud()
    {
        var state = Runner.State;
        var right = state.Right;

        if (Hud.Cards.Count > 0)
        {
            var card = Hud.Cards[Mathf.Clamp(_p2CardIndex, 0, Hud.Cards.Count - 1)];
            _p2CardCursor.GlobalPosition = card.GlobalPosition - new Vector2(0, 4);
            _p2CardCursor.Size = new Vector2(card.Size.X, 2);
        }
        if (Hud.ConduitOrder.Count > 0)
        {
            var button = Hud.ConduitButton(
                Hud.ConduitOrder[Mathf.Clamp(_p2ConduitIndex, 0, Hud.ConduitOrder.Count - 1)]);
            if (button is not null)
            {
                _p2ConduitCursor.GlobalPosition = button.GlobalPosition
                    + new Vector2(0, button.Size.Y + 1);
                _p2ConduitCursor.Size = new Vector2(button.Size.X, 2);
            }
        }

        var fielded = 0;
        foreach (var unit in state.Units)
        {
            if (unit.Side == PlayerSide.Right && unit.IsAlive)
            {
                fielded++;
            }
        }
        _p2Readout.Text = $"P2  {(int)right.Mana}/{(int)right.EffectiveWalletCap}"
            + $"  F{fielded}/{state.Config.MaxFieldedPerSide}"
            + $"  T{right.AscensionTier}";

        UpdateP2Parley(right);
    }

    private void UpdateP2Parley(PlayerState right)
    {
        if (right.AwaitingParley && _p2ParleyPanel is null)
        {
            _p2OfferIndex = 0;
            _p2ParleyPanel = new PanelContainer
            {
                AnchorLeft = 1f,
                AnchorRight = 1f,
                AnchorTop = 0.5f,
                AnchorBottom = 0.5f,
                OffsetLeft = -300f,
                OffsetRight = -6f,
                OffsetTop = -60f,
            };
            var vbox = new VBoxContainer();
            _p2ParleyPanel.AddChild(vbox);
            vbox.AddChild(new Label
            {
                Text = "P2 PARLEY — D-pad picks · A seals · X tithes",
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            _p2OfferLabels.Clear();
            for (var i = 0; i < 3; i++)
            {
                var label = new Label();
                _p2OfferLabels.Add(label);
                vbox.AddChild(label);
            }
            Hud.AddChild(_p2ParleyPanel);
        }
        else if (!right.AwaitingParley && _p2ParleyPanel is not null)
        {
            _p2ParleyPanel.QueueFree();
            _p2ParleyPanel = null;
            _p2OfferLabels.Clear();
        }

        if (_p2ParleyPanel is null)
        {
            return;
        }
        for (var i = 0; i < _p2OfferLabels.Count; i++)
        {
            if (i >= right.PendingOffers.Count)
            {
                _p2OfferLabels[i].Text = string.Empty;
                continue;
            }
            var def = DraconicWars.Sim.Pacts.PactCatalog.ById(right.PendingOffers[i]);
            var price = EffectText.ForPactPrice(def);
            var priceLine = price.Length > 0 ? $" — PRICE: {price}" : string.Empty;
            _p2OfferLabels[i].Text =
                $"{(i == _p2OfferIndex ? "> " : "  ")}[{def.Tier}] {def.DisplayName}{priceLine}";
            _p2OfferLabels[i].Modulate = i == _p2OfferIndex
                ? new Color(0.98f, 0.82f, 0.5f)
                : Colors.White;
        }
    }
}
