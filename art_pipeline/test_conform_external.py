"""test_conform_external.py -- end-to-end proof of the conform pipeline.

Conforms a DETERMINISTIC synthetic source sheet (no external binary needed) and
asserts the output is palette-passing, ground-anchored, and manifest-valid under
the relaxed external lint profile -- the Part-D acceptance, reproducible in CI.

    python art_pipeline/test_conform_external.py
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

import conform_external
import lint

# Source grid: 32x32 cells, 4 rows (idle/walk/attack/death) x 3 cols.
SRC_CELL = 32
SRC_COLS = 3
SRC_ROWS = 4
# A 16w x 26h body in three horizontal greyscale bands -> three luminance
# levels after posterize; each band >= 8px tall so every mapped color region is
# well above the 3px orphan floor (orphan_pixels stays hard, so this matters).
BANDS = [
    (2, 10, (200, 200, 200)),   # light
    (11, 19, (130, 130, 130)),  # mid
    (20, 27, (60, 60, 60)),     # dark
]


def _build_source(path: str) -> None:
    sheet = Image.new("RGBA", (SRC_CELL * SRC_COLS, SRC_CELL * SRC_ROWS), (0, 0, 0, 0))
    px = sheet.load()
    for row in range(SRC_ROWS):
        for col in range(SRC_COLS):
            ox, oy = col * SRC_CELL, row * SRC_CELL
            for (y0, y1, grey) in BANDS:
                for y in range(y0, y1 + 1):
                    for x in range(8, 24):      # 16 px wide body
                        px[ox + x, oy + y] = (*grey, 255)
    sheet.save(path)


def _build_speckled_source(path: str) -> None:
    """A solid body PLUS isolated specks of a distinct luminance -- the shape
    real downscaled pixel art produces (thin staffs, glints, VFX motes) that the
    clean-band fixture never does. After posterize each speck maps to a different
    ramp index than the body -> a sub-3px orphan region. One speck sits inside
    the body (must be absorbed into the body color); one floats above it in
    transparency (must be deleted)."""
    sheet = Image.new("RGBA", (SRC_CELL * SRC_COLS, SRC_CELL * SRC_ROWS), (0, 0, 0, 0))
    px = sheet.load()
    for row in range(SRC_ROWS):
        for col in range(SRC_COLS):
            ox, oy = col * SRC_CELL, row * SRC_CELL
            for y in range(2, 28):              # solid 16x26 body, one grey
                for x in range(8, 24):
                    px[ox + x, oy + y] = (130, 130, 130, 255)
            px[ox + 15, oy + 14] = (245, 245, 245, 255)   # interior glint speck
            px[ox + 15, oy + 0] = (245, 245, 245, 255)    # floating mote (above body)
    sheet.save(path)


def _mapping() -> dict:
    return {
        "name": "synth_hero",
        "typeclass": "melee_biped",
        "element": "fire",          # metadata only; conform maps via materials
        "canvas": "32x32",
        "levels": 3,
        "materials": [{"ramp": "leather"}],   # single material -> one ramp
        "source_grid": {"frame_w": SRC_CELL, "frame_h": SRC_CELL},
        "rows": [
            {"animation": "idle",   "src_row": 0, "frames": 2, "fps": 7,  "loop": True},
            {"animation": "walk",   "src_row": 1, "frames": 2, "fps": 10, "loop": True},
            {"animation": "attack", "src_row": 2, "frames": 3, "fps": 12, "loop": False},
            {"animation": "death",  "src_row": 3, "frames": 2, "fps": 10, "loop": False},
        ],
        "foreswing_ticks": 8,
        "backswing_ticks": 12,
    }


# HARD checks that must pass on conformed external art (DR2: orphan_pixels and
# whitelist_cap stay hard; only no_aa/outline_1px/banding are demoted).
HARD = ["on_palette", "frames", "manifest_contract", "body_size",
        "orphan_pixels", "whitelist_cap"]


class ConformExternalTest(unittest.TestCase):
    def test_end_to_end_lint_passes(self):
        with tempfile.TemporaryDirectory() as d:
            src = str(Path(d) / "source.png")
            _build_source(src)
            result = conform_external.conform_external(_mapping(), src, outdir=d)

            self.assertTrue(Path(result["sheet"]).exists())
            self.assertTrue(Path(result["manifest"]).exists())

            report = lint.lint_sheet(result["sheet"], result["manifest"])
            self.assertEqual(report["profile"], "external")
            for name in HARD:
                self.assertTrue(report["checks"][name]["passed"],
                                f"hard check {name} failed: {report['checks'][name]}")
            self.assertTrue(report["passed"],
                            "conformed sheet must pass the external profile")

    def test_manifest_contract(self):
        with tempfile.TemporaryDirectory() as d:
            src = str(Path(d) / "source.png")
            _build_source(src)
            data = conform_external.conform_external(_mapping(), src, outdir=d)["data"]

            self.assertEqual(data["source"], "external")
            self.assertEqual(data["ground_y"], 29)            # 32 - 3
            self.assertEqual(data["frame_w"], 32)
            self.assertEqual(data["frame_h"], 32)
            attack = next(a for a in data["animations"] if a["name"] == "attack")
            self.assertIn("contact_frame", attack)
            self.assertTrue(0 <= attack["contact_frame"] < attack["frames"])
            # body landed in the melee_biped band (measured, not declared)
            self.assertGreaterEqual(data["body_size"]["height"], 24)
            self.assertGreaterEqual(data["body_size"]["width"], 14)

    def test_palette_compliance_by_construction(self):
        """Every conformed pixel is an exact Resurrect-64 ramp color."""
        with tempfile.TemporaryDirectory() as d:
            src = str(Path(d) / "source.png")
            _build_source(src)
            sheet = conform_external.conform_external(_mapping(), src, outdir=d)["sheet"]
            pal = conform_external.get_palette()
            img = Image.open(sheet).convert("RGBA")
            px = img.load()
            for y in range(img.size[1]):
                for x in range(img.size[0]):
                    r, g, bb, a = px[x, y]
                    if a == 0:
                        continue
                    self.assertIn((r, g, bb), pal.colors_rgb,
                                  f"off-palette pixel {(r, g, bb)} at {(x, y)}")

    def test_downscale_specks_are_denoised(self):
        """Real downscaled art fragments into sub-3px specks; the conform must
        despeckle so orphan_pixels (HARD in every profile) passes. The clean-band
        fixture never exercises this -- a speckled source is the adversarial case."""
        with tempfile.TemporaryDirectory() as d:
            src = str(Path(d) / "speckled.png")
            _build_speckled_source(src)
            result = conform_external.conform_external(_mapping(), src, outdir=d)
            report = lint.lint_sheet(result["sheet"], result["manifest"])
            orphan = report["checks"]["orphan_pixels"]
            self.assertTrue(orphan["passed"],
                            f"specks not denoised: {orphan['count']} orphans "
                            f"{orphan['offenders'][:3]}")
            self.assertTrue(report["passed"])

    def test_desaturated_pixel_routes_to_neutral(self):
        """Saturation guard: a near-white pixel (book pages, beard, highlights)
        has no meaningful hue and must NOT land on a chromatic skin ramp. With a
        neutral material declared, low-sat pixels route there; saturated skin
        still hue-matches. Without this the book fused into the face."""
        neutral = {"ramp": "mauve_grey", "neutral": True}
        chromatic = [{"ramp": "peach", "hue": 15}]
        self.assertIs(conform_external._material_for((230, 228, 227),
                                                     chromatic, neutral), neutral)
        self.assertIsNot(conform_external._material_for((187, 143, 130),
                                                        chromatic, neutral), neutral)

    def test_body_fill_override_beats_typeclass_band(self):
        """Resolution headroom: body_fill decouples the visual size target from
        the gameplay typeclass band, so a hero can fill a tall canvas instead of
        being crushed to the small-unit ~26px band."""
        with tempfile.TemporaryDirectory() as d:
            src = str(Path(d) / "source.png")
            _build_source(src)
            m = _mapping()
            m["canvas"] = "48x64"
            m["body_fill"] = 0.85
            data = conform_external.conform_external(m, src, outdir=d)["data"]
            self.assertGreater(data["body_size"]["height"], 40)   # not the ~26 band
            self.assertLessEqual(data["body_size"]["height"], data["ground_y"] + 1)

    def test_conform_is_deterministic(self):
        """Identical source + mapping => identical bytes (pipeline invariant)."""
        with tempfile.TemporaryDirectory() as d:
            src = str(Path(d) / "source.png")
            _build_source(src)
            out1, out2 = Path(d) / "a", Path(d) / "b"
            r1 = conform_external.conform_external(_mapping(), src, outdir=out1)
            r2 = conform_external.conform_external(_mapping(), src, outdir=out2)
            self.assertEqual(Path(r1["sheet"]).read_bytes(),
                             Path(r2["sheet"]).read_bytes())
            self.assertEqual(json.loads(Path(r1["manifest"]).read_text()),
                             json.loads(Path(r2["manifest"]).read_text()))


class AssembleExternalFramesTest(unittest.TestCase):
    """The frame assembler's deterministic subsample (Cethiel ships 161-301 frames
    per anim; the DW pipeline needs ~6-8 evenly spaced, endpoints included)."""

    def test_even_indices_endpoints_and_count(self):
        from assemble_external_frames import even_indices
        self.assertEqual(even_indices(161, 6), [0, 32, 64, 96, 128, 160])
        self.assertEqual(even_indices(301, 8), [0, 43, 86, 129, 171, 214, 257, 300])
        self.assertEqual(even_indices(5, 5), [0, 1, 2, 3, 4])
        self.assertEqual(even_indices(3, 6), [0, 1, 2])   # k >= n -> all frames
        self.assertEqual(even_indices(161, 1), [0])       # degenerate, no div-by-zero


if __name__ == "__main__":
    unittest.main(verbosity=2)
