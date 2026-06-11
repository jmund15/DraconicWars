namespace DraconicWars.Sim.Battle;

/// <summary>
/// Battle-wide tuning values. Numbers are v1 provisional pending the economy-coherence
/// spreadsheet pass (design.md §13a).
/// </summary>
public sealed record BattleConfig(
    int TickRate,
    float LaneLength,
    float BaseDripPerSecond,
    float StartingMana,
    float StartingWalletCap,
    float SpireMaxHp,
    float DeploySpawnOffset)
{
    public static readonly BattleConfig Default = new(
        TickRate: 30,
        LaneLength: 38f,
        BaseDripPerSecond: 12f,
        StartingMana: 60f,
        StartingWalletCap: 300f,
        SpireMaxHp: 4000f,
        DeploySpawnOffset: 1.5f);

    public float DripPerTick => BaseDripPerSecond / TickRate;
}
