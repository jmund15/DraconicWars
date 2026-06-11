"""run_calibration.py -- the calibration gate (art-direction.md section 10).

Round 2: generates the SIX template specimens end-to-end (generate -> lint ->
contact sheets over the battle background -> 1-bit silhouette strip) and
prints a pass/fail summary:

  - kobold_spearman  melee_biped,   fire,  spear + small shield
  - forest_archer    ranged_biped,  venom, bow (replaces round-1 goblin_slinger)
  - frost_whelp      aerial_flyer,  frost, no props (fly loop)
  - dune_marksman    sniper_biped,  storm, long level crossbow
  - stone_ram        siege_machine, stone, battering-ram chassis on wheels
  - vale_chanter     support_robed, venom, staff with glowing tip

First-playable budget (idle 2 / walk-or-fly / attack 4 with contact frame /
death 3). Canvases: 32x32 except the sniper (48x64 -- art doc section 4 row,
height = range threat) and siege (40x32 -- 26 px chassis + ram lunge room).
Round 3 template rule: ranged/sniper attack frames never bake projectiles.
Exit code 0 only when every unit passes lint. The
visual gate (contact sheets + silhouette strip against section 9's 12 rules,
mutual 1x distinguishability) remains a human/agent step.

CLI:
    python art_pipeline/run_calibration.py [--outdir DIR]
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

CALIBRATION_SPECS = [
    {
        "name": "kobold_spearman",
        "typeclass": "melee_biped",
        "element": "fire",
        "size_class": "small",
        "props": ["spear", "small_shield"],
        "palette_overrides": {"cloth": ["leather", 1]},
        "foreswing_ticks": 4,
        "backswing_ticks": 8,
    },
    {
        "name": "forest_archer",
        "typeclass": "ranged_biped",
        "element": "venom",
        "size_class": "small",
        "props": ["bow"],
        "palette_overrides": {"cloth": ["leather", 1]},
        "foreswing_ticks": 8,
        "backswing_ticks": 4,
    },
    {
        "name": "frost_whelp",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "size_class": "small",
        "props": [],
        "palette_overrides": {},
        "foreswing_ticks": 6,
        "backswing_ticks": 6,
    },
    {
        "name": "dune_marksman",
        "typeclass": "sniper_biped",
        "element": "storm",
        "size_class": "small",
        "props": ["crossbow"],
        "palette_overrides": {"skin": ["tan", 1]},
        "foreswing_ticks": 10,
        "backswing_ticks": 2,
    },
    {
        "name": "stone_ram",
        "typeclass": "siege_machine",
        "element": "stone",
        "size_class": "medium",
        "props": [],
        "palette_overrides": {},
        "foreswing_ticks": 6,
        "backswing_ticks": 6,
    },
    {
        "name": "vale_chanter",
        "typeclass": "support_robed",
        "element": "venom",
        "size_class": "small",
        "props": ["staff"],
        "palette_overrides": {},
        "foreswing_ticks": 5,
        "backswing_ticks": 7,
    },
]


def run(outdir: Path = OUT) -> int:
    units_dir = outdir / "units"
    reports_dir = outdir / "reports"
    sheets_dir = outdir / "contact_sheets"
    reports_dir.mkdir(parents=True, exist_ok=True)

    bg = backgrounds.generate_battle_background(outdir / "backgrounds" / "battle_bg.png")
    print(f"battle background: {bg}")

    results = []
    strip_inputs = []
    for spec in CALIBRATION_SPECS:
        gen = generate_unit.generate_unit(spec, units_dir)
        report = lint.lint_sheet(gen["sheet"], gen["manifest"])
        report_path = reports_dir / f"{spec['name']}.lint.json"
        with open(report_path, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        contacts = contact_sheet.make_contact_sheets(gen["sheet"], gen["manifest"], bg, sheets_dir)
        strip_inputs.append((gen["sheet"], gen["manifest"]))
        results.append({
            "name": spec["name"],
            "typeclass": spec["typeclass"],
            "lint_pass": report["passed"],
            "sheet": gen["sheet"],
            "manifest": gen["manifest"],
            "lint_report": str(report_path),
            "contact_sheets": contacts,
            "body_size": gen["data"].get("body_size"),
            "failed_checks": [k for k, v in report["checks"].items() if not v["passed"]],
        })

    strip_paths = contact_sheet.make_silhouette_strip(strip_inputs, sheets_dir)
    print(f"silhouette strip: {strip_paths[0]}")

    print()
    print(f"{'unit':<18} {'lint':<6} failed checks")
    print("-" * 60)
    for r in results:
        print(f"{r['name']:<18} {'PASS' if r['lint_pass'] else 'FAIL':<6} "
              f"{', '.join(r['failed_checks']) or '-'}")
    print()
    summary = {"units": results, "silhouette_strip": strip_paths}
    summary_path = outdir / "calibration_summary.json"
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    print(f"summary: {summary_path}")
    return 0 if all(r["lint_pass"] for r in results) else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Run the 6-unit calibration gate end-to-end.")
    ap.add_argument("--outdir", default=None, help=f"output root (default {OUT})")
    args = ap.parse_args(argv)
    return run(Path(args.outdir) if args.outdir else OUT)


if __name__ == "__main__":
    sys.exit(main())
