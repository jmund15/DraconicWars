namespace DraconicWars.Sim.Units;

/// <summary>
/// Collection rarity — decoupled from battle <see cref="UnitDef.Tier"/>. Drives
/// units-per-form, behaviour uniqueness, Sigil-unlock cost, and visual flair; NEVER raw
/// power or mana (Tier owns the gold-entry offset and the Ascension gate; all units still
/// converge to identical max stats). Ascending warband tiers Common..Mythic, then Draconic
/// for dragons (egg/bond acquisition, not the Sigil schedule). See roster-expansion-40.md §1.
/// </summary>
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Mythic,
    Draconic,
}
