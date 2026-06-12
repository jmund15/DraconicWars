namespace DraconicWars.Sim.Battle;

using System.Collections.Generic;

/// <summary>
/// Battle-wide tuning values. Numbers are v1 provisional pending the economy-coherence
/// spreadsheet pass (design.md §13a). Timeline ticks are config-driven so compressed
/// teaching levels and Heat modifiers can reshape the arc per battle.
/// </summary>
public sealed record BattleConfig(
    int TickRate,
    float LaneLength,
    float BaseDripPerSecond,
    float StartingMana,
    float StartingWalletCap,
    float SpireMaxHp,
    float DeploySpawnOffset,
    int CrescendoStartTick,
    int SuddenDeathStartTick,
    int HardEndTick,
    int SuddenDeathEscalationTicks,
    float CrescendoDripMultiplier,
    float SuddenDeathEscalationFactor,
    float SuddenDeathDecayPerSecond,
    float KnockbackDistance,
    int KnockbackIFrameTicks,
    int ConduitSockets,
    float SlowAuraRange,
    bool LastStandEnabled,
    float LastStandDripBonus,
    float AscensionTricklePerSecond,
    float LaneControlBonusPerSecond,
    float TierBehindTrickleMultiplier,
    float KillAscensionCapPct,
    float KillAscensionPerTier,
    float ChipDamageAscensionRate,
    float AscensionDripEscalation,
    float SummoningCost,
    float BreathMaxSeconds,
    int BreathRechargeSeconds,
    int BreathPulseTicks,
    int BreathPulseDamage,
    float BreathRadius,
    int WrathCooldownTicks,
    int WrathDamage,
    float WrathKnockbackDistance,
    float TitheCostMana,
    float DripFloorPerSecond,
    int MaxFieldedPerSide)
{
    /// <summary>Cumulative meter required to REACH tiers 2, 3, and Dragon (4).
    /// Playtest: human battles ended before anyone summoned — tier 4 pulled in.</summary>
    public static readonly float[] DefaultAscensionThresholds = { 100f, 240f, 400f };

    public IReadOnlyList<float> AscensionThresholds { get; init; } = DefaultAscensionThresholds;

    /// <summary>Ascension tiers whose attainment summons the Broker (design.md §9 —
    /// parleys are EARNED by tiering up, not granted by the clock). Per-level authorable;
    /// empty disables parleys.</summary>
    public static readonly int[] DefaultParleyTiers = { 2, 3, 4 };

    public IReadOnlyList<int> ParleyTiers { get; init; } = DefaultParleyTiers;

    /// <summary>Ticks a parley stays open before the sim deterministically seals the
    /// first offer (gameplay continues throughout — replay-safe, PvP-fair).</summary>
    public int ParleyPickTicks { get; init; } = 450;

    /// <summary>Edicts of Ascent rolled per tier segment (0 disables the trials).</summary>
    public int EdictsPerTier { get; init; } = 2;

    /// <summary>First claimant's surge as a fraction of the segment's threshold gap.
    /// Playtest: 0.3 let edicts dominate tier pacing — halved.</summary>
    public float EdictSurgePct { get; init; } = 0.15f;

    /// <summary>Runner-up's share of the surge (anti-snowball: the race stays live).</summary>
    public float EdictRunnerUpPct { get; init; } = 0.5f;

    /// <summary>Rebreathing cost as a multiple of the unit's deploy cost.</summary>
    public float RebreathCostFactor { get; init; } = 1.5f;

    /// <summary>Each PAID re-swear raises the next one's price by this fraction of the
    /// base — element pivots stay possible but can't be habitual. Free (Prism) re-swears
    /// don't escalate.</summary>
    public float RebreathCostStepPct { get; init; } = 0.5f;

    /// <summary>Machine kills' bounty share (tier credit is ALWAYS denied to
    /// armaments; this knob exists for harness A/Bs at 0.5/0 if tier-denial alone
    /// under-deters turtling).</summary>
    public float ArmamentKillBountyPct { get; init; } = 1.0f;

    /// <summary>Mid-battle 4th utility socket: a late-game mana sink, once per battle.</summary>
    public float SocketPurchaseCost { get; init; } = 700f;

    public int SocketPurchaseTierGate { get; init; } = 3;

    public static readonly BattleConfig Default = new(
        TickRate: 30,
        LaneLength: 38f,
        BaseDripPerSecond: 12f,
        StartingMana: 60f,
        StartingWalletCap: 600f,
        SpireMaxHp: 7000f,
        DeploySpawnOffset: 1.5f,
        CrescendoStartTick: 8 * 60 * 30,
        SuddenDeathStartTick: 10 * 60 * 30,
        HardEndTick: 12 * 60 * 30,
        SuddenDeathEscalationTicks: 30 * 30,
        CrescendoDripMultiplier: 2f,
        SuddenDeathEscalationFactor: 1.25f,
        SuddenDeathDecayPerSecond: 10f,
        KnockbackDistance: 1.5f,
        KnockbackIFrameTicks: 8,
        ConduitSockets: 3,
        SlowAuraRange: 6f,
        LastStandEnabled: false,
        LastStandDripBonus: 12f,
        AscensionTricklePerSecond: 1.0f,
        LaneControlBonusPerSecond: 0.3f,
        TierBehindTrickleMultiplier: 1.5f,
        KillAscensionCapPct: 0.3f,
        KillAscensionPerTier: 2f,
        ChipDamageAscensionRate: 0.01f,
        AscensionDripEscalation: 1.25f,
        SummoningCost: 1100f,
        BreathMaxSeconds: 4f,
        BreathRechargeSeconds: 12,
        BreathPulseTicks: 6,
        BreathPulseDamage: 5,
        BreathRadius: 1.5f,
        WrathCooldownTicks: 60 * 30,
        WrathDamage: 30,
        WrathKnockbackDistance: 3f,
        TitheCostMana: 60f,
        DripFloorPerSecond: 2f,
        MaxFieldedPerSide: 12);

    public float DripPerTick => BaseDripPerSecond / TickRate;
}
