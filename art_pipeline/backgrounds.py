"""backgrounds.py -- one placeholder battle background (640x360).

Four horizontal bands from the dark thirds of the palette ramps
(palette.json ``background_rule``): dusk sky gradient (ink -> mauve_grey
darks), far ridge silhouettes, mid terrain, and the battlefield ground strip.
Deliberately simple -- it exists so contact sheets composite over a REAL
backdrop instead of a void (art-direction.md sections 6 and 9 rule 12).

Deterministic: band edges wave with fixed sine formulas, no randomness.

CLI:
    python art_pipeline/backgrounds.py [--out PATH]
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

from PIL import Image

from palette import get_palette

DEFAULT_OUT = Path(__file__).resolve().parent / "output" / "backgrounds" / "battle_bg.png"

W, H = 640, 360
GROUND_TOP = 300  # battlefield strip; contact sheets stand units around here


def generate_battle_background(out_path: str | Path | None = None,
                               w: int = W, h: int = H) -> Path:
    pal = get_palette()
    sky = [pal.get("ink", 0), pal.get("ink", 1), pal.get("ink", 2), pal.get("mauve_grey", 0)]
    ridge_far = pal.get("frost", 0)       # 323353
    ridge_near = pal.get("shadow", 0)     # 45293f
    terrain = pal.get("stone", 0)         # 313638
    terrain_lit = pal.get("stone", 1)     # 374e4a
    ground = pal.get("ink", 1)            # 3e3546
    ground_edge = pal.get("ink", 2)       # 625565
    scatter = pal.get("shadow", 0)

    img = Image.new("RGB", (w, h), sky[0])
    px = img.load()

    sky_bands = [int(h * 0.17), int(h * 0.30), int(h * 0.40)]
    for x in range(w):
        wave = int(2 * math.sin(x * 0.045))
        ridge1 = int(h * 0.36) + int(11 * math.sin(x * 0.018) + 6 * math.sin(x * 0.047 + 2.1))
        ridge2 = int(h * 0.46) + int(7 * math.sin(x * 0.026 + 0.8) + 4 * math.sin(x * 0.061 + 1.3))
        terr = int(h * 0.56) + int(5 * math.sin(x * 0.021 + 0.4) + 3 * math.sin(x * 0.057 + 2.9))
        for y in range(h):
            if y >= GROUND_TOP:
                c = ground_edge if y < GROUND_TOP + 2 else ground
            elif y >= terr:
                c = terrain_lit if y < terr + 6 else terrain
            elif y >= ridge2:
                c = ridge_near
            elif y >= ridge1:
                c = ridge_far
            elif y >= sky_bands[2] + wave:
                c = sky[3]
            elif y >= sky_bands[1] + wave:
                c = sky[2]
            elif y >= sky_bands[0] + wave:
                c = sky[1]
            else:
                c = sky[0]
            px[x, y] = c

    # sparse deterministic ground scatter (pebble dashes)
    for i in range(36):
        sx = (i * 53 + 17) % (w - 6)
        sy = GROUND_TOP + 8 + (i * 37) % (h - GROUND_TOP - 12)
        for d in range(2 + (i % 2)):
            px[sx + d, sy] = scatter

    out = Path(out_path) if out_path else DEFAULT_OUT
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    return out


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Generate the placeholder battle background.")
    ap.add_argument("--out", default=None, help=f"output path (default {DEFAULT_OUT})")
    args = ap.parse_args(argv)
    out = generate_battle_background(args.out)
    print(f"background: {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
