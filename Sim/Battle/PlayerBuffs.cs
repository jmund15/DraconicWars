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

    public static readonly PlayerBuffs None = new();
}
