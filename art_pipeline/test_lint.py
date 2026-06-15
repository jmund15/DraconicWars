"""test_lint.py -- TDD for the relaxed `source: external` lint profile.

The art pipeline is a pure-Python sidecar (Pillow only; no pytest). These tests
use stdlib ``unittest`` and run standalone:

    python art_pipeline/test_lint.py

The relaxed profile demotes EXACTLY {no_aa, outline_1px, banding} to warnings
when (and only when) the manifest carries ``"source": "external"``. The
load-bearing checks (on_palette, frames, manifest_contract, body_size) stay
hard, as do orphan_pixels and whitelist_cap (the spec names only the three).
"""

from __future__ import annotations

import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

import lint
from palette import get_palette, hex_to_rgb

# fire ramp index 2 -- a MID-ramp color: on-palette, but NOT any ramp's step-0
# and not ink, so it never counts as sel-out "dark" boundary coverage. This is
# what forces outline_1px to fail deterministically (plan_check F3).
BODY_HEX = "fb6b1d"
BODY_RGB = hex_to_rgb(BODY_HEX)
OFF_PALETTE_RGB = (1, 2, 3)  # guaranteed absent from Resurrect-64


def _draw_body(px, ox: int, oy: int, color=BODY_RGB) -> None:
    """Solid 16w x 26h bar, lowest opaque row at local y=29 (== ground_y)."""
    for y in range(4, 30):      # local 4..29 inclusive -> 26 rows, bottom at 29
        for x in range(8, 24):  # local 8..23 inclusive -> 16 cols
            px[ox + x, oy + y] = (*color, 255)


def _build_fixture(tmpdir: str, *, source: str | None = None,
                   off_palette: bool = False) -> tuple[str, str]:
    """A melee_biped sheet that passes every HARD check but fails outline_1px.

    Cells are 32x32; 4 rows (idle/walk/attack/death). Attack has 2 frames, so
    the sheet is 64x128. Returns (sheet_path, manifest_path).
    """
    fw = fh = 32
    sheet = Image.new("RGBA", (fw * 2, fh * 4), (0, 0, 0, 0))
    px = sheet.load()
    # idle r0c0, walk r1c0, attack r2c0+r2c1, death r3c0
    for (ox, oy) in [(0, 0), (0, 32), (0, 64), (32, 64), (0, 96)]:
        _draw_body(px, ox, oy)
    if off_palette:
        # 3 contiguous interior pixels -> on_palette fails, but the region is
        # >= MIN_CLUSTER so orphan_pixels does NOT also fire (isolates the HARD
        # check under test).
        for x in (12, 13, 14):
            px[x, 15] = (*OFF_PALETTE_RGB, 255)

    sheet_path = Path(tmpdir) / "fixture_sheet.png"
    sheet.save(sheet_path)

    manifest = {
        "name": "fixture",
        "typeclass": "melee_biped",
        "element": "fire",
        "frame_w": fw,
        "frame_h": fh,
        "animations": [
            {"name": "idle", "row": 0, "frames": 1, "fps": 7, "loop": True},
            {"name": "walk", "row": 1, "frames": 1, "fps": 10, "loop": True},
            {"name": "attack", "row": 2, "frames": 2, "fps": 12, "loop": False,
             "contact_frame": 1},
            {"name": "death", "row": 3, "frames": 1, "fps": 10, "loop": False},
        ],
        "foreswing_ticks": 4,
        "backswing_ticks": 8,
        "ground_y": 29,
        "body_size": {"width": 16, "height": 26},
        "lint": {"whitelist_colors": [], "prop_colors": [],
                 "airborne_animations": []},
    }
    if source is not None:
        manifest["source"] = source
    manifest_path = Path(tmpdir) / "fixture.manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f)
    return str(sheet_path), str(manifest_path)


def _minimal_manifest(tmpdir: str, sheet, *, fw: int, fh: int,
                      exempt: list[str] | None = None,
                      whitelist: list[str] | None = None) -> str:
    """Write a bare manifest carrying just the lint block + frame geometry."""
    manifest = {
        "name": "fx", "frame_w": fw, "frame_h": fh,
        "animations": [{"name": "idle", "row": 0, "frames": 1}],
        "ground_y": fh - 3,
        "lint": {
            "whitelist_colors": whitelist or [],
            "prop_colors": [],
            "airborne_animations": [],
            "detail_exempt_colors": exempt or [],
        },
    }
    mp = Path(tmpdir) / "fx.manifest.json"
    with open(mp, "w", encoding="utf-8") as f:
        json.dump(manifest, f)
    return str(mp)


