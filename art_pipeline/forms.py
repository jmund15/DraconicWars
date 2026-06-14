"""forms.py -- element attack-form CORE generator (Attack Archetype System).

A "form" is the cosmetic projectile a Magic-archetype unit conjures: a baked
pixel-art core (palette-contract, element-tinted) the view layer spawns at the
caster and tweens to the target (arch-attack-archetypes.md §5). Parallel to the
body-plan template family in skeletons.py -- a FormTemplate satisfies the same
kind of structural contract (canvas / frames() / draw_frame()), so forms render
through the SAME finalize (sel-out outline + shading) + palette pipeline units
use; only the buffer-filling differs.

Frame kinds: spawn (conjure) -> travel (looping flight) -> impact (burst). The
shape (shard / ball / chunk / bolt) is the silhouette language; the element ramp
tints it. Colors come ONLY from palette.json via the element's primary ramp (no
computed shades) -- the same contract units obey. Determinism: identical
(element, shape) => identical bytes.

CLI: python art_pipeline/forms.py [--only <element>_<shape>]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

import skeletons
from palette import Palette, get_palette

ROOT = Path(__file__).resolve().parent

FORM_SHAPES = ("shard", "ball", "chunk", "bolt")
FRAME_KINDS = ("spawn", "travel", "impact")

# The canonical form each element conjures (mirrors the UnitDef.Attack.Form the
# catalog assigns its casters). The generator is general -- any element x shape
# pair renders -- this is just the v1 set the magic roster actually spawns.
CANONICAL = (
    ("fire", "ball"),
    ("frost", "shard"),
    ("venom", "ball"),
    ("stone", "chunk"),
    ("storm", "bolt"),
)


def resolve_form_colors(element: str, pal: Palette) -> dict[str, tuple[str, int]]:
    """Element primary ramp -> core / hot / edge steps (palette-contract)."""
    primary = pal.element_primary(element)
    return {
        "core": (primary, pal.clamp_index(primary, 2)),
        "hot": (primary, pal.clamp_index(primary, 4)),
        "edge": (primary, pal.clamp_index(primary, 1)),
    }


class FormTemplate:
    """A projectile core. ``shape`` selects the silhouette language; ``draw_frame``
    branches on it (the same config-driven-branch idiom as BipedConfig.build /
    ConstructTemplate.construct_style). Faces right (travels +x), like units."""

    canvas = (16, 16)

    def __init__(self, shape: str):
        if shape not in FORM_SHAPES:
            raise KeyError(f"unknown form shape '{shape}'; available: {FORM_SHAPES}")
        self.shape = shape

    def frames(self) -> list[str]:
        return list(FRAME_KINDS)

    def draw_frame(self, buf, kind: str, colors: dict, pal: Palette) -> None:
        cx, cy = self.canvas[0] // 2 - 1, self.canvas[1] // 2
        getattr(self, f"_draw_{self.shape}")(buf, kind, cx, cy, colors)

    # ----------------------------------------------------------- shapes
    def _draw_shard(self, buf, kind, cx, cy, c):
        cr, ci = c["core"]
        hr, hi = c["hot"]
        if kind == "spawn":
            buf.fill_triangle((cx - 1, cy - 2), (cx + 3, cy), (cx - 1, cy + 2), cr, ci, part="form")
        elif kind == "travel":
            # pointed crystal aimed right: wide base left, apex right + a lit facet
            buf.fill_triangle((cx - 3, cy - 3), (cx + 5, cy), (cx - 3, cy + 3), cr, ci, part="form")
            buf.fill_triangle((cx - 3, cy - 3), (cx + 5, cy), (cx, cy - 1), hr, hi, part="form")
        else:  # impact: a radiating shard burst
            for dx, dy in ((-5, 0), (5, 0), (0, -5), (0, 5), (-3, -3), (3, 3), (3, -3), (-3, 3)):
                buf.line(cx, cy, cx + dx, cy + dy, hr, hi, part="form")

    def _draw_ball(self, buf, kind, cx, cy, c):
        cr, ci = c["core"]
        hr, hi = c["hot"]
        if kind == "spawn":
            buf.fill_ellipse(cx, cy, 2, 2, cr, ci, part="form")
        elif kind == "travel":
            buf.fill_ellipse(cx, cy, 4, 4, cr, ci, part="form")
            buf.fill_ellipse(cx + 1, cy - 1, 2, 2, hr, hi, part="form")  # hot core, light side
        else:  # impact: a wider splash + bright center
            buf.fill_ellipse(cx, cy, 6, 5, cr, ci, part="form")
            buf.fill_ellipse(cx, cy, 3, 2, hr, hi, part="form")

    def _draw_chunk(self, buf, kind, cx, cy, c):
        cr, ci = c["core"]
        er, ei = c["edge"]
        if kind == "spawn":
            buf.fill_rect(cx - 1, cy - 1, cx + 1, cy + 1, cr, ci, part="form")
        elif kind == "travel":
            buf.fill_rect(cx - 3, cy - 2, cx + 3, cy + 3, cr, ci, part="form")
            buf.fill_rect(cx - 2, cy - 3, cx + 1, cy - 2, cr, ci, part="form")  # bumpy top
            buf.fill_rect(cx - 3, cy + 2, cx + 3, cy + 3, er, ei, part="form")  # darker base facet
        else:  # impact: debris scatter
            for dx, dy in ((-5, 2), (-2, -3), (2, 4), (5, -1), (0, 3), (4, 2)):
                buf.fill_rect(cx + dx, cy + dy, cx + dx + 1, cy + dy + 1, cr, ci, part="form")

    def _draw_bolt(self, buf, kind, cx, cy, c):
        hr, hi = c["hot"]
        if kind == "spawn":
            buf.fill_rect(cx - 1, cy - 1, cx, cy, hr, hi, part="form", no_outline=True)
        elif kind == "travel":
            pts = [(cx - 5, cy - 1), (cx - 2, cy + 2), (cx + 1, cy - 2), (cx + 4, cy + 1), (cx + 6, cy - 1)]
            for i in range(len(pts) - 1):
                buf.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], hr, hi,
                         part="form", width=2, no_outline=True)
        else:  # impact: a zap star
            for dx, dy in ((-5, -2), (5, -2), (-4, 3), (4, 3), (0, -5), (0, 5)):
                buf.line(cx, cy, cx + dx, cy + dy, hr, hi, part="form", no_outline=True)


def generate_form(element: str, shape: str, outdir: str | Path | None = None,
                  pal: Palette | None = None) -> dict:
    """Render a <element>_<shape> form sheet (one row: spawn/travel/impact) +
    manifest. Mirrors generate_unit's finalize->render->composite pipeline."""
    pal = pal or get_palette()
    outdir = Path(outdir) if outdir else (ROOT / "output" / "forms")
    outdir.mkdir(parents=True, exist_ok=True)
    tpl = FormTemplate(shape)
    colors = resolve_form_colors(element, pal)
    protected = {pal.hex_of(*colors["hot"])}  # keep the hot core bright (shading-exempt)
    W, H = tpl.canvas
    kinds = tpl.frames()
    sheet = Image.new("RGBA", (W * len(kinds), H), (0, 0, 0, 0))
    ramps: set[str] = set()
    for col, kind in enumerate(kinds):
        buf = skeletons.PixelBuffer(W, H)
        tpl.draw_frame(buf, kind, colors, pal)
        skeletons.finalize(buf, pal, protected)
        ramps |= buf.ramps_used()
        sheet.alpha_composite(skeletons.render(buf, pal), (col * W, 0))
    name = f"{element}_{shape}"
    sheet_path = outdir / f"{name}_sheet.png"
    sheet.save(sheet_path)
    manifest = {
        "name": name,
        "kind": "form",
        "shape": shape,
        "element": element,
        "frame_w": W,
        "frame_h": H,
        "frames": kinds,
        "loop_frame": kinds.index("travel"),
        "ramps_used": sorted(ramps),
    }
    manifest_path = outdir / f"{name}.manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    return {"sheet": str(sheet_path), "manifest": str(manifest_path), "data": manifest}


