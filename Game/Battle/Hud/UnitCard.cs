namespace DraconicWars.Game.Battle.Hud;

using System;
using System.Collections.Generic;
using DraconicWars.Sim.Units;
using Godot;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// One deploy-bar card: cost, cooldown sweep, affordability/tier-lock states.
/// Instanced from unit_card.tscn per loadout slot (runtime-known count).
/// </summary>
public partial class UnitCard : Button
{
    public event Action<string> DeployRequested = delegate { };

    /// <summary>Right-click: open the Rebreathing menu for this company.</summary>
    public event Action<string> AttuneRequested = delegate { };

    [Export, RequiredExport] public Label NameLabel { get; set; } = null!;

    [Export, RequiredExport] public Label CostLabel { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar CooldownBar { get; set; } = null!;

    public string UnitDefId { get; private set; } = string.Empty;

    private int _deployCost;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        Pressed += () => DeployRequested(UnitDefId);
        GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            {
                AttuneRequested(UnitDefId);
            }
        };
    }

    private bool _attunedShown;

    /// <summary>Marks the card once its company has re-sworn this battle.</summary>
    public void ShowAttuned(Element element)
    {
        if (_attunedShown)
        {
            return;
        }
        _attunedShown = true;
        NameLabel.Modulate = ElementColors.Of(element);
        TooltipText += $"\nRe-sworn to {element} this battle";
    }

    public void Bind(UnitDef def, int level = 0)
    {
        UnitDefId = def.Id;
        _deployCost = def.DeployCost;
        NameLabel.Text = ShortName(def.DisplayName);
        NameLabel.Modulate = ElementColors.Of(def.Element);
        CostLabel.Text = def.DeployCost.ToString();
        var levelLine = level > 0 ? $"  Lv{level}" : string.Empty;
        var lore = DraconicWars.Game.Content.UnitLore.For(def.Id);
        var loreLines = lore.Title.Length > 0 ? $"\n{lore.Title}\n\"{lore.Flavor}\"" : string.Empty;
        TooltipText = $"{def.DisplayName}{levelLine}\nTier {def.Tier} {def.Element} {def.TypeClass}\n"
            + $"HP {def.MaxHp}  DMG {def.Damage}  RNG {def.Range}\n"
            + $"Speed {def.MoveSpeed:0.#} m/s  Attack {def.ForeswingTicks + def.BackswingTicks}t"
            + $"  KB x{def.KnockbackCount}{loreLines}";
    }

    /// <summary>Cards are 56px wide; the last word stays unique across the roster
    /// ("Spearman", "Gryphon") while the tooltip carries the full name.</summary>
    private static string ShortName(string displayName)
    {
        var lastSpace = displayName.LastIndexOf(' ');
        return lastSpace < 0 ? displayName : displayName[(lastSpace + 1)..];
    }

    public void UpdateState(float mana, int cooldownTicksLeft, int cooldownTotal, bool tierLocked)
    {
        var affordable = mana >= _deployCost;
        var cooling = cooldownTicksLeft > 0;
        Disabled = !affordable || cooling || tierLocked;
        CooldownBar.Value = cooldownTotal <= 0 || !cooling
            ? 0
            : 100.0 * cooldownTicksLeft / cooldownTotal;
        Modulate = tierLocked ? new Color(0.55f, 0.55f, 0.7f) : Colors.White;
    }
}
