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
            + profile.AttunementsOwned.Count
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

    /// <summary>Rank rewards in ascending order, for "next unlock" UI. Thresholds
    /// mirror LevelCap/ConduitSockets — keep all three in sync.</summary>
    public static readonly (int Rank, string Reward)[] RankMilestones =
    {
        (40, "unit level cap 6"),
        (60, "4th conduit socket"),
        (90, "unit level cap 8"),
        (150, "unit level cap 10"),
        (180, "5th conduit socket"),
        (220, "unit level cap 13"),
    };

    public static (int Rank, string Reward)? NextMilestone(int rank)
    {
        foreach (var milestone in RankMilestones)
        {
            if (milestone.Rank > rank)
            {
                return milestone;
            }
        }
        return null;
    }

    /// <summary>Sigil cost to unlock a warband unit of the given rarity — a balance-inert
    /// acquisition schedule (rarity gates collection effort, never power). Common is free
    /// (base unlock). Draconic dragons are NOT bought here (egg/bond acquisition) — callers
    /// route them through the dragon-unlock path. Numbers provisional.</summary>
    public static int SigilUnlockCost(DraconicWars.Sim.Units.Rarity rarity)
    {
        return rarity switch
        {
            DraconicWars.Sim.Units.Rarity.Common => 0,
            DraconicWars.Sim.Units.Rarity.Uncommon => 1,
            DraconicWars.Sim.Units.Rarity.Rare => 3,
            DraconicWars.Sim.Units.Rarity.Epic => 8,
            DraconicWars.Sim.Units.Rarity.Mythic => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
        };
    }

    /// <summary>Max Mythic units fielded in one loadout — apex scarcity (mirrors the
    /// 1-dragon rule). See roster-expansion-40.md §2.</summary>
    public const int MaxMythicPerLoadout = 1;

    /// <summary>True if the loadout respects the Mythic cap (at most one Mythic equipped).</summary>
    public static bool LoadoutMythicCapOk(
        System.Collections.Generic.IEnumerable<DraconicWars.Sim.Units.UnitDef> loadout)
    {
        var mythics = 0;
        foreach (var unit in loadout)
        {
            if (unit.Rarity == DraconicWars.Sim.Units.Rarity.Mythic)
            {
                mythics++;
            }
        }
        return mythics <= MaxMythicPerLoadout;
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

    public static string AttunementKey(string unitId, DraconicWars.Sim.Units.Element element)
    {
        return $"{unitId}:{element}";
    }

    /// <summary>Gold cost of unlocking one Rebreathing element for a unit —
    /// 2x the unit's first level-up on the shared table (100/400/3200 by tier).</summary>
    public static int AttunementCost(int unitTier)
    {
        return 2 * CostForLevel(EntryLevel(unitTier) + 1);
    }

    public static bool TryBuyAttunement(
        PlayerProfile profile,
        string unitId,
        DraconicWars.Sim.Units.Element element,
        int unitTier,
        DraconicWars.Sim.Units.Element nativeElement)
    {
        var key = AttunementKey(unitId, element);
        if (element == nativeElement
            || unitTier >= 4
            || profile.AttunementsOwned.Contains(key)
            || !profile.UnitLevels.ContainsKey(unitId))
        {
            return false;
        }
        var cost = AttunementCost(unitTier);
        if (profile.Gold < cost)
        {
            return false;
        }
        profile.Gold -= cost;
        profile.AttunementsOwned.Add(key);
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