def lint_form(result: dict, pal: Palette) -> list[str]:
    """Frame-kind contract + palette-only. Returns a list of errors ([] = pass)."""
    errs = []
    m = result["data"]
    if list(m["frames"]) != list(FRAME_KINDS):
        errs.append(f"{m['name']}: frames {m['frames']} != {list(FRAME_KINDS)}")
    if not (0 <= m["loop_frame"] < len(m["frames"])):
        errs.append(f"{m['name']}: loop_frame {m['loop_frame']} out of range")
    for ramp in m["ramps_used"]:
        if ramp not in pal.ramps:
            errs.append(f"{m['name']}: non-palette ramp '{ramp}'")
    return errs


def run(only: str | None = None) -> int:
    pal = get_palette()
    print(f"{'form':<18} {'lint':<5} {'errors'}")
    print("-" * 60)
    ok = True
    for element, shape in CANONICAL:
        name = f"{element}_{shape}"
        if only and name != only:
            continue
        res = generate_form(element, shape, pal=pal)
        errs = lint_form(res, pal)
        ok = ok and not errs
        print(f"{name:<18} {'PASS' if not errs else 'FAIL':<5} {'; '.join(errs)}")
    return 0 if ok else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Generate element attack-form cores.")
    ap.add_argument("--only", default=None, help="single <element>_<shape>")
    return run(ap.parse_args(argv).only)


if __name__ == "__main__":
    sys.exit(main())
