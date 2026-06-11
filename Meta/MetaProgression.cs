namespace DraconicWars.Meta;

using System;

/// <summary>
/// Meta-progression math per design-meta.md §2-3. One shared cost table for all units;
/// rarity is an entry offset, never a different ceiling. Numbers provisional pending
/// the economy spreadsheet pass.
/// </summary>
public static class MetaProgression
{
    public const int MaxLevel = 13;
    private const int BaseCost = 50;
    private const float CostRatio = 2f;
    private const float StatGrowthPerLevel = 1.1f;

    /// <summary>Gold cost to purchase the given level (valid for levels 2..13).</summary>
    public static int CostForLevel(int level)
    {
        if (level is < 2 or > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }
        return (int)(BaseCost * MathF.Pow(CostRatio, level - 2));
    }

    /// <summary>Entry level per unit tier (Tier I common → L1 ... Dragon → L9).</summary>
    public static int EntryLevel(int tier)
    {
        return tier switch
        {
            1 => 1,
            2 => 3,
            3 => 6,
            4 => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };
    }

    public static float StatMultiplier(int level, int tier)
    {
        return MathF.Pow(StatGrowthPerLevel, level - EntryLevel(tier));
    }

    /// <summary>Every progress source double-counts into the account meter.</summary>
    public static int DragonRank(PlayerProfile profile)
    {
        return profile.LevelsPurchased
            + profile.UnitLevels.Count
            + profile.CampaignFirstClears * 2
            + profile.HeatRungClears
            + profile.AchievementsEarned;
    }

    public static int LevelCap(int rank)
    {
        return rank switch
        {
            >= 220 => 13,
            >= 150 => 10,
            >= 90 => 8,
            >= 40 => 6,
            _ => 4,
        };
    }

    public static int ConduitSockets(int rank)
    {
        return rank switch
        {
            >= 180 => 5,
            >= 60 => 4,
            _ => 3,
        };
    }

    public static bool TryBuyLevel(PlayerProfile profile, string unitId)
    {
        if (!profile.UnitLevels.TryGetValue(unitId, out var level))
        {
            return false;
        }
        var nextLevel = level + 1;
        var cap = Math.Min(MaxLevel, LevelCap(DragonRank(profile)));
        if (nextLevel > cap)
        {
            return false;
        }
        var cost = CostForLevel(nextLevel);
        if (profile.Gold < cost)
        {
            return false;
        }

        profile.Gold -= cost;
        profile.GoldSpentOnLevels += cost;
        profile.UnitLevels[unitId] = nextLevel;
        profile.LevelsPurchased++;
        return true;
    }

    /// <summary>
    /// Free 100% respec: refunds all level gold, resets units to entry level. Dragon
    /// Rank is monotone — purchases already counted stay counted.
    /// </summary>
    public static void Respec(PlayerProfile profile, Func<string, int> tierForUnit)
    {
        profile.Gold += profile.GoldSpentOnLevels;
        profile.GoldSpentOnLevels = 0;
        foreach (var unitId in System.Linq.Enumerable.ToArray(profile.UnitLevels.Keys))
        {
            profile.UnitLevels[unitId] = EntryLevel(tierForUnit(unitId));
        }
    }
}
