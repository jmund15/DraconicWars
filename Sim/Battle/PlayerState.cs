namespace DraconicWars.Sim.Battle;

using System.Collections.Generic;

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

    public float EffectiveWalletCap => WalletCap + Buffs.WalletCapBonus;
}