def _build_banded_fixture(tmpdir: str, *, exempt: list[str] | None = None
                          ) -> tuple[str, str]:
    """Two ADJACENT fire-ramp steps as a 1px/1px hugging horizontal pair of
    run length 13 (> band_limit 6 at fh=32), isolated above/below -> a banding
    offender. With its hi hex in detail_exempt_colors it must stop counting."""
    pal = get_palette()
    ramp = pal.ramps["fire"]
    hi_hex, lo_hex = ramp[2], ramp[3]          # consecutive -> in adjacent_steps
    hi, lo = hex_to_rgb(hi_hex), hex_to_rgb(lo_hex)
    fw = fh = 32
    sheet = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
    px = sheet.load()
    y = 10
    for x in range(5, 18):                     # 13 columns
        px[x, y] = (*hi, 255)
        px[x, y + 1] = (*lo, 255)
    sheet_path = Path(tmpdir) / "banded.png"
    sheet.save(sheet_path)
    mp = _minimal_manifest(tmpdir, sheet, fw=fw, fh=fh, exempt=exempt)
    return str(sheet_path), mp


def _build_orphan_fixture(tmpdir: str, *, exempt: list[str] | None = None
                          ) -> tuple[str, str]:
    """A single isolated MID-ramp pixel (not a darkest sel-out step) -> a
    sub-MIN_CLUSTER orphan offender. With its hex in detail_exempt_colors it
    must be exempted like a whitelisted detail pixel."""
    pal = get_palette()
    mid_hex = pal.ramps["fire"][3]             # mid-ramp, NOT step-0 darkest
    mid = hex_to_rgb(mid_hex)
    fw = fh = 32
    sheet = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
    px = sheet.load()
    px[16, 16] = (*mid, 255)                   # isolated single pixel
    sheet_path = Path(tmpdir) / "orphan.png"
    sheet.save(sheet_path)
    mp = _minimal_manifest(tmpdir, sheet, fw=fw, fh=fh, exempt=exempt)
    return str(sheet_path), mp


# Checks the relaxed external profile demotes to warnings (spec verbatim).
DEMOTED = {"no_aa", "outline_1px", "banding"}
# Checks that must stay hard regardless of profile.
HARD = {"on_palette", "frames", "manifest_contract", "body_size",
        "orphan_pixels", "whitelist_cap"}


