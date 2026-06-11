"""run_calibration.py -- the calibration gate (art-direction.md section 10).

Generates the 3 calibration units end-to-end (generate -> lint -> contact
sheet over the battle background) and prints a pass/fail summary:

  - kobold_spearman  melee_biped,  fire,  spear + small shield, 32x32
  - goblin_slinger   ranged_biped, venom, sling,                32x32
  - frost_whelp      aerial_flyer, frost, no props (fly loop),  32x32

Exit code 0 only when every unit passes lint. The visual gate (review the
contact sheets against section 9's 12 rules) remains a human/agent step.

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
        "name": "goblin_slinger",
        "typeclass": "ranged_biped",
        "element": "venom",
        "size_class": "small",
        "props": ["sling"],
        "palette_overrides": {"skin": ["venom", 2], "cloth": ["leather", 1]},
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
]


def run(outdir: Path = OUT) -> int:
    units_dir = outdir / "units"
    reports_dir = outdir / "reports"
    sheets_dir = outdir / "contact_sheets"
    reports_dir.mkdir(parents=True, exist_ok=True)

    bg = backgrounds.generate_battle_background(outdir / "backgrounds" / "battle_bg.png")
    print(f"battle background: {bg}")

    results = []
    for spec in CALIBRATION_SPECS:
        gen = generate_unit.generate_unit(spec, units_dir)
        report = lint.lint_sheet(gen["sheet"], gen["manifest"])
        report_path = reports_dir / f"{spec['name']}.lint.json"
        with open(report_path, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        contacts = contact_sheet.make_contact_sheets(gen["sheet"], gen["manifest"], bg, sheets_dir)
        results.append({
            "name": spec["name"],
            "lint_pass": report["passed"],
            "sheet": gen["sheet"],
            "manifest": gen["manifest"],
            "lint_report": str(report_path),
            "contact_sheets": contacts,
            "failed_checks": [k for k, v in report["checks"].items() if not v["passed"]],
        })

    print()
    print(f"{'unit':<18} {'lint':<6} failed checks")
    print("-" * 60)
    for r in results:
        print(f"{r['name']:<18} {'PASS' if r['lint_pass'] else 'FAIL':<6} "
              f"{', '.join(r['failed_checks']) or '-'}")
    print()
    summary_path = outdir / "calibration_summary.json"
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2)
    print(f"summary: {summary_path}")
    return 0 if all(r["lint_pass"] for r in results) else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Run the 3-unit calibration gate end-to-end.")
    ap.add_argument("--outdir", default=None, help=f"output root (default {OUT})")
    args = ap.parse_args(argv)
    return run(Path(args.outdir) if args.outdir else OUT)


if __name__ == "__main__":
    sys.exit(main())
