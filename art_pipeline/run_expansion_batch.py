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
    {
        # Mossmite: graduated demo form (aerial_flyer insect_wing, proven by demo_faerie).
        # Rare Venom counter-flipper — a winged spore-mite (distinct from spore_wisp's
        # formless mote) that marks an enemy's defensive element so allied counters land.
        "name": "mossmite",
        "typeclass": "aerial_flyer",
        "element": "venom",
        "canvas": "32x32",
        "size_class": "small",
        "props": [],
        "flyer": {"head": "wyvern", "wing_mult": 1.3, "insect_wing": True, "eye_px": 2},
        # Venom's accent_index (2) equals the skin index, so the accent hex == the body
        # fill and the whole body counts as whitelisted detail (blows the 6px cap). Move
        # accent to a free venom step (3) — distinct from skin (2) and hot (4).
        "palette_overrides": {"accent": ["venom", 3]},
        "foreswing_ticks": 6, "backswing_ticks": 12,
    },
    # --- The 4 new Draconic dragons (one per remaining element). Boss-tier aerials on the
    # pyraxis rig family; each given a DISTINCT silhouette (head/crest/scale; Sythraal uses
    # the serpentine wyrm body_plan) so they stay pairwise-distinct from pyraxis + each other.
    {
        "name": "voltherax",
        "typeclass": "aerial_flyer",
        "element": "storm",
        "canvas": "96x96",
        "size_class": "boss",
        "props": [],
        "flyer": {"scale": 3.2, "wing_mult": 1.2, "head": "beaked", "crest": "ridge",
                  "feather_wing": True, "eye_px": 4, "eye_shape": "slit", "boss": True,
                  "dragon": True, "seed": 12},
        "foreswing_ticks": 8, "backswing_ticks": 12,
    },
    {
        "name": "glacereth",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "canvas": "96x96",
        "size_class": "boss",
        "props": [],
        "flyer": {"scale": 3.0, "wing_mult": 1.0, "head": "horned", "crest": "head",
                  "eye_px": 4, "boss": True, "dragon": True, "seed": 13},
        # Frost accent_index (4) collides with the 'hot' step (frost[4]) — move accent off it.
        "palette_overrides": {"accent": ["frost", 3]},
        "foreswing_ticks": 12, "backswing_ticks": 18,
    },
    {
        "name": "sythraal",
        "typeclass": "aerial_flyer",
        "element": "venom",
        "canvas": "96x96",
        "size_class": "boss",
        "props": [],
        "flyer": {"body_plan": "wyrm", "scale": 2.8, "eye_px": 4, "boss": True,
                  "dragon": True, "seed": 14},
        # Venom accent_index (2) equals the skin index — move accent to a free step.
        "palette_overrides": {"accent": ["venom", 3]},
        "foreswing_ticks": 10, "backswing_ticks": 14,
    },
    # --- New air forms (Part 3): ordnance-flyer (Mythic stormwright) + leviathan (Mythic
    # cloudwhale). Both extend AerialFlyerTemplate via a new body_plan.
    {
        "name": "stormwright",
        "typeclass": "aerial_flyer",
        "element": "storm",
        "canvas": "48x48",
        "size_class": "medium",
        "props": [],
        "flyer": {"body_plan": "ordnance", "scale": 1.5, "wing_mult": 1.1, "eye_px": 2},
        "foreswing_ticks": 14, "backswing_ticks": 28,
    },
    {
        "name": "frostbarge_cloudwhale",
        "typeclass": "aerial_flyer",
        "element": "frost",
        "canvas": "64x48",
        "size_class": "large",
        "props": [],
        "flyer": {"body_plan": "leviathan", "scale": 1.5, "wing_mult": 1.2, "eye_px": 2,
                  "body_dy": -3},
        # Frost accent_index (4) collides with the 'hot' step — move accent off it.
        "palette_overrides": {"accent": ["frost", 3]},
        "foreswing_ticks": 10, "backswing_ticks": 16,
    },
    {
        "name": "terravossk",
        "typeclass": "aerial_flyer",
        "element": "stone",
        "canvas": "96x96",
        "size_class": "boss",
        "props": [],
        "flyer": {"scale": 3.6, "wing_mult": 0.9, "head": "crowned", "crest": "head",
                  "eye_px": 4, "eye_shape": "slit", "boss": True, "dragon": True,
                  "body_dx": -12, "seed": 15},
        "foreswing_ticks": 14, "backswing_ticks": 20,
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
