namespace DraconicWars.Meta;

using System.Collections.Generic;
using DraconicWars.Sim.Units;

/// <summary>
/// Local PvP fairness rules (design-meta §10): both sides field the SAME roster at
/// the SAME clamped level — profiles, trophies, and unlock libraries never tilt a
/// couch match. The shared pre-rolled pact tier path and published edicts are already
/// symmetric by construction.
/// </summary>
public static class WarStandard
{
    public const int ClampLevel = 9;

    /// <summary>Catalog defs scaled to the clamp (dragons stay base: entry level 9).</summary>
    public static IReadOnlyList<UnitDef> BuildPvpDefs(IReadOnlyList<UnitDef> catalog)
    {
        var defs = new List<UnitDef>(catalog.Count);
        foreach (var def in catalog)
        {
            var multiplier = MetaProgression.StatMultiplier(ClampLevel, def.Tier);
            defs.Add(def with
            {
                MaxHp = (int)System.MathF.Round(def.MaxHp * multiplier),
                Damage = (int)System.MathF.Round(def.Damage * multiplier),
            });
        }
        return defs;
    }
}
