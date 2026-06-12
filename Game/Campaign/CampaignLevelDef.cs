namespace DraconicWars.Game.Campaign;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;

/// <summary>
/// One campaign level: battle config (compressed arcs for teaching levels), enemy
/// roster + magnification (the content-reuse lever), authored wave script, rewards.
/// </summary>
public sealed record CampaignLevelDef(
    string Id,
    string DisplayName,
    BattleConfig Config,
    IReadOnlyList<string> EnemyUnitIds,
    int MagnificationPct,
    IReadOnlyList<WaveEntry> Waves,
    IReadOnlyList<RepeatingWave> RepeatingWaves,
    int BaseGoldReward,
    string? BondedDragonId = null,
    string Blurb = "",
    string? UnlockConduitId = null,
    AiPersona? Persona = null)
{
    public const string EnemyIdPrefix = "enemy:";

    /// <summary>Magnification scales HP and damage ONLY (speed/range/abilities stay).</summary>
    public static UnitDef Magnify(UnitDef def, int magnificationPct, string idPrefix)
    {
        return def with
        {
            Id = idPrefix + def.Id,
            MaxHp = (int)((long)def.MaxHp * magnificationPct / 100),
            Damage = (int)((long)def.Damage * magnificationPct / 100),
        };
    }
}
