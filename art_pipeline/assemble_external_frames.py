"""assemble_external_frames.py -- pack a per-animation external frame library
into a single gridded source sheet for conform_external.py.

Some curated sources (e.g. Cethiel's CC0 "Dragon - Fully Animated") ship as
separate per-frame PNGs in one folder PER animation, at a high frame rate
(161-301 frames each) and high resolution. conform_external expects ONE source
sheet with a uniform grid (source_grid + src_row x frames). This assembler:

  1. evenly subsamples K frames per chosen folder (endpoints included),
  2. optionally horizontal-flips (source faces left, DW faces right),
  3. packs them one-animation-per-row into a uniform-cell grid sheet.

conform_external then bbox-crops + posterizes + ramp-maps + re-anchors each cell,
so the assembler does NOT need to crop or rescale -- it only selects + arranges.

Determinism: even_indices is a pure integer function -> identical inputs give an
identical sheet (the conform pipeline's byte-stability contract holds upstream).

CLI:
    python art_pipeline/assemble_external_frames.py <spec.json> [--outdir DIR]

spec.json:
    {
      "zip": "external/sources/cethiel_dragon/cethiel_dragon_pack.zip",
      "prefix": "Dragon - Fully Animated/",
      "flip_h": true,
      "out": "external/sources/cethiel_dragon/cethiel_dragon_source.png",
      "rows": [
        {"folder": "Idle",     "frames": 6},
        {"folder": "Walking",  "frames": 6},
        {"folder": "Attack 1", "frames": 7},
        {"folder": "Death",    "frames": 8}
      ]
    }
"""

from __future__ import annotations

import argparse
import io
import json
import sys
import zipfile
from pathlib import Path

from PIL import Image


def even_indices(n: int, k: int) -> list[int]:
    """K evenly spaced indices in [0, n) with both endpoints included.

    k >= n -> every frame; k <= 1 -> [0] (degenerate, no divide-by-zero)."""
    if k >= n:
        return list(range(n))
    if k <= 1:
        return [0]
    return [round(i * (n - 1) / (k - 1)) for i in range(k)]


def assemble(zip_path: str | Path, prefix: str, rows: list[dict],
             out_path: str | Path, flip_h: bool = False) -> dict:
    """Subsample + grid-pack the per-folder frames into one sheet at out_path.
    Returns {cell_w, cell_h, frames_per_row, rows, sheet}."""
    z = zipfile.ZipFile(zip_path)
    names = z.namelist()
    picked: list[list[Image.Image]] = []
    cw = ch = 0
    for r in rows:
        folder = r["folder"]
        files = sorted(n for n in names
                       if n.startswith(f"{prefix}{folder}/") and n.lower().endswith(".png"))
        if not files:
            raise ValueError(f"no PNG frames under '{prefix}{folder}/' in {zip_path}")
        idxs = even_indices(len(files), int(r["frames"]))
        imgs = []
        for i in idxs:
            im = Image.open(io.BytesIO(z.read(files[i]))).convert("RGBA")
            if flip_h:
                im = im.transpose(Image.FLIP_LEFT_RIGHT)
            imgs.append(im)
            cw, ch = max(cw, im.width), max(ch, im.height)
        picked.append(imgs)

    max_frames = max(len(imgs) for imgs in picked)
    sheet = Image.new("RGBA", (cw * max_frames, ch * len(rows)), (0, 0, 0, 0))
    for ri, imgs in enumerate(picked):
        for ci, im in enumerate(imgs):
            sheet.paste(im, (ci * cw, ri * ch))   # top-left aligned; conform finds the bbox
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)
    return {"cell_w": cw, "cell_h": ch, "frames_per_row": [len(i) for i in picked],
            "rows": [r["folder"] for r in rows], "sheet": str(out_path)}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Assemble external per-anim frame folders into a grid sheet.")
    ap.add_argument("spec", help="path to an assemble spec .json")
    args = ap.parse_args(argv)
    spec = json.loads(Path(args.spec).read_text(encoding="utf-8"))
    base = Path(args.spec).resolve().parent
    zp = Path(spec["zip"])
    if not zp.is_absolute():
        zp = base / zp
    out = Path(spec["out"])
    if not out.is_absolute():
        out = base / out
    info = assemble(zp, spec["prefix"], spec["rows"], out, bool(spec.get("flip_h", False)))
    print(f"sheet:  {info['sheet']}")
    print(f"cell:   {info['cell_w']}x{info['cell_h']}  frames/row: {info['frames_per_row']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
