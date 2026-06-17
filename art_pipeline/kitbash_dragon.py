"""kitbash_dragon.py -- strategy B of the Hero A/B/C bake-off.

Hand-authored part-library kit-bash: a small set of dragon parts are pixel-authored
here as luminance-toned ASCII grids (tone 1=dark .. 5=light, ' '=transparent), then
a compositor stamps them into per-animation source frames with simple per-frame
transforms (wing beat, body bob, head/tail lift, attack lunge, death collapse).

The result is emitted as ONE gridded source sheet and run through the SAME
conform_external "house treatment" as the curated strategy C -- so the only thing
that varies between B and C is the SOURCE art (hand-authored vs curated-CC0), which
is exactly what the bake-off measures. No house-treatment / palettize / anchor /
manifest code is duplicated here (conform owns all of it).

CLI:
    python art_pipeline/kitbash_dragon.py            # -> writes the source sheet
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image

# Luminance ramp for the authored tones -> conform posterizes these onto the fire
# ramp (dark tone -> dark fire step, light tone -> bright fire step).
TONE = {"1": 40, "2": 95, "3": 150, "4": 200, "5": 240}

# --- hand-authored parts (facing RIGHT) ------------------------------------
# CORE = one CONNECTED wyvern silhouette: tail (left) -> body -> haunch + two
# legs -> neck -> horned head + snout (right). The animated near-wing overlays it.
# 5=accent (horn tip / ember), 4=light body, 3=mid, 2=belly/shade.
CORE = [
    "                                        5         ",
    "                                   5   441        ",
    "                                    5 4441         ",
    "                                     4441          ",
    "                                  44444441         ",
    "                                4444444441         ",
    "                               444444444 1         ",
    "                              4444444444441        ",
    "                  4444       444444444 5           ",
    "                444444444  44444444441             ",
    "              444444444444444444444441             ",
    "            4444444444444444444444441              ",
    "      4   444444444444444444444444441              ",
    "     444444444444444444444444444444441             ",
    "    44444444444444444444444444444444 1             ",
    "   4444444444444444444444444444444441              ",
    "   3444444444444444444444444444444331              ",
    "    3344444444444444444444444444331                ",
    "      33344444444444444444444331                   ",
    "         333333344444444433321                     ",
    "             4443 4443 44433                        ",
    "             444  444  4441                         ",
    "             343  343  341                          ",
    "             333  333  331                          ",
]

# WING (membrane), spread; stamped near (solid) + far (1 tone darker, offset).
WING = [
    "        111         ",
    "      11222111      ",
    "    1122222221      ",
    "   122222222221     ",
    "  12222222222 21    ",
    " 122222222222  1    ",
    " 1222222222221      ",
    "12222222222221      ",
    "12222222222 1       ",
    "1222222222 1        ",
    "12222222 21         ",
    " 122222 1           ",
    "  12221             ",
    "   111              ",
]

# TAIL, tapering left, ember tip (tone 5) -- conform maps tip to the bright step.
TAIL = [
    "                       111  ",
    "                    1112221  ",
    "  1             1112224445   ",
    " 1 1        11122244445551   ",
    "1 1 1  111224444444411       ",
    " 11122244444444411          ",
    "  11111111111111            ",
]

# HIND LEG + clawed foot.
LEG = [
    " 1221 ",
    " 1441 ",
    " 1441 ",
    " 1441 ",
    "114411",
    "144441",
    "111111",
]


def _grid(part):
    return [list(row) for row in part]


def _stamp(canvas, part, ox, oy, tone_shift=0, flip=False):
    """Blit a part's non-space cells onto a 2D tone canvas at (ox, oy)."""
    rows = part[::1]
    for ry, row in enumerate(rows):
        cells = row[::-1] if flip else row
        for cx, ch in enumerate(cells):
            if ch == " ":
                continue
            t = int(ch) + tone_shift
            t = max(1, min(5, t))
            x, y = ox + cx, oy + ry
            if 0 <= y < len(canvas) and 0 <= x < len(canvas[0]):
                canvas[y][x] = str(t)


