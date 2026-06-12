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
    /// <summary>Cumulative meter required to REACH tiers 2, 3, and Dragon (4).</summary>
    public static readonly float[] DefaultAscensionThresholds = { 100f, 250f, 450f };

    public IReadOnlyList<float> AscensionThresholds { get; init; } = DefaultAscensionThresholds;

    /// <summary>Draft window timestamps in sim ticks; per-level authorable (design.md §9).</summary>
    public static readonly int[] DefaultParleyTicks = { 2 * 60 * 30, 5 * 60 * 30, 8 * 60 * 30 };

    public IReadOnlyList<int> ParleyTicks { get; init; } = DefaultParleyTicks;

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
        SummoningCost: 1800f,
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
