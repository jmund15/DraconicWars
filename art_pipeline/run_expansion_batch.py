"""run_expansion_batch.py -- renders the roster-expansion-to-40 NEW units to
output/units/ (the <id>_sheet.png + <id>.manifest.json the UnitCatalog and the
RosterArtContractTest gate consume; ticks must match the catalog).

Starts with units on PROVEN templates (graduated demos + existing forms), which
render lint-clean without new-form template work. New ground/air FORMS (naga,
cavalry, burrower, monolith, ordnance-flyer, leviathan) are added as their
templates land in skeletons.py. See roster-expansion-40.md.

CLI: python art_pipeline/run_expansion_batch.py [--only NAME]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import backgrounds
import contact_sheet
import generate_unit
import lint

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "output"
UNITS_DIR = OUT / "units"
SHEETS_DIR = OUT / "contact_sheets"

UNITS: list[dict] = [
    {
        # Skylance: a Dragonlord lieutenant on a tamed lesser-drake disc (FlyingMount
        # base, proven by demo_sky_rider). Aerial striker; its lance is a real
        # projectile (sim-side ProjectileSpeed). Ticks match the catalog UnitDef.
        "name": "skylance_emberknight",
        "typeclass": "melee_biped",
        "base": "mount",
        "head_style": "plain",
        "element": "fire",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["sword"],
        "build": "dangerous", "seed": 7,
        "proportions": {"torso_w": 12, "torso_h": 13, "leg_h": 11, "head_w": 8, "head_h": 8},
        "palette_overrides": {},
        "foreswing_ticks": 8, "backswing_ticks": 12,
    },
]


def run(only: str | None = None) -> int:
    UNITS_DIR.mkdir(parents=True, exist_ok=True)
    bg = backgrounds.DEFAULT_OUT
    if not bg.exists():
        backgrounds.generate_battle_background(bg)
    ok = True
    print(f"{'unit':<24} {'lint':<5} {'failed checks'}")
    print("-" * 60)
    for spec in UNITS:
        if only and spec["name"] != only:
            continue
        gen = generate_unit.generate_unit(spec, UNITS_DIR)
        sheet, manifest = Path(gen["sheet"]), Path(gen["manifest"])
        contact_sheet.make_contact_sheets(sheet, manifest, bg, SHEETS_DIR)
        report = lint.lint_sheet(sheet, manifest)
        ok = ok and report["passed"]
        failed = [k for k, v in report["checks"].items() if not v["passed"]]
        print(f"{spec['name']:<24} {'PASS' if report['passed'] else 'FAIL':<5} {', '.join(failed)}")
    return 0 if ok else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Render roster-expansion units.")
    ap.add_argument("--only", default=None, help="render a single unit by name")
    return run(ap.parse_args(argv).only)


if __name__ == "__main__":
    sys.exit(main())
