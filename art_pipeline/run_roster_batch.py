"""run_roster_batch.py -- the v1 roster expansion batch (13 units).

Unit ticks come from the game's unit catalog and are BINDING (the Godot
importer rejects manifest disagreement). Same regeneration policy as
run_fp_batch.py: "always" for new art, "ticks" once a sheet is accepted.

Pipeline per regenerated unit: generate -> lint -> contact sheets (1x + 3x).
Finishes with a roster silhouette strip over the 13 expansion units.

CLI:
    python art_pipeline/run_roster_batch.py [--only NAME]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

import backgrounds
import contact_sheet
import generate_unit
import lint

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "output"
UNITS_DIR = OUT / "units"
REPORTS_DIR = OUT / "reports"
SHEETS_DIR = OUT / "contact_sheets"

# (spec, policy). Ticks per the binding unit catalog, roster-expansion batch.
BATCH: list[tuple[dict, str]] = [
    ({
        "name": "cinder_acolyte",
        "build": "sturdy", "seed": 9,
        "proportions": {"torso_w": 14, "torso_h": 12, "leg_h": 9, "head_w": 8, "head_h": 8},
        "typeclass": "support_robed",      # robed lobber -- "she lobs prayers"
        "element": "fire",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["staff"],
        "palette_overrides": {},
        "foreswing_ticks": 8,
        "backswing_ticks": 12,
    }, "always"),
    ({
        "name": "ash_revenant",
        "build": "dangerous", "seed": 1,
        "proportions": {"torso_w": 15, "torso_h": 11, "leg_h": 10, "head_w": 8, "head_h": 7},
        "typeclass": "melee_biped",
        "element": "fire",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["sword"],
        "palette_overrides": {"cloth": ["leather", 1]},
        "foreswing_ticks": 5,
        "backswing_ticks": 7,
    }, "always"),
    ({
        "name": "cinder_wyrmling",
        "typeclass": "aerial_flyer",
        "element": "fire",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        "palette_overrides": {},
        "flyer": {"head": "horned", "wing_mult": 1.0, "eye_px": 2},
        "foreswing_ticks": 8,
        "backswing_ticks": 10,
    }, "always"),
    ({
        "name": "pyre_ogre",
        "seed": 2,
        "typeclass": "ogre",      # hulking biped-variant (OGRE_CONFIG governs mass)
        "element": "fire",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["quarterstaff"],
        "palette_overrides": {"skin": ["tan", 1]},
        "foreswing_ticks": 14,
        "backswing_ticks": 20,
    }, "always"),
    ({
        "name": "rime_sentry",
        "build": "sturdy", "seed": 5,
        "proportions": {"torso_w": 14, "torso_h": 12, "leg_h": 11, "head_w": 8, "head_h": 8},
        "typeclass": "melee_biped",
        "element": "frost",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["spear", "shield"],
        "palette_overrides": {"wood": ["frost", 2]},
        "foreswing_ticks": 7,
        "backswing_ticks": 13,
    }, "always"),
    ({
        "name": "glacier_adept",
        "build": "agile", "seed": 8,
        "proportions": {"torso_w": 11, "leg_h": 12, "head_w": 8, "head_h": 9},
        "typeclass": "support_robed",
        "element": "frost",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["staff"],
        "palette_overrides": {},
        "foreswing_ticks": 10,
        "backswing_ticks": 16,
    }, "always"),
    ({
        "name": "boreal_colossus",
        "build": "sturdy", "seed": 6,
        "proportions": {"torso_w": 18, "torso_h": 14, "leg_h": 9, "head_w": 7, "head_h": 7},
        "typeclass": "melee_biped",
        "element": "frost",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["quarterstaff"],
        "palette_overrides": {},
        "foreswing_ticks": 12,
        "backswing_ticks": 18,
    }, "always"),
    ({
        "name": "bog_stalker",
        "build": "agile", "seed": 3,
        "proportions": {"torso_w": 13, "torso_h": 11, "leg_h": 10, "head_w": 8, "head_h": 8},
        "typeclass": "melee_biped",
        "element": "venom",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["sword"],
        "palette_overrides": {"cloth": ["leather", 1]},
        "foreswing_ticks": 5,
        "backswing_ticks": 9,
    }, "always"),
    ({
        "name": "plague_bell",
        "typeclass": "slime",      # re-themed: a venom plague-slime, not a bell-cart
        "element": "venom",
        "canvas": "40x32",
        "size_class": "medium",
        "props": [],
        "palette_overrides": {},
        "foreswing_ticks": 12,
        "backswing_ticks": 22,
    }, "always"),
    ({
        "name": "quarry_slinger",
        "seed": 5,
        "proportions": {"head_fwd": 2},
        "typeclass": "sniper_biped",
        "element": "stone",
        "canvas": "48x64",
        "size_class": "small",
        "props": ["crossbow"],
        "palette_overrides": {},
        "foreswing_ticks": 13,
        "backswing_ticks": 20,
    }, "always"),
    ({
        "name": "deepway_bulwark",
        "build": "sturdy", "seed": 4,
        "proportions": {"torso_w": 18, "torso_h": 11, "leg_h": 8, "head_w": 8, "head_h": 9},
        "typeclass": "melee_biped",
        "element": "stone",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["spear", "shield"],
        "palette_overrides": {"wood": ["stone", 2], "cloth": ["stone", 1]},
        "foreswing_ticks": 10,
        "backswing_ticks": 16,
    }, "always"),
    ({
        "name": "spark_courier",
        "build": "agile", "seed": 7,
        "proportions": {"torso_w": 10, "torso_h": 8, "leg_h": 15, "head_w": 8, "head_h": 8},
        "typeclass": "melee_biped",
        "element": "storm",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["sword"],
        "palette_overrides": {},
        "foreswing_ticks": 4,
        "backswing_ticks": 8,
    }, "always"),
    ({
        "name": "gale_harrier",
        "typeclass": "aerial_flyer",
        "element": "storm",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        "palette_overrides": {},
        "flyer": {"head": "beaked", "wing_mult": 1.3, "eye_px": 2},
        "foreswing_ticks": 7,
        "backswing_ticks": 9,
    }, "always"),
]


def needs_regen(spec: dict, policy: str) -> tuple[bool, str]:
    if policy == "always":
        return True, "forced"
    mp = UNITS_DIR / f"{spec['name']}.manifest.json"
    if not mp.exists():
        return True, "no existing manifest"
    with open(mp, "r", encoding="utf-8") as f:
        m = json.load(f)
    have = (m.get("foreswing_ticks"), m.get("backswing_ticks"))
    want = (spec["foreswing_ticks"], spec["backswing_ticks"])
    if have != want:
        return True, f"ticks {have[0]}/{have[1]} -> {want[0]}/{want[1]}"
    return False, "ticks match; accepted art kept"


def run(only: str | None = None) -> int:
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    bg = backgrounds.DEFAULT_OUT
    if not bg.exists():
        backgrounds.generate_battle_background(bg)

    results = []
    strip_inputs = []
    dist_units = []
    for spec, policy in BATCH:
        name = spec["name"]
        if only and name != only:
            continue
        regen, reason = needs_regen(spec, policy)
        sheet = UNITS_DIR / f"{name}_sheet.png"
        manifest = UNITS_DIR / f"{name}.manifest.json"
        contacts = []
        if regen:
            gen = generate_unit.generate_unit(spec, UNITS_DIR)
            sheet, manifest = Path(gen["sheet"]), Path(gen["manifest"])
            contacts = contact_sheet.make_contact_sheets(sheet, manifest, bg, SHEETS_DIR)
        report = lint.lint_sheet(sheet, manifest)
        rp = REPORTS_DIR / f"roster_{name}.lint.json"
        with open(rp, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        strip_inputs.append((str(sheet), str(manifest)))
        # body-only silhouette mask for the cross-unit distinctness check
        sil = UNITS_DIR / f"{name}.silhouette.png"
        if sil.exists():
            with Image.open(sil) as im:
                mask = lint.silhouette_mask(im)
            with open(manifest, "r", encoding="utf-8") as f:
                mdata = json.load(f)
            dist_units.append({"name": name, "typeclass": mdata["typeclass"],
                               "mask": mask, "body_size": mdata.get("body_size", {})})
        results.append({
            "name": name,
            "regenerated": regen,
            "reason": reason,
            "lint_pass": report["passed"],
            "failed_checks": [k for k, v in report["checks"].items() if not v["passed"]],
            "warnings": report["checks"].get("body_size", {}).get("warnings", []),
            "body_size": (json.load(open(manifest))).get("body_size"),
            "contact_sheets": contacts,
        })

    strip_paths = []
    dist_report = None
    if not only:
        strip_paths = contact_sheet.make_silhouette_strip(
            strip_inputs, SHEETS_DIR, name="roster_silhouette_strip")
        # cross-unit silhouette distinctness -> the C# art-contract gate reads
        # this report (RosterArtContractTest.RosterSilhouettesAreDistinct).
        dist_report = lint.roster_distinctness(dist_units)
        dp = REPORTS_DIR / "roster_distinctness.json"
        with open(dp, "w", encoding="utf-8") as f:
            json.dump(dist_report, f, indent=2)

    print(f"{'unit':<18} {'regen':<6} {'lint':<5} {'failed checks / reason'}")
    print("-" * 78)
    for r in results:
        extra = ", ".join(r["failed_checks"]) if r["failed_checks"] else r["reason"]
        print(f"{r['name']:<18} {'yes' if r['regenerated'] else 'no':<6} "
              f"{'PASS' if r['lint_pass'] else 'FAIL':<5} {extra}")
        for wmsg in r["warnings"]:
            print(f"{'':<18} warn: {wmsg}")
    summary = {"units": results, "silhouette_strip": strip_paths,
               "distinctness": dist_report}
    sp = OUT / "roster_batch_summary.json"
    with open(sp, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    print(f"\nsummary: {sp}")
    if dist_report is not None:
        dc = dist_report["checks"]
        print(f"distinctness: {'PASS' if dist_report['passed'] else 'FAIL'}  "
              f"(iou {'ok' if dc['silhouette_distinctness']['passed'] else 'XX'} / "
              f"scale {'ok' if dc['scale_gap']['passed'] else 'XX'} / "
              f"head {'ok' if dc['head_to_body']['passed'] else 'XX'})  -> "
              f"{REPORTS_DIR / 'roster_distinctness.json'}")
        for off in dc["silhouette_distinctness"]["offenders"]:
            print(f"    too-similar: {off['pair'][0]} ~ {off['pair'][1]}  iou={off['iou']}")
    lint_ok = all(r["lint_pass"] for r in results)
    dist_ok = dist_report is None or dist_report["passed"]
    return 0 if (lint_ok and dist_ok) else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Run the roster-expansion asset batch.")
    ap.add_argument("--only", default=None, help="run a single unit by name")
    args = ap.parse_args(argv)
    return run(args.only)


if __name__ == "__main__":
    sys.exit(main())
