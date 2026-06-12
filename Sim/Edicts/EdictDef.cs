namespace DraconicWars.Sim.Edicts;

using DraconicWars.Sim.Units;

/// <summary>Sim-measurable trial shapes. Each reads one running per-player counter.</summary>
public enum EdictKind
{
    ElementManaDeployed,
    SingleDeployCost,
    ConduitGrafts,
    Kills,
    BreathPulses,
    BankedMana,
}

/// <summary>
/// One of the Court's trials (design.md §8 — Edicts of Ascent). Rolled per battle
/// from the published pool, the SAME set for both sides: first Dragonlord to satisfy
/// an edict claims its full ascension surge; the runner-up still collects half.
/// RequiredElement edicts only enter play when both rosters can field that element.
/// </summary>
public sealed record EdictDef(
    string Id,
    string DisplayName,
    EdictKind Kind,
    float Threshold,
    string Lore = "",
    Element? RequiredElement = null);