class RelaxedLintProfileTest(unittest.TestCase):
    def setUp(self):
        # The mid-ramp body color must NOT be a sel-out-eligible dark step, or
        # outline_1px would pass and the demotion would be untested.
        self.assertNotIn(BODY_HEX, get_palette().darkest_step_hexes())

    def test_fixture_isolates_outline_1px(self):
        """Sanity: the fixture fails outline_1px and ONLY outline_1px."""
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_fixture(d)
            report = lint.lint_sheet(sheet, manifest)
            failing = {n for n, c in report["checks"].items() if not c["passed"]}
            self.assertEqual(failing, {"outline_1px"},
                             f"fixture should fail only outline_1px, got {failing}")

    def test_relaxable_strict_fails(self):
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_fixture(d, source=None)
            report = lint.lint_sheet(sheet, manifest)
            self.assertFalse(report["passed"])
            self.assertEqual(report["profile"], "strict")
            self.assertFalse(report["checks"]["outline_1px"]["passed"])
            self.assertEqual(report["checks"]["outline_1px"]["severity"], "error")

    def test_non_external_source_stays_hard(self):
        """A non-'external' source value does NOT relax anything (24-unit
        regression invariant: only the exact string 'external' relaxes)."""
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_fixture(d, source="generated")
            report = lint.lint_sheet(sheet, manifest)
            self.assertFalse(report["passed"])
            self.assertEqual(report["profile"], "strict")
            for name in DEMOTED:
                self.assertEqual(report["checks"][name]["severity"], "error",
                                 f"{name} must stay hard for non-external source")

    def test_relaxable_external_passes(self):
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_fixture(d, source="external")
            report = lint.lint_sheet(sheet, manifest)
            self.assertTrue(report["passed"],
                            "outline_1px is demoted under external -> overall pass")
            self.assertEqual(report["profile"], "external")
            self.assertFalse(report["checks"]["outline_1px"]["passed"])
            self.assertEqual(report["checks"]["outline_1px"]["severity"], "warning")

    def test_hard_violation_external_still_fails(self):
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_fixture(d, source="external", off_palette=True)
            report = lint.lint_sheet(sheet, manifest)
            self.assertFalse(report["passed"], "on_palette is hard even when external")
            self.assertFalse(report["checks"]["on_palette"]["passed"])
            self.assertEqual(report["checks"]["on_palette"]["severity"], "error")

    def test_demoted_set_exact(self):
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_fixture(d, source="external")
            report = lint.lint_sheet(sheet, manifest)
            warnings = {n for n, c in report["checks"].items()
                        if c["severity"] == "warning"}
            errors = {n for n, c in report["checks"].items()
                      if c["severity"] == "error"}
            self.assertEqual(warnings, DEMOTED)
            self.assertEqual(errors, HARD)

    # --- Part C / WS5: detail_exempt_colors channel (rim-light + internal volume) ---

    def test_banding_offender_then_exempt_channel(self):
        """A long adjacent-step 1px band trips banding; declaring its hex in
        detail_exempt_colors exempts it (rim-light / internal-shading bands)."""
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_banded_fixture(d, exempt=None)
            report = lint.lint_sheet(sheet, manifest)
            self.assertFalse(report["checks"]["banding"]["passed"],
                             "control: long adjacent-step band must trip banding")
        with tempfile.TemporaryDirectory() as d:
            hi_hex = get_palette().ramps["fire"][2]
            sheet, manifest = _build_banded_fixture(d, exempt=[hi_hex])
            report = lint.lint_sheet(sheet, manifest)
            self.assertTrue(report["checks"]["banding"]["passed"],
                            "detail_exempt_colors must exempt the declared band hex")

    def test_orphan_then_exempt_channel(self):
        """An isolated mid-ramp pixel trips orphan_pixels; declaring its hex in
        detail_exempt_colors exempts it like a whitelisted detail pixel."""
        with tempfile.TemporaryDirectory() as d:
            sheet, manifest = _build_orphan_fixture(d, exempt=None)
            report = lint.lint_sheet(sheet, manifest)
            self.assertFalse(report["checks"]["orphan_pixels"]["passed"],
                             "control: isolated mid-ramp pixel must trip orphan_pixels")
        with tempfile.TemporaryDirectory() as d:
            mid_hex = get_palette().ramps["fire"][3]
            sheet, manifest = _build_orphan_fixture(d, exempt=[mid_hex])
            report = lint.lint_sheet(sheet, manifest)
            self.assertTrue(report["checks"]["orphan_pixels"]["passed"],
                            "detail_exempt_colors must exempt the declared orphan hex")


class WhitelistCapCollisionTest(unittest.TestCase):
    """Anti-collision is enforced by lint's whitelist_cap, NOT a producer-side
    color-assert: a color collision (accent hex == a fill hex) is only a real
    violation when that fill actually draws > WHITELIST_CAP px in a frame (lint
    counts by raw hex, part-blind). A producer color-assert false-positives on
    harmless collisions -- demo_faerie ships fine with accent==hot==8fd3ff
    because its insect-wing body draws <=6px of it. lint is the authoritative,
    pixel-accurate gate; this pins the mechanism Slice-5 accents rely on."""

    def test_whitelist_cap_counts_collided_fill(self):
        """A body fill sharing the whitelisted (accent) hex blows the 6px cap --
        this is the gate that protects against a Slice-5 accent landing on a
        LARGE fill, caught pixel-accurately at regen."""
        with tempfile.TemporaryDirectory() as d:
            fw = fh = 32
            sheet = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
            px = sheet.load()
            for y in range(8, 12):
                for x in range(8, 12):          # 16px block >> 6
                    px[x, y] = (*BODY_RGB, 255)
            sp = Path(d) / "cap.png"
            sheet.save(sp)
            mp = _minimal_manifest(d, sheet, fw=fw, fh=fh, whitelist=[BODY_HEX])
            report = lint.lint_sheet(str(sp), mp)
            self.assertFalse(report["checks"]["whitelist_cap"]["passed"],
                             "part-blind: a 16px whitelisted fill blows the 6px cap")


if __name__ == "__main__":
    unittest.main(verbosity=2)
