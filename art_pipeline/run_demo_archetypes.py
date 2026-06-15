"""run_demo_archetypes.py -- showcase sheets for NEW archetype capabilities that
have no roster unit yet (so the capability is proven + reviewable without inventing
a phantom game unit). Output: art_pipeline/output/demos/.

Demonstrates:
  * demo_sky_rider -- a melee biped on a FlyingMount base (a unit riding a hovering
    disc / flying machinery): composition, no flyer template.
  * demo_phoenix   -- an aerial flyer with the feather_wing + fire_tail + crest
    treatment (a feathered fire bird, distinct from the membrane dragons).

CLI: python art_pipeline/run_demo_archetypes.py [--only NAME]
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
DEMOS_DIR = OUT / "demos"
SHEETS_DIR = OUT / "contact_sheets"

DEMOS: list[dict] = [
    {
        "name": "demo_sky_rider",
        "typeclass": "melee_biped",
        "base": "mount",            # FlyingMount: rides a hovering disc
        "head_style": "plain",
        "element": "storm",
        "canvas": "32x32",
        "size_class": "small",
        "props": ["sword"],
        "build": "agile", "seed": 4,
        "proportions": {"torso_w": 12, "torso_h": 13, "leg_h": 11, "head_w": 8, "head_h": 8},
        "palette_overrides": {},
        "foreswing_ticks": 6, "backswing_ticks": 10,
    },
    {
        "name": "demo_phoenix",
        "typeclass": "aerial_flyer",
        "element": "fire",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        # feathered fire bird: beaked + feather wings + a flame tail + head crest
        "flyer": {"scale": 1.6, "wing_mult": 1.15, "head": "beaked", "crest": "head",
                  "fire_tail": True, "feather_wing": True, "eye_px": 2},
        "palette_overrides": {},
        "foreswing_ticks": 8, "backswing_ticks": 12,
    },
    {
        "name": "demo_faerie",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        # tiny sprite: two thin gossamer wing pairs (wide enough to clear the 24px
        # aerial wingspan floor), minimal head
        "flyer": {"head": "wyvern", "wing_mult": 1.3, "insect_wing": True, "eye_px": 2},
        "palette_overrides": {},
        "foreswing_ticks": 6, "backswing_ticks": 10,
    },
    {
        "name": "demo_wyrm",
        "typeclass": "aerial_flyer",
        "element": "fire",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        # serpentine eastern dragon: a long ribbon body, no large wings, dorsal
        # fins + mane + barbels + a high antlered head. Fire ramp = a red/gold
        # eastern dragon (venom is avoided: its accent hex == skin, tripping the
        # whitelist cap on a wingless body).
        "flyer": {"body_plan": "wyrm", "eye_px": 2},
        "palette_overrides": {},
        "foreswing_ticks": 8, "backswing_ticks": 12,
    },
    {
        "name": "demo_seraph",
        "typeclass": "aerial_flyer",
        "element": "storm",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        # frontal celestial: a glowing core + halo + three symmetric wing-pairs
        # (6 wings). Storm ramp = a radiant golden seraph (frost is avoided: its
        # hot hex == accent, tripping the whitelist cap on the big core glow).
        "flyer": {"body_plan": "seraph", "eye_px": 1, "wing_mult": 1.0},
        "palette_overrides": {},
        "foreswing_ticks": 8, "backswing_ticks": 12,
    },
    {
        "name": "demo_wisp",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        # wingless floating elemental: a bright teal heart + skin orb + membrane
        # aura, ringed by orbiting motes (no head/wings). The bright core uses
        # BELLY, not hot (frost's hot hex == accent would trip the whitelist).
        "flyer": {"body_plan": "wisp", "eye_px": 1},
        "palette_overrides": {},
        "foreswing_ticks": 4, "backswing_ticks": 8,
    },
    {
        "name": "demo_manta",
        "typeclass": "aerial_flyer",
        "element": "stone",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        # sky-ray glider: a wide flat delta body (pectoral wings continuous with
        # the body), twin cephalic horns at the nose, a long trailing whip-tail
        # (stone ramp = a sandstone sky-ray)
        "flyer": {"body_plan": "manta", "eye_px": 1},
        "palette_overrides": {},
        "foreswing_ticks": 6, "backswing_ticks": 12,
    },
]


def run(only: str | None = None) -> int:
    DEMOS_DIR.mkdir(parents=True, exist_ok=True)
    bg = backgrounds.DEFAULT_OUT
    if not bg.exists():
        backgrounds.generate_battle_background(bg)
    ok = True
    print(f"{'demo':<18} {'lint':<5} {'failed checks'}")
    print("-" * 60)
    for spec in DEMOS:
        if only and spec["name"] != only:
            continue
        gen = generate_unit.generate_unit(spec, DEMOS_DIR)
        sheet, manifest = Path(gen["sheet"]), Path(gen["manifest"])
        contact_sheet.make_contact_sheets(sheet, manifest, bg, SHEETS_DIR)
        report = lint.lint_sheet(sheet, manifest)
        ok = ok and report["passed"]
        failed = [k for k, v in report["checks"].items() if not v["passed"]]
        print(f"{spec['name']:<18} {'PASS' if report['passed'] else 'FAIL':<5} {', '.join(failed)}")
    return 0 if ok else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Generate demo sheets for new archetype capabilities.")
    ap.add_argument("--only", default=None, help="run a single demo by name")
    return run(ap.parse_args(argv).only)


if __name__ == "__main__":
    sys.exit(main())
