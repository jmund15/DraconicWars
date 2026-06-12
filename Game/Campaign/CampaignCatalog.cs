namespace DraconicWars.Game.Campaign;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;

/// <summary>
/// First-playable campaign: 5 levels of the Cinderfell Marches (design-meta.md §9).
/// L1-L4 run compressed teaching arcs; L5 debuts the full 10-minute battle.
/// </summary>
public static class CampaignCatalog
{
    private static BattleConfig Compressed(
        int crescendoSec, int suddenDeathSec, int hardEndSec,
        int edictsPerTier, params int[] parleyTiers)
    {
        return BattleConfig.Default with
        {
            SpireMaxHp = 2500f,
            CrescendoStartTick = crescendoSec * 30,
            SuddenDeathStartTick = suddenDeathSec * 30,
            HardEndTick = hardEndSec * 30,
            LastStandEnabled = true,
            AscensionThresholds = new[] { 42f, 110f, 240f },
            EdictsPerTier = edictsPerTier,
            ParleyTiers = parleyTiers,
        };
    }

    public static readonly IReadOnlyList<CampaignLevelDef> Levels = new[]
    {
        new CampaignLevelDef(
            Id: "cm_01", DisplayName: "First Sparks",
            Config: Compressed(150, 180, 240, edictsPerTier: 0 /* no parleys, no edicts: teaching */),
            EnemyUnitIds: new[] { "kobold_spearman", "frost_whelp" },
            MagnificationPct: 100,
            Waves: new[]
            {
                new WaveEntry(2700, "enemy:frost_whelp"),
                new WaveEntry(2790, "enemy:frost_whelp"),
                new WaveEntry(2880, "enemy:frost_whelp"),
            },
            RepeatingWaves: new[]
            {
                new RepeatingWave(90, 240, "enemy:kobold_spearman", 5400),
            },
            BaseGoldReward: 80,
            UnlockConduitId: "aurum_vault",
            Blurb: "The eastern span went quiet a year ago. Vask's kindle-spears are why. "
                + "Walk the lane, raise a warband, and remind the Marches who holds this spire."),

        new CampaignLevelDef(
            Id: "cm_02", DisplayName: "The Tide Teaches",
            Config: Compressed(150, 180, 240, edictsPerTier: 0, 2 /* first Broker visit at Tier II */),
            EnemyUnitIds: new[] { "kobold_spearman", "forest_archer" },
            MagnificationPct: 100,
            Waves: Enumerable.Range(0, 6)
                .Select(i => new WaveEntry(1800 + i * 20, "enemy:kobold_spearman"))
                .Append(new WaveEntry(1860, "enemy:forest_archer"))
                .Append(new WaveEntry(1900, "enemy:forest_archer"))
                .ToArray(),
            RepeatingWaves: new[]
            {
                new RepeatingWave(120, 300, "enemy:kobold_spearman", 5400),
            },
            BaseGoldReward: 90,
            UnlockConduitId: "breath_coil",
            Blurb: "A Marches proverb: the tide teaches drowning. Vask sends whelps over "
                + "the chaff line — keep your dragon's breath aimed at the sky."),

        new CampaignLevelDef(
            Id: "cm_03", DisplayName: "Augury",
            Config: Compressed(160, 200, 260, edictsPerTier: 2, 2, 3),
            EnemyUnitIds: new[] { "kobold_spearman", "forest_archer", "dune_marksman" },
            MagnificationPct: 100,
            Waves: new[] { new WaveEntry(3000, "enemy:forest_archer") },
            RepeatingWaves: new[]
            {
                new RepeatingWave(90, 220, "enemy:kobold_spearman", 6600),
                new RepeatingWave(600, 480, "enemy:forest_archer", 6600),
                new RepeatingWave(2400, 600, "enemy:dune_marksman", 6600),
            },
            BaseGoldReward: 100,
            UnlockConduitId: "swift_banner",
            Persona: AiPersona.Rusher,
            Blurb: "The Broker attends your duels now. The Court is watching, which means "
                + "the Court is wagering. Seal your first pact wisely."),

        new CampaignLevelDef(
            Id: "cm_04", DisplayName: "The Refit",
            Config: Compressed(170, 210, 280, edictsPerTier: 2, 2, 3),
            EnemyUnitIds: new[] { "kobold_spearman", "forest_archer", "frost_whelp", "storm_monk" },
            MagnificationPct: 120,
            Waves: new[] { new WaveEntry(2400, "enemy:frost_whelp"), new WaveEntry(2460, "enemy:frost_whelp") },
            RepeatingWaves: new[]
            {
                new RepeatingWave(90, 260, "enemy:forest_archer", 7200),
                new RepeatingWave(400, 300, "enemy:kobold_spearman", 7200),
                new RepeatingWave(2000, 480, "enemy:storm_monk", 7200),
            },
            BaseGoldReward: 110,
            UnlockConduitId: "skyward_flak",
            Persona: AiPersona.Streamer,
            Blurb: "Vask's columns answer whatever wall you raise. Sell what stands, graft "
                + "what's needed — a spire's strength is what it builds next."),

        new CampaignLevelDef(
            Id: "cm_05", DisplayName: "The Marcher Lord",
            Config: BattleConfig.Default with { LastStandEnabled = true },
            EnemyUnitIds: new[]
            {
                "kobold_spearman", "forest_archer", "frost_whelp",
                "storm_monk", "stone_ram", "storm_gryphon",
            },
            MagnificationPct: 150,
            Waves: new[]
            {
                new WaveEntry(9000, "enemy:kobold_spearman"),
                new WaveEntry(9020, "enemy:kobold_spearman"),
                new WaveEntry(9040, "enemy:kobold_spearman"),
                new WaveEntry(9060, "enemy:forest_archer"),
                new WaveEntry(9080, "enemy:forest_archer"),
                new WaveEntry(9100, "enemy:frost_whelp"),
                new WaveEntry(9120, "enemy:frost_whelp"),
                new WaveEntry(11000, "enemy:stone_ram"),
                new WaveEntry(11500, "enemy:storm_gryphon"),
                new WaveEntry(12200, "enemy:stone_ram"),
            },
            RepeatingWaves: new[]
            {
                new RepeatingWave(120, 240, "enemy:kobold_spearman", 18000),
                new RepeatingWave(700, 420, "enemy:forest_archer", 18000),
                new RepeatingWave(1500, 600, "enemy:frost_whelp", 18000),
                new RepeatingWave(4500, 540, "enemy:storm_monk", 18000),
            },
            BaseGoldReward: 150,
            BondedDragonId: "pyraxis",
            UnlockConduitId: "siege_mortar",
            Persona: AiPersona.Powerhouse,
            Blurb: "Vask broke Court law: Pyraxis fights in chains at his spire. Break the "
                + "chains and the Marchfire chooses his own lord. Beat the dragon, bond the dragon."),
    };

