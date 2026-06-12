namespace DraconicWars.Game.Content;

using System.Collections.Generic;

/// <summary>
/// Presentation-layer narrative for units (the sim never reads this). Titles and
/// flavor lines per the lore bible (vault: lore.md) — the Marches war-chronicle
/// voice: dry, specific, a little haunted.
/// </summary>
public static class UnitLore
{
    public readonly record struct Entry(string Title, string Flavor);

    private static readonly Dictionary<string, Entry> Entries = new()
    {
        ["kobold_spearman"] = new(
            "Kindle-Spear of the Ashmark",
            "First over the wall, last to be named in the songs."),
        ["forest_archer"] = new(
            "Archer of the Vale",
            "She counts her arrows by the debts they settle."),
        ["frost_whelp"] = new(
            "Whelp of the Still Word",
            "Too young to know dragons are supposed to be feared."),
        ["stone_warden"] = new(
            "Warden of the Patient Bone",
            "The shield was his grandmother's. The patience is his own."),
        ["storm_monk"] = new(
            "Monk of the Loosed Sky",
            "Struck by lightning twice. Ordained the second time."),
        ["vale_chanter"] = new(
            "Chanter of the Vale",
            "Her hymns close wounds and open graves."),
        ["dune_marksman"] = new(
            "Marksman of the Glass Sandsea",
            "Three miles of nothing taught him to make the first shot count."),
        ["stone_ram"] = new(
            "Ram of the Quarried Oath",
            "Cinderfell oak, dwarvish iron, and a standing grudge against gates."),
        ["storm_gryphon"] = new(
            "Gryphon of the High Reaches",
            "Hunts in the half-second between thunder and light."),
        ["elder_drake"] = new(
            "Elder Drake — Court Rental",
            "On loan from the Wyrmcourt. Do read the damage clauses."),
        ["pyraxis"] = new(
            "Pyraxis, the Marchfire",
            "He remembers being worshipped. He prefers being feared. He is learning to be free."),
    };

    public static Entry For(string unitId)
    {
        return Entries.GetValueOrDefault(unitId, new Entry(string.Empty, string.Empty));
    }
}
