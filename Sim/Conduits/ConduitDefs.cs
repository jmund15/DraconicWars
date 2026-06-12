namespace DraconicWars.Sim.Conduits;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// V1 conduit catalog (design.md §7). Tuning values are provisional pending the
/// economy spreadsheet; the Mana Well tier-1 payback ≥150s constraint is test-pinned.
/// </summary>
public static class ConduitDefs
{
    public static readonly IReadOnlyList<ConduitDef> All = new[]
    {
        new ConduitDef("mana_well", "Mana Well", BaseCost: 300, DripBonusPerTier: 2f),
        new ConduitDef("aurum_vault", "Aurum Vault", BaseCost: 150,
            WalletCapPerTier: 150f, KillBountyPctPerTier: 0.2f),
        new ConduitDef("war_horn", "War Horn", BaseCost: 200, DamagePctPerTier: 0.08f),
        new ConduitDef("swift_banner", "Swift Banner", BaseCost: 180, SpeedPctPerTier: 0.06f),
        new ConduitDef("rampart", "Rampart", BaseCost: 220,
            SpireShieldPerTier: 400f, SlowAuraPctPerTier: 0.07f),
        new ConduitDef("breath_coil", "Armament Coil", BaseCost: 160,
            BreathRegenPctPerTier: 0.25f, TurretCadencePctPerTier: 0.08f),

        // Armaments — the Crownmount family. Sub-lethal by law: turrets soften and
        // specialize; they never out-kill units or an aimed breath.
        new ConduitDef("skyward_flak", "Skyward Flak", BaseCost: 220,
            IsArmament: true, TargetsAir: true,
            TurretRange: 14f, TurretDamagePerTier: 6, TurretCadenceTicks: 24,
            OnHitSlowPct: 0.15f, OnHitSlowTicks: 20),
        new ConduitDef("siege_mortar", "Siege Mortar", BaseCost: 240,
            IsArmament: true, TargetsGround: true,
            TurretRange: 18f, TurretRangeMin: 4f, TurretDamagePerTier: 9,
            TurretCadenceTicks: 45, TurretAoeRadius: 1.5f),
    };

    private static readonly Dictionary<string, ConduitDef> Index = All.ToDictionary(def => def.Id);

    public static ConduitDef ById(string id)
    {
        return Index[id];
    }
}