    /// <summary>
    /// Player defs (scaled by profile unit levels) + the level's magnified, id-prefixed
    /// enemy defs (always base stats — the mirrored AI plays at War Standard).
    /// </summary>
    public static IReadOnlyList<UnitDef> BuildBattleDefs(
        CampaignLevelDef level, DraconicWars.Meta.PlayerProfile? profile = null)
    {
        var defs = new List<UnitDef>();
        foreach (var def in UnitCatalog.FirstPlayable)
        {
            defs.Add(profile is not null && profile.UnitLevels.TryGetValue(def.Id, out var unitLevel)
                ? ApplyLevel(def, unitLevel)
                : def);
        }
        foreach (var enemyId in level.EnemyUnitIds)
        {
            var baseDef = UnitCatalog.FirstPlayable.First(def => def.Id == enemyId);
            defs.Add(CampaignLevelDef.Magnify(
                baseDef, level.MagnificationPct, CampaignLevelDef.EnemyIdPrefix));
        }
        return defs;
    }

    private static UnitDef ApplyLevel(UnitDef def, int unitLevel)
    {
        var multiplier = DraconicWars.Meta.MetaProgression.StatMultiplier(unitLevel, def.Tier);
        return def with
        {
            MaxHp = (int)System.MathF.Round(def.MaxHp * multiplier),
            Damage = (int)System.MathF.Round(def.Damage * multiplier),
        };
    }
}
