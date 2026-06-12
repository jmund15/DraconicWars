namespace DraconicWars.Sim.Battle;

/// <summary>Aggregated per-player conduit effects, recomputed on any conduit change.</summary>
public sealed class PlayerBuffs
{
    public float DripBonusPerSecond { get; init; }

    public float WalletCapBonus { get; init; }

    public float KillBountyPct { get; init; }

    public float DamagePct { get; init; }

    public float SpeedPct { get; init; }

    public float SlowAuraPct { get; init; }

    public float BreathRegenPct { get; init; }

    public float BreathDamagePct { get; init; }

    public float DeployCooldownPct { get; init; }

    public float AscensionTricklePct { get; init; }

    public float SummonCostPct { get; init; }

    public float WrathCooldownPct { get; init; }

    /// <summary>Ongoing Wyrm-pact mana tithe; income floors at DripFloorPerSecond.</summary>
    public float DripPricePerSecond { get; init; }

    /// <summary>Armament Coil: fraction shaved off the mounted turret's cadence.</summary>
    public float TurretCadencePct { get; init; }

    /// <summary>Salvage Charter: added to the 50% base conduit sell refund.</summary>
    public float ConduitRefundBonusPct { get; init; }

    public static readonly PlayerBuffs None = new();
}
