namespace DraconicWars.Sim.Edicts;

using System.Collections.Generic;
using DraconicWars.Sim.Units;

/// <summary>V1 edict pool. Thresholds provisional, harness-tuned. Lore voice:
/// the Court phrases every trial as a standing law being tested.</summary>
public static class EdictCatalog
{
    public static readonly IReadOnlyList<EdictDef> All = new[]
    {
        new EdictDef("edict_loosed_sky", "Edict of the Loosed Sky",
            EdictKind.ElementManaDeployed, 400f,
            Lore: "Let the sky muster four hundred measures of war.",
            RequiredElement: Element.Storm),
        new EdictDef("edict_first_flame", "Edict of the First Flame",
            EdictKind.ElementManaDeployed, 400f,
            Lore: "Let hunger be fed four hundred measures.",
            RequiredElement: Element.Fire),
        new EdictDef("edict_patient_bone", "Edict of the Patient Bone",
            EdictKind.ElementManaDeployed, 400f,
            Lore: "Let the mountain walk, four hundred measures of it.",
            RequiredElement: Element.Stone),
        new EdictDef("edict_deep_coffer", "Edict of the Deep Coffer",
            EdictKind.BankedMana, 450f,
            Lore: "Wealth withheld is also a weapon. Prove it."),
        new EdictDef("edict_champion", "Edict of the Champion",
            EdictKind.SingleDeployCost, 240f,
            Lore: "Send one worth naming."),
        new EdictDef("edict_mason", "Edict of the Mason",
            EdictKind.ConduitGrafts, 3f,
            Lore: "Three grafts upon the spire's living stone."),
        new EdictDef("edict_red_ledger", "Edict of the Red Ledger",
            EdictKind.Kills, 12f,
            Lore: "The Court counts twelve entries, paid in full."),
        new EdictDef("edict_long_burn", "Edict of the Long Burn",
            EdictKind.BreathPulses, 20f,
            Lore: "Twenty tongues of breath, each one tasting blood."),
    };
}