def _blank(w, h):
    return [[" "] * w for _ in range(h)]


def compose_frame(pose, W=120, H=104):
    """Stamp the kit into one tone-canvas frame: far wing -> connected core ->
    near wing. Per-frame transforms: bob (body), wing (beat), lunge (attack)."""
    c = _blank(W, H)
    bob = pose.get("bob", 0)
    wing_dy = pose.get("wing", 0)
    lunge = pose.get("lunge", 0)
    cx, cy = 30 + lunge, 28 + bob
    # far wing behind the body (1 tone darker, raised + offset for depth)
    _stamp(c, WING, cx + 20, cy - 4 + wing_dy // 2, tone_shift=-1)
    # connected wyvern silhouette (tail/legs/neck/head integrated)
    _stamp(c, CORE, cx, cy)
    # near wing over the body's upper back
    _stamp(c, WING, cx + 14, cy - 6 + wing_dy)
    return c


def _to_image(canvas):
    h, w = len(canvas), len(canvas[0])
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            ch = canvas[y][x]
            if ch != " ":
                v = TONE[ch]
                px[x, y] = (v, v, v, 255)
    return img


# Per-animation poses (kit-bash "animation": simple part transforms).
ANIMS = {
    "idle": [{"wing": -2, "bob": 0}, {"wing": 0, "bob": 0},
             {"wing": 2, "bob": 1}, {"wing": 1, "bob": 1},
             {"wing": -1, "bob": 1}, {"wing": -2, "bob": 0}],
    "fly":  [{"wing": -6, "bob": -1}, {"wing": -2, "bob": 0}, {"wing": 3, "bob": 1},
             {"wing": 6, "bob": 2}, {"wing": 2, "bob": 1}, {"wing": -4, "bob": 0}],
    "attack": [{"wing": -5, "head_dx": -2, "bob": -1}, {"wing": -7, "head_dx": -3, "bob": -2},
               {"wing": 3, "head_dx": 3, "lunge": 4, "bob": 1}, {"wing": 0, "head_dx": 1, "lunge": 2},
               {"wing": -3, "bob": 0}, {"wing": -4, "bob": 0}, {"wing": -2, "bob": 1}],
    "death": [{"wing": 4, "bob": 1, "tail": 1}, {"wing": 6, "bob": 3, "tail": 2},
              {"wing": 7, "bob": 6, "tail": 4}, {"wing": 8, "bob": 9, "tail": 6},
              {"wing": 8, "bob": 12, "tail": 8}, {"wing": 8, "bob": 14, "tail": 9},
              {"wing": 8, "bob": 15, "tail": 9}, {"wing": 8, "bob": 15, "tail": 9}],
}
ROWS = ["idle", "fly", "attack", "death"]


def build_source(out_path, W=120, H=104):
    """Compose all frames into one gridded source sheet for conform_external."""
    max_frames = max(len(ANIMS[a]) for a in ROWS)
    sheet = Image.new("RGBA", (W * max_frames, H * len(ROWS)), (0, 0, 0, 0))
    for ri, anim in enumerate(ROWS):
        for ci, pose in enumerate(ANIMS[anim]):
            sheet.alpha_composite(_to_image(compose_frame(pose, W, H)), (ci * W, ri * H))
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)
    return {"cell_w": W, "cell_h": H, "frames_per_row": [len(ANIMS[a]) for a in ROWS]}


if __name__ == "__main__":
    here = Path(__file__).resolve().parent
    out = here / "kitbash" / "kitbash_dragon_source.png"
    info = build_source(out)
    print(f"sheet: {out}")
    print(f"cell:  {info['cell_w']}x{info['cell_h']}  frames/row: {info['frames_per_row']}")
