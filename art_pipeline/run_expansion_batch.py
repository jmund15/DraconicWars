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
    {
        # Ember Arbalest: Common Fire sniper on the proven sniper_biped rig (same
        # template as quarry_slinger/dune_marksman). Glass heavy crossbow — one big
        # bolt, slow cycle. No new form. Ticks mirror the catalog UnitDef.
        "name": "ember_arbalest",
        "typeclass": "sniper_biped",
        "element": "fire",
        "canvas": "48x64",
        "size_class": "small",
        "props": ["crossbow"],
        "seed": 11,
        "proportions": {"head_fwd": 2},
        "palette_overrides": {},
        "foreswing_ticks": 14, "backswing_ticks": 24,
    },
    {
        # Glide Manta: graduated demo form (aerial_flyer manta body_plan, proven by
        # demo_manta). Common Frost anti-air interceptor — a sky-ray that prioritizes
        # airborne prey (sim PrefersAirTarget). Frost ramp = a rime sky-ray.
        "name": "glide_manta",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        "flyer": {"body_plan": "manta", "eye_px": 1},
        # Frost's accent_index (4) collides with the flyer 'hot' step (frost[4]=8fd3ff),
        # so the bright maw-glow geometry counts as whitelisted accent and blows the 6px
        # detail cap. Move accent off index 4 → the maw glow stays bright 'hot' geometry
        # (>=3px, uncapped) while the small accent detail reads as a distinct icy blue.
        "palette_overrides": {"accent": ["frost", 3]},
        "foreswing_ticks": 6, "backswing_ticks": 10,
    },
    {
        # Spore Wisp: graduated demo form (aerial_flyer wisp body_plan, proven by
        # demo_wisp). Common Venom evasive harasser — a formless mote ringed by orbiting
        # spores (distinct from mossmite's insect_wing). Periodically phases out (sim
        # PhaseCadenceTicks). The wisp core uses BELLY, sidestepping the hot/accent cap.
        "name": "spore_wisp",
        "typeclass": "aerial_flyer",
        "element": "venom",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        "flyer": {"body_plan": "wisp", "eye_px": 1},
        "palette_overrides": {},
        "foreswing_ticks": 6, "backswing_ticks": 10,
    },
    {
        # Crag Tyrant (Roc): colossal feather_wing preset (same rig family as demo_phoenix
        # / gale_harrier, scaled up ~2x). Epic Stone aerial that grabs the frontmost enemy
        # and throws it back (sim GrabThrowDistance). A giant stone eagle — beaked, ridged
        # crest, broad feather wings; no fire tail.
        "name": "crag_tyrant",
        "typeclass": "aerial_flyer",
        "element": "stone",
        "canvas": "64x64",
        "size_class": "colossal",
        "props": [],
        "flyer": {"scale": 2.0, "wing_mult": 1.2, "head": "beaked", "crest": "ridge",
                  "feather_wing": True, "eye_px": 3, "seed": 9},
        "foreswing_ticks": 12, "backswing_ticks": 16,
    },
    {
        # Tempest Choir: graduated demo form (aerial_flyer seraph body_plan, proven by
        # demo_seraph). Epic Storm support — a radiant golden seraph that acts as a living
        # mana-conduit and projects a haste-halo. Frontal celestial: glowing core + halo +
        # symmetric wing-pairs.
        "name": "tempest_choir",
        "typeclass": "aerial_flyer",
        "element": "storm",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        "flyer": {"body_plan": "seraph", "eye_px": 1, "wing_mult": 1.0},
        "palette_overrides": {},
        "foreswing_ticks": 10, "backswing_ticks": 16,
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
