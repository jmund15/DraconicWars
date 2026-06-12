namespace DraconicWars.Sim.Battle;

using System.Collections.Generic;
using DraconicWars.Sim.Units;

public sealed class PlayerState
{
    public float Mana { get; set; }

    public float WalletCap { get; set; }

    public Dictionary<string, int> DeployCooldowns { get; } = new();

    /// <summary>Built conduits: conduit id → current tier (1-3). One socket per type.</summary>
    public Dictionary<string, int> Conduits { get; } = new();

    /// <summary>Total mana spent per conduit, for the 50% sell refund.</summary>
    public Dictionary<string, int> ConduitSpent { get; } = new();

    public PlayerBuffs Buffs { get; set; } = PlayerBuffs.None;

    public float SpireShield { get; set; }

    public bool LastStandUsed { get; set; }

    public float AscensionMeter { get; set; }

    public int AscensionTier { get; set; } = 1;

    /// <summary>Kill-sourced meter within the current threshold segment (capped at 30%).</summary>
    public float KillMeterThisSegment { get; set; }

    public string? EquippedDragonId { get; set; }

    public float SummoningProgress { get; set; }

    public float BreathEnergySeconds { get; set; }

    public int BreathPulseCounter { get; set; }

    public int WrathCooldownTicks { get; set; }

    /// <summary>A parley is open: offers on the table, gameplay flowing. UI flag only —
    /// the sim never freezes for it.</summary>
    public bool AwaitingParley { get; set; }

    public List<string> PendingOffers { get; } = new();

    public List<string> SealedPacts { get; } = new();

    /// <summary>Tithes paid to the Broker during the current parley (resets per parley).</summary>
    public int TithesPaidThisParley { get; set; }

    /// <summary>Parleys this side has opened — its index into the shared tier path.</summary>
    public int ParleysOpened { get; set; }

    /// <summary>Tier-ups that earned a parley while one was already open.</summary>
    public int PendingParleys { get; set; }

    /// <summary>Tick at which the open parley auto-seals its first offer.</summary>
    public int ParleyDeadlineTick { get; set; }

    // Edict counters (cumulative; EdictProgress reads them).

    public Dictionary<Element, float> ManaDeployedByElement { get; } = new();

    public float MaxSingleDeployCost { get; set; }

    public int ConduitGrafts { get; set; }

    public int Kills { get; set; }

    public int BreathPulsesLanded { get; set; }

    // Ascension provenance, for telemetry-driven "earned vs passive" tuning.

    public float AscensionFromTrickle { get; set; }

    public float AscensionFromKills { get; set; }

    public float AscensionFromLane { get; set; }

    public float AscensionFromChip { get; set; }

    public float AscensionFromEdicts { get; set; }

    public float EffectiveWalletCap => WalletCap + Buffs.WalletCapBonus;
}
