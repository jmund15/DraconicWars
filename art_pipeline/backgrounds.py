"""backgrounds.py -- battle backgrounds + the Cinderfell Marches parallax set.

``generate_battle_background``: the flat 640x360 review backdrop used by
contact sheets (art-direction.md sections 6 and 9 rule 12).

``generate_cinderfell_set``: the first in-game parallax biome -- four layers
written to output/backgrounds/:
  cinderfell_sky.png     640x360  smooth dark gradient ink -> mauve_grey darks
                         + 3 soft cloud bands. The sky is the ONLY layer
                         allowed smooth gradients / soft blending (art doc
                         section 6); every other layer is flat ramp steps.
  cinderfell_far.png     640x140  jagged mountain/ruin silhouettes, 2 flat
                         dark ramp steps, transparent above.
  cinderfell_mid.png     640x110  rolling terrain band, restrained detail,
                         transparent above.
  cinderfell_ground.png  640x80   battlefield strip: ground texture +
                         scattered rocks / banners / bones, transparent above.
All layers stay in the darkest ramp thirds so units pop.

Deterministic: edges wave with fixed sine/triangle formulas, scatter uses
modular arithmetic -- no randomness anywhere.

CLI:
    python art_pipeline/backgrounds.py [--out PATH] [--cinderfell]
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


# ===========================================================================
# Cinderfell Marches parallax set
# ===========================================================================

CINDERFELL_DIR = DEFAULT_OUT.parent


def _lerp(c0, c1, t):
    t = max(0.0, min(1.0, t))
    return tuple(round(c0[i] + (c1[i] - c0[i]) * t) for i in range(3))


def _cinderfell_sky(pal, w=640, h=360):
    """Smooth vertical gradient (allowed on this layer only) + 3 soft cloud
    bands with gaussian falloff and sine-wavy centerlines."""
    ink0, ink1 = pal.get("ink", 0), pal.get("ink", 1)
    mg0 = pal.get("mauve_grey", 0)
    cloud = pal.get("mauve_grey", 1)
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        c = _lerp(ink0, ink1, t / 0.55) if t < 0.55 else _lerp(ink1, mg0, (t - 0.55) / 0.45)
        for x in range(w):
            px[x, y] = (*c, 255)
    bands = [(84, 7, 11, 0.42, 0.011, 0.0),
             (150, 9, 14, 0.34, 0.008, 2.1),
             (215, 6, 9, 0.26, 0.013, 4.4)]
    for band, (cy0, amp, thick, strength, freq, phase) in enumerate(bands):
        for x in range(w):
            cy = cy0 + amp * math.sin(x * freq + phase) + 3 * math.sin(x * 0.031 + band)
            for dy in range(-2 * thick, 2 * thick + 1):
                y = int(cy) + dy
                if not 0 <= y < h:
                    continue
                g = math.exp(-(dy / thick) ** 2)
                if g < 0.05:
                    continue
                base = px[x, y][:3]
                px[x, y] = (*_lerp(base, cloud, strength * g), 255)
    return img


def _cinderfell_far(pal, w=640, h=140):
    """Two flat dark steps of jagged mountain silhouettes; ruin towers with
    broken crenellation rise off the back ridge. Transparent above."""
    back_c = pal.get("frost", 0)     # 323353 dark slate
    front_c = pal.get("shadow", 0)   # 45293f dark mauve
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()

    def ridge_y(x, base, seed):
        # two desynced triangle waves: a single sawtooth reads metronomic
        tri = abs(((x * 0.043 + seed) % 2.0) - 1.0)
        tri2 = abs(((x * 0.0127 + seed * 2.3) % 2.0) - 1.0)
        return int(base + 14 * math.sin(x * 0.017 + seed) + 15 * (tri - 0.5)
                   + 13 * (tri2 - 0.5) + 5 * math.sin(x * 0.052 + seed * 1.7))

    for x in range(w):
        yb = ridge_y(x, 52, 1.3)
        yf = ridge_y(x, 92, 4.1)
        for y in range(h):
            if y >= yf:
                px[x, y] = (*front_c, 255)
            elif y >= yb:
                px[x, y] = (*back_c, 255)
    for i, tx in enumerate((96, 318, 522)):
        tw_ = 14 + 4 * (i % 2)
        top = max(6, ridge_y(tx, 52, 1.3) - (26 + 6 * (i % 2)))
        for x in range(tx, min(tx + tw_, w)):
            notch = 4 if ((x - tx) // 3) % 2 == 0 else 0  # broken crenellation
            for y in range(top + notch, ridge_y(x, 52, 1.3)):
                if 0 <= y < h:
                    px[x, y] = (*back_c, 255)
    return img


def _cinderfell_mid(pal, w=640, h=110):
    """Rolling terrain band, restrained detail (sparse scrub dashes)."""
    base_c = pal.get("stone", 0)   # 313638
    lit_c = pal.get("stone", 1)    # 374e4a
    dark_c = pal.get("ink", 1)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()

    def top_y(x):
        return int(34 + 9 * math.sin(x * 0.012 + 0.7) + 5 * math.sin(x * 0.033 + 2.2)
                   + 3 * math.sin(x * 0.071))

    for x in range(w):
        yt = top_y(x)
        for y in range(max(0, yt), h):
            px[x, y] = (*(lit_c if y < yt + 4 else base_c), 255)
    for i in range(26):
        sx = (i * 97 + 31) % (w - 8)
        yt = top_y(sx)
        span = h - yt - 16
        if span <= 0:
            continue
        sy = yt + 10 + (i * 53) % span
        for d in range(3 + i % 3):
            if 0 <= sy < h:
                px[sx + d, sy] = (*dark_c, 255)
    return img


def _cinderfell_ground(pal, w=640, h=80):
    """The battlefield strip: textured ground + rocks, torn banners, bones."""
    base_c = pal.get("ink", 1)        # 3e3546
    edge_c = pal.get("ink", 2)        # 625565
    dark_c = pal.get("ink", 0)
    rock_c = pal.get("stone", 0)
    rock_lit = pal.get("stone", 1)
    bone_c = pal.get("mauve_grey", 1)  # 7f708a desaturated bone
    banner_c = pal.get("fire_dark", 0)  # 6e2727 dark torn crimson
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()

    def top_y(x):
        return 6 + int(2 * math.sin(x * 0.05) + 1.4 * math.sin(x * 0.013 + 1.1))

    for x in range(w):
        yt = top_y(x)
        for y in range(yt, h):
            px[x, y] = (*(edge_c if y < yt + 2 else base_c), 255)
    # ground texture dashes
    for i in range(70):
        sx = (i * 41 + 13) % (w - 5)
        sy = 14 + (i * 29) % (h - 20)
        c = rock_c if i % 3 else dark_c
        for d in range(2 + i % 3):
            px[sx + d, sy] = (*c, 255)
    # rock mounds (lit top row, dark mass)
    for i in range(9):
        rx = (i * 151 + 60) % (w - 16)
        ry = 26 + (i * 37) % (h - 40)
        rw = 5 + (i % 3) * 2
        rows = rw // 2
        for dy in range(rows):
            c = rock_lit if dy == rows - 1 else rock_c
            for dx in range(dy, rw - dy):
                px[rx + dx, ry - dy] = (*c, 255)
    # bones: long bones and 2x2 skulls
    for i in range(6):
        bx = (i * 199 + 117) % (w - 10)
        by = 30 + (i * 61) % (h - 38)
        if i % 2:
            for d in range(4):
                px[bx + d, by] = (*bone_c, 255)
            px[bx, by - 1] = (*bone_c, 255)
            px[bx + 3, by + 1] = (*bone_c, 255)
        else:
            for dx in range(2):
                for dy in range(2):
                    px[bx + dx, by + dy] = (*bone_c, 255)
            px[bx + 2, by] = (*bone_c, 255)
    # torn war banners on dark poles, rising above the strip edge
    for i, bx in enumerate((120, 354, 540)):
        base_y = top_y(bx) + 10 + (i * 7) % 8
        pole_top = max(1, base_y - 14)
        for y in range(pole_top, base_y + 1):
            px[bx, y] = (*dark_c, 255)
        for dy in range(6):
            wdt = 6 - (dy // 2) - (1 if (dy + i) % 3 == 0 else 0)
            for dx in range(1, wdt + 1):
                if bx + dx < w:
                    px[bx + dx, pole_top + 1 + dy] = (*banner_c, 255)
    return img


def generate_cinderfell_set(outdir: str | Path | None = None) -> list[Path]:
    """Generate the 4 Cinderfell Marches parallax layers. Returns the paths."""
    pal = get_palette()
    outdir = Path(outdir) if outdir else CINDERFELL_DIR
    outdir.mkdir(parents=True, exist_ok=True)
    layers = [
        ("cinderfell_sky.png", _cinderfell_sky(pal)),
        ("cinderfell_far.png", _cinderfell_far(pal)),
        ("cinderfell_mid.png", _cinderfell_mid(pal)),
        ("cinderfell_ground.png", _cinderfell_ground(pal)),
    ]
    paths = []
    for name, img in layers:
        p = outdir / name
        img.save(p)
        paths.append(p)
    return paths


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Generate battle backgrounds.")
    ap.add_argument("--out", default=None, help=f"output path (default {DEFAULT_OUT})")
    ap.add_argument("--cinderfell", action="store_true",
                    help="also generate the Cinderfell Marches parallax set")
    args = ap.parse_args(argv)
    out = generate_battle_background(args.out)
    print(f"background: {out}")
    if args.cinderfell:
        for p in generate_cinderfell_set():
            print(f"parallax layer: {p}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
