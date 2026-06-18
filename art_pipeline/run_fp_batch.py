"""run_fp_batch.py -- the first-playable asset batch (11 units + Cinderfell).

Unit ticks come from the game's unit catalog and are BINDING (the Godot
importer rejects manifest disagreement). Regeneration policy per unit:
  always  -- new units, or forced regens (stone_ram: palette remap + new ticks)
  ticks   -- regenerate ONLY if the existing manifest's foreswing/backswing
             ticks differ from the spec; otherwise leave the accepted art
             untouched and report regenerated=false.

Pipeline per regenerated unit: generate -> lint -> contact sheets (1x + 3x).
Untouched units are still linted (9-check) against their accepted sheets.
Finishes with the Cinderfell parallax set + a silhouette strip over ALL units.

CLI:
    python art_pipeline/run_fp_batch.py [--only NAME] [--skip-backgrounds]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import backgrounds
import contact_sheet
import generate_unit
import lint

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "output"
UNITS_DIR = OUT / "units"
REPORTS_DIR = OUT / "reports"
SHEETS_DIR = OUT / "contact_sheets"

# (spec, policy). Ticks per the binding unit catalog, 2026-06-11 batch.
BATCH: list[tuple[dict, str]] = [
    ({
        "name": "stone_warden",
        "typeclass": "melee_biped",
        "element": "stone",
        "canvas": "32x32",
        "size_class": "small",
        "build": "sturdy",                 # broad tank silhouette (roster distinctness)
        "props": ["sword", "shield"],      # LARGE round shield = class identity
        # wood -> stone ramp: a leather-faced shield blends into the leather
        # torso (class identity lost) AND the default face (leather[3])
        # collides with the stone element's whitelisted accent hex.
        "palette_overrides": {"wood": ["stone", 2]},
        "foreswing_ticks": 10,
        "backswing_ticks": 14,
    }, "always"),
    ({
        "name": "storm_monk",
        "typeclass": "melee_biped",
        "element": "storm",
        "canvas": "32x32",
        "size_class": "small",
        "build": "agile",                  # lithe, fast silhouette (roster distinctness)
        "props": ["quarterstaff"],
        "palette_overrides": {},
        "foreswing_ticks": 4,
        "backswing_ticks": 6,
    }, "always"),
    ({
        "name": "vale_chanter",
        "typeclass": "support_robed",
        "element": "storm",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["staff"],
        "palette_overrides": {},
        "foreswing_ticks": 10,
        "backswing_ticks": 20,
    }, "ticks"),
    ({
        "name": "dune_marksman",
        "typeclass": "sniper_biped",
        "element": "stone",
        "canvas": "48x64",
        "size_class": "small",
        "build": "sturdy",                 # broad silhouette vs the lean ember/quarry snipers
        "props": ["crossbow"],
        "palette_overrides": {"skin": ["tan", 1]},
        "foreswing_ticks": 14,
        "backswing_ticks": 22,
    }, "always"),
    ({
        "name": "forest_archer",
        "typeclass": "ranged_biped",
        "element": "venom",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["bow"],
        "palette_overrides": {"cloth": ["leather", 1]},
        "foreswing_ticks": 6,
        "backswing_ticks": 12,
    }, "ticks"),
    ({
        "name": "kobold_spearman",
        "typeclass": "melee_biped",
        "element": "fire",
        "canvas": "32x32",
        "size_class": "small",
        "build": "dangerous",      # fierce wedge silhouette, distinct from the other melee bipeds
        "proportions": {"torso_w": 12, "torso_h": 11, "leg_h": 12, "head_w": 9, "head_h": 9},
        "eye_shape": "slit",       # reptilian kobold -- vertical slit pupil
        "props": ["spear", "small_shield"],
        "palette_overrides": {"cloth": ["leather", 1]},
        "foreswing_ticks": 4,
        "backswing_ticks": 8,
    }, "always"),
    ({
        "name": "frost_whelp",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        "palette_overrides": {},
        "foreswing_ticks": 6,
        "backswing_ticks": 6,
    }, "ticks"),
    ({
        "name": "stone_ram",
        "typeclass": "siege_machine",
        "element": "stone",
        "canvas": "40x32",
        "size_class": "medium",
        "props": [],
        "palette_overrides": {},
        "foreswing_ticks": 12,
        "backswing_ticks": 24,
    }, "always"),
    ({
        "name": "storm_gryphon",
        "typeclass": "aerial_flyer",
        "element": "storm",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        "palette_overrides": {},
        "flyer": {"scale": 1.55, "wing_mult": 0.86, "head": "beaked",
                  "crest": "head", "eye_px": 2, "body_dx": -4, "feather_wing": True},
        "foreswing_ticks": 8,
        "backswing_ticks": 10,
    }, "always"),
    ({
        "name": "elder_drake",
        "typeclass": "aerial_flyer",
        "element": "stone",
        "canvas": "80x80",        # adult-dragon tier (roadmap reserves 80-96)
        "size_class": "large",
        "props": [],
        # slate membrane: the moss-green stone ramp reads swampy against the
        # ochre leather body; neutral basalt grey keeps the stone identity
        "palette_overrides": {"membrane": ["mauve_grey", 1]},
        "flyer": {"scale": 2.5, "wing_mult": 1.05, "tail_mult": 1.25,
                  "head": "horned", "eye_px": 2, "eye_shape": "slit",
                  "body_dx": -2, "body_dy": -1, "dragon": True},
        "foreswing_ticks": 12,
        "backswing_ticks": 18,
    }, "always"),
    ({
        "name": "pyraxis",
        "typeclass": "aerial_flyer",
        "element": "fire",
        "canvas": "96x96",
        "size_class": "boss",
        "props": [],
        "palette_overrides": {},
        "flyer": {"scale": 3.2, "wing_mult": 0.84, "tail_mult": 1.3,
                  "head": "crowned", "crest": "ridge", "fire_tail": True,
                  "eye_px": 4, "eye_shape": "slit", "boss": True, "body_dx": -10,
                  "dragon": True, "seed": 5},
        "foreswing_ticks": 10,
        "backswing_ticks": 14,
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


def run(only: str | None = None, skip_backgrounds: bool = False) -> int:
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    bg = backgrounds.DEFAULT_OUT
    if not bg.exists():
        backgrounds.generate_battle_background(bg)

    results = []
    strip_inputs = []
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
        rp = REPORTS_DIR / f"fp_{name}.lint.json"
        with open(rp, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        strip_inputs.append((str(sheet), str(manifest)))
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
    if not only:
        strip_paths = contact_sheet.make_silhouette_strip(strip_inputs, SHEETS_DIR)

    bg_paths = []
    if not skip_backgrounds and not only:
        bg_paths = [str(p) for p in backgrounds.generate_cinderfell_set()]

    print(f"{'unit':<16} {'regen':<6} {'lint':<5} {'failed checks / reason'}")
    print("-" * 78)
    for r in results:
        extra = ", ".join(r["failed_checks"]) if r["failed_checks"] else r["reason"]
        print(f"{r['name']:<16} {'yes' if r['regenerated'] else 'no':<6} "
              f"{'PASS' if r['lint_pass'] else 'FAIL':<5} {extra}")
        for wmsg in r["warnings"]:
            print(f"{'':<16} warn: {wmsg}")
    summary = {"units": results, "silhouette_strip": strip_paths,
               "cinderfell": bg_paths}
    sp = OUT / "fp_batch_summary.json"
    with open(sp, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    print(f"\nsummary: {sp}")
    return 0 if all(r["lint_pass"] for r in results) else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Run the FP asset batch end-to-end.")
    ap.add_argument("--only", default=None, help="run a single unit by name")
    ap.add_argument("--skip-backgrounds", action="store_true")
    args = ap.parse_args(argv)
    return run(args.only, args.skip_backgrounds)


if __name__ == "__main__":
    sys.exit(main())
