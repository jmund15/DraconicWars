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
            parts.Add("machine kills feed no Ascension");
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
        AddPct(parts, def.ConduitRefundBonusPct, "+{0}% conduit refund");
        AddPct(parts, def.TurretCadencePct, "-{0}% armament cadence");
        if (def.FreeAttunements > 0)
        {
            parts.Add($"{def.FreeAttunements} free re-swears");
        }
        return string.Join(", ", parts);
    }

    /// <summary>The trial's requirement in plain mechanics — what the lore line is
    /// actually asking the player to DO (playtest: lore alone was undigestible
    /// mid-battle).</summary>
    public static string ForEdict(Sim.Edicts.EdictDef def)
    {
        return def.Kind switch
        {
            Sim.Edicts.EdictKind.ElementManaDeployed =>
                $"Deploy {def.Threshold:0} mana of {def.RequiredElement} units",
            Sim.Edicts.EdictKind.SingleDeployCost =>
                $"Deploy one unit costing {def.Threshold:0}+ mana",
            Sim.Edicts.EdictKind.ConduitGrafts =>
                $"Build {def.Threshold:0} conduits this battle",
            Sim.Edicts.EdictKind.Kills =>
                $"Slay {def.Threshold:0} enemy units",
            Sim.Edicts.EdictKind.BreathPulses =>
                $"Land {def.Threshold:0} breath pulses",
            Sim.Edicts.EdictKind.BankedMana =>
                $"Hold {def.Threshold:0} unspent mana at once",
            _ => string.Empty,
        };
    }

    /// <summary>The unit's signature kit in one line; empty for kit-less units.</summary>
    public static string ForSignature(Sim.Units.UnitDef def)
    {
        var parts = new List<string>(2);
        if (def.PrefersFarthestTarget)
        {
            parts.Add("lobs shots over the line — strikes the FARTHEST enemy in range");
        }
        if (def.ReviveHpPct > 0f)
        {
            parts.Add($"rises once from death at {def.ReviveHpPct * 100:0}% HP");
        }
        if (def.OnDeathBlastDamage > 0)
        {
            parts.Add($"detonates on death: {def.OnDeathBlastDamage} dmg"
                + $" within {def.OnDeathBlastRadius:0.#}m");
        }
        if (def.AuraDamagePerTick > 0)
        {
            parts.Add($"burns everything within {def.AuraRadius:0.#}m"
                + $" for {def.AuraDamagePerTick * 30}/s");
        }
        if (def.VigilDrMaxPct > 0f)
        {
            parts.Add($"holding post hardens him (up to -{def.VigilDrMaxPct * 100:0}%"
                + " damage taken); knockback breaks the watch");
        }
        if (def.ZoneRadius > 0f)
        {
            parts.Add($"attacks frost the ground: {def.ZoneSlowPct * 100:0}% slow zone"
                + $" for {def.ZoneDurationTicks / 30f:0.#}s");
        }
        if (def.ShoveDistance > 0f)
        {
            parts.Add($"each blow shoves all enemies within {def.ShoveRadius:0.#}m"
                + $" back {def.ShoveDistance:0.#}m");
        }
        if (def.LifestealPct > 0f)
        {
            parts.Add($"collects {def.LifestealPct * 100:0}% of damage dealt as HP");
        }
        if (def.TollRampPct > 0f)
        {
            parts.Add($"each toll rings {def.TollRampPct * 100:0}% harder"
                + $" (cap +{def.TollRampCap * 100:0}%); knockback resets the toll");
        }
        if (def.BonusVsHighHpPct > 0f)
        {
            parts.Add($"+{def.BonusVsHighHpPct * 100:0}% damage"
                + $" vs {def.HighHpThreshold}+ HP targets");
        }
        if (def.Unstaggerable)
        {
            parts.Add("never staggered — only a dragonlord's Wrath can move it");
        }
        if (def.FirstStrikeBonusPct > 0f)
        {
            parts.Add($"first strike lands instantly at +{def.FirstStrikeBonusPct * 100:0}% damage");
        }
        if (def.StrafeDistance > 0f)
        {
            parts.Add($"swoops {def.StrafeDistance:0.#}m past her mark after every attack");
        }
        return string.Join("; ", parts);
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
