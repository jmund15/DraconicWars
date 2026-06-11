namespace DraconicWars.Game.Campaign;

using DraconicWars.Meta;

/// <summary>
/// Applies battle results to the profile and answers unlock queries. Pillar P4:
/// level N+1 unlocks on clearing level N — never on grind.
/// </summary>
public static class CampaignProgress
{
    /// <summary>Returns true if this was the level's first clear.</summary>
    public static bool ApplyBattleResult(
        PlayerProfile profile,
        CampaignLevelDef level,
        bool won,
        int battleTicks,
        float enemySpireDamagePct)
    {
        profile.Gold += BattleRewards.ComputeGold(
            level.BaseGoldReward, won, battleTicks, level.Config.HardEndTick, enemySpireDamagePct);

        if (!won || profile.ClearedLevelIds.Contains(level.Id))
        {
            return false;
        }

        profile.ClearedLevelIds.Add(level.Id);
        profile.CampaignFirstClears++;
        if (level.BondedDragonId is { } dragonId && !profile.UnitLevels.ContainsKey(dragonId))
        {
            // Beat the dragon, bond the dragon (design-meta.md §9).
            profile.UnitLevels[dragonId] = MetaProgression.EntryLevel(4);
        }
        return true;
    }

    public static bool IsUnlocked(PlayerProfile profile, int levelIndex)
    {
        if (levelIndex <= 0)
        {
            return true;
        }
        if (levelIndex >= CampaignCatalog.Levels.Count)
        {
            return false;
        }
        return profile.ClearedLevelIds.Contains(CampaignCatalog.Levels[levelIndex - 1].Id);
    }
}
