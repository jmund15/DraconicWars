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

    /// <summary>While &gt;0, this side cannot channel mana into Dragon summoning — its
    /// escrow is stalled (the_tithe's economy attack). Ticks down each tick.</summary>
    public int EscrowStallTicks { get; set; }

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

    /// <summary>Rebreathing: companies re-sworn this battle (unit id → new element).
    /// A company re-swears ONCE per duel; future deploys spawn attuned.</summary>
    public Dictionary<string, Element> AttunedThisBattle { get; } = new();

    /// <summary>Per-armament fire cooldowns. Armaments are ordinary conduits — they
    /// stack freely; the tax is sockets spent and ascension-dead machine kills.</summary>
    public Dictionary<string, int> TurretCooldowns { get; } = new();

    /// <summary>Prism Parley grants: re-swears that cost no mana.</summary>
    public int FreeAttunements { get; set; }

    /// <summary>Paid re-swears this battle — each raises the next one's price.</summary>
    public int PaidRebreaths { get; set; }

    /// <summary>Utility sockets bought mid-battle (Tier-3-gated, once per battle).</summary>
    public int BonusSockets { get; set; }

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
