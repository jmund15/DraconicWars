namespace DraconicWars.Game.Battle.Hud;

using System.Collections.Generic;
using DraconicWars.Sim.Conduits;
using DraconicWars.Sim.Pacts;

/// <summary>
/// Presentation formatters turning typed effect fields into the short strings shown
/// on tooltips and parley cards. Pure C# so the Logic suite pins that every catalog
/// entry renders a legible, non-empty description.
/// </summary>
public static class EffectText
{
    public static string ForConduit(ConduitDef def, int tier)
    {
        var parts = new List<string>(3);
        AddFlat(parts, def.DripBonusPerTier * tier, "{0} mana/s");
        AddFlat(parts, def.WalletCapPerTier * tier, "{0} cap");
        AddPct(parts, def.KillBountyPctPerTier * tier, "+{0}% bounty");
        AddPct(parts, def.DamagePctPerTier * tier, "+{0}% dmg");
        AddPct(parts, def.SpeedPctPerTier * tier, "+{0}% speed");
        AddFlat(parts, def.SpireShieldPerTier * tier, "{0} shield");
        AddPct(parts, def.SlowAuraPctPerTier * tier, "{0}% slow aura");
        AddPct(parts, def.BreathRegenPctPerTier * tier, "+{0}% breath regen");
        AddPct(parts, def.TurretCadencePctPerTier * tier, "-{0}% armament cadence");
        if (def.IsArmament && def.TurretCadenceTicks > 0)
        {
            var strata = def.TargetsAir && def.TargetsGround
                ? "air+ground"
                : def.TargetsAir ? "air" : "ground";
            parts.Add($"{def.TurretDamagePerTier * tier} dmg vs {strata}"
                + $" every {def.TurretCadenceTicks / 30f:0.#}s");
            if (def.TurretAoeRadius > 0f)
            {
                parts.Add($"{def.TurretAoeRadius:0.#}m splash");
            }
            if (def.OnHitSlowPct > 0f)
            {
                parts.Add($"slows {def.OnHitSlowPct * 100:0}%");
            }
            parts.Add($"range {def.TurretRangeMin:0}-{def.TurretRange:0}m");
            parts.Add("replaces breath while mounted");
        }
        return string.Join(", ", parts);
    }

    public static string ForPact(PactDef def)
    {
        var parts = new List<string>(4);
        AddFlat(parts, def.DripBonusPerSecond, "{0} mana/s");
        AddFlat(parts, def.WalletCapBonus, "{0} cap");
        AddPct(parts, def.KillBountyPct, "+{0}% bounty");
        AddPct(parts, def.DamagePct, "+{0}% dmg");
        AddPct(parts, def.SpeedPct, "+{0}% speed");
        AddPct(parts, def.BreathRegenPct, "+{0}% breath regen");
        AddPct(parts, def.BreathDamagePct, "+{0}% breath dmg");
        AddPct(parts, def.DeployCooldownPct, "-{0}% deploy cd");
        AddPct(parts, def.AscensionTricklePct, "+{0}% ascension");
        AddPct(parts, def.SummonCostPct, "-{0}% summon cost");
        AddPct(parts, def.WrathCooldownPct, "-{0}% wrath cd");
        return string.Join(", ", parts);
    }

    public static string ForPactPrice(PactDef def)
    {
        var parts = new List<string>(2);
        if (def.PriceSpireHpPct > 0f)
        {
            parts.Add($"{(int)(def.PriceSpireHpPct * 100)}% spire blood");
        }
        if (def.PriceDripPerSecond > 0f)
        {
            parts.Add($"-{def.PriceDripPerSecond:0.#} drip");
        }
        return string.Join(", ", parts);
    }

    private static void AddFlat(List<string> parts, float value, string format)
    {
        if (value != 0f)
        {
            parts.Add(string.Format(format, $"{(value > 0 ? "+" : "")}{value:0.#}"));
        }
    }

    private static void AddPct(List<string> parts, float value, string format)
    {
        if (value != 0f)
        {
            parts.Add(string.Format(format, $"{value * 100:0.#}"));
        }
    }
}
