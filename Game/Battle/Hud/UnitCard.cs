namespace DraconicWars.Game.Battle.Hud;

using System;
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

    [Export, RequiredExport] public Label NameLabel { get; set; } = null!;

    [Export, RequiredExport] public Label CostLabel { get; set; } = null!;

    [Export, RequiredExport] public ProgressBar CooldownBar { get; set; } = null!;

    public string UnitDefId { get; private set; } = string.Empty;

    private int _deployCost;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        Pressed += () => DeployRequested(UnitDefId);
    }

    public void Bind(UnitDef def)
    {
        UnitDefId = def.Id;
        _deployCost = def.DeployCost;
        NameLabel.Text = def.DisplayName;
        CostLabel.Text = def.DeployCost.ToString();
        TooltipText = $"{def.DisplayName}\nTier {def.Tier} {def.Element} {def.TypeClass}\n"
            + $"HP {def.MaxHp}  DMG {def.Damage}  RNG {def.Range}";
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
