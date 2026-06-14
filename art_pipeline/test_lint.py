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


if __name__ == "__main__":
    unittest.main(verbosity=2)
