"""skeletons.py -- the 6 parametric TypeClass body templates.

Templates: melee_biped, ranged_biped, sniper_biped, aerial_flyer,
siege_machine, support_robed.

Contract (art-direction.md sections 4, 5, 9):
- Bottom-center pivot; feet on a ground line 2 px above the canvas bottom
  (last opaque row = ``H - 3``). Aerial units hover above that line.
- All units authored facing RIGHT.
- One global light source, top-left ~45 deg, hard-coded in the shading pass.
- Pose keyframes for the first-playable budget: idle 2, walk 4 (aerial: fly 6),
  attack 4 with a designated contact frame, death 3.
- Body parts are drawn pixel-cluster primitives. Shadow = one ramp step down on
  the away-from-light side, highlight = one step up on lit edges, sel-out
  outline = darkest ramp step on lit edges and ink #2e222f on bottom/shadow
  edges, uniform 1 px.

Poses are parameterized part positions (head / torso / arms / legs or wings /
prop anchor), so units are template INSTANCES, not bespoke drawings.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, replace
from typing import Protocol, runtime_checkable

import props as props_mod
from palette import Palette

GROUND_MARGIN = 2          # empty rows below the feet line
LIGHT_SOURCE = "top-left 45deg"  # global; baked into outline + shading passes
INK = ("ink", 0)           # near-black #2e222f -- reserved for outline / eyes


def ground_row(canvas_h: int) -> int:
    """Last opaque row for a grounded unit (feet line)."""
    return canvas_h - GROUND_MARGIN - 1


@runtime_checkable
class BodyPlanTemplate(Protocol):
    """The structural contract every creature body-plan template satisfies -- the
    exact surface generate_unit consumes (``canvas``, ``ROLE_DEFAULTS``,
    ``animations()``, ``poses()``, ``draw_pose()``). Runtime-inert: the incumbent
    templates conform by duck-typing and need NOT inherit it (subclassing a
    structural Protocol is the strictly-safer byte-identity path). A new form is
    checked against this contract by test_templates.py BEFORE generate_unit can
    build it -- the 'determined upfront, expand as needed' guarantee for the
    template family."""

    ROLE_DEFAULTS: dict
    canvas: tuple[int, int]

    def animations(self) -> list: ...

    def poses(self, anim: str, contact_idx: int) -> list[dict]: ...

    def draw_pose(self, buf, pose: dict, unit: dict, pal) -> None: ...


# ===========================================================================
# Pixel buffer + primitives
# ===========================================================================

@dataclass
class Cell:
    ramp: str
    idx: int
    part: str
    no_outline: bool = False  # thin props (shafts, straps) keep their own color
    volume: bool = False      # big-tier internal-volume shading (lint-exempt)


class PixelBuffer:
    """A canvas of (ramp, index) cells -- colors stay symbolic until render."""

    def __init__(self, w: int, h: int):
        self.w = w
        self.h = h
        self.cells: dict[tuple[int, int], Cell] = {}

    # ------------------------------------------------------------ plotting

    def set_px(self, x, y, ramp, idx, part="body", no_outline=False):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.cells[(x, y)] = Cell(ramp, idx, part, no_outline)

    def opaque(self, x, y) -> bool:
        return (x, y) in self.cells

    def fill_rect(self, x0, y0, x1, y1, ramp, idx, part="body", no_outline=False):
        for y in range(min(y0, y1), max(y0, y1) + 1):
            for x in range(min(x0, x1), max(x0, x1) + 1):
                self.set_px(x, y, ramp, idx, part, no_outline)

    def fill_round_rect(self, x0, y0, x1, y1, ramp, idx, part="body"):
        """Rect with the 4 corner pixels skipped (when big enough).

        Corners are skipped, not erased -- pixels another part already drew
        there survive (erasing would punch holes through the body).
        """
        corners = set()
        if x1 - x0 >= 3 and y1 - y0 >= 3:
            corners = {(x0, y0), (x1, y0), (x0, y1), (x1, y1)}
        for y in range(min(y0, y1), max(y0, y1) + 1):
            for x in range(min(x0, x1), max(x0, x1) + 1):
                if (x, y) not in corners:
                    self.set_px(x, y, ramp, idx, part)

    def limb(self, x0, y0, x1, y1, ramp, idx, part="body", bend="vertical"):
        """2 px-wide bent limb (thigh+shin / upper+forearm).

        Axis-aligned segments instead of a diagonal line: a 2 px diagonal is
        100% silhouette boundary and the sel-out pass would ink it solid.
        ``bend='vertical'`` drops from (x0,y0) then steps across to (x1,y1)
        (legs); ``bend='horizontal'`` goes across first (arms).
        """
        if bend == "vertical":
            mid = (y0 + y1) // 2
            self.fill_rect(x0, y0, x0 + 1, mid, ramp, idx, part)
            self.fill_rect(x1, mid, x1 + 1, y1, ramp, idx, part)
            if abs(x1 - x0) > 1:
                self.fill_rect(min(x0, x1), mid, max(x0, x1) + 1, mid + 1, ramp, idx, part)
        else:
            mid = (x0 + x1) // 2
            self.fill_rect(x0, y0, mid + 1, y0 + 1, ramp, idx, part)
            self.fill_rect(mid, min(y0, y1), mid + 1, y1, ramp, idx, part)
            self.fill_rect(mid, y1, x1, y1 + 1, ramp, idx, part)

    def fill_ellipse(self, cx, cy, rx, ry, ramp, idx, part="body"):
        rx_f, ry_f = rx + 0.45, ry + 0.45
        for y in range(int(cy - ry), int(cy + ry) + 1):
            for x in range(int(cx - rx), int(cx + rx) + 1):
                if ((x - cx) / rx_f) ** 2 + ((y - cy) / ry_f) ** 2 <= 1.0:
                    self.set_px(x, y, ramp, idx, part)

    def fill_triangle(self, p0, p1, p2, ramp, idx, part="body"):
        xs = [p0[0], p1[0], p2[0]]
        ys = [p0[1], p1[1], p2[1]]

        def edge(a, b, p):
            return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])

        area = edge(p0, p1, p2)
        if area == 0:
            self.line(min(xs), ys[xs.index(min(xs))], max(xs), ys[xs.index(max(xs))], ramp, idx, part)
            return
        for y in range(min(ys), max(ys) + 1):
            for x in range(min(xs), max(xs) + 1):
                w0, w1, w2 = edge(p1, p2, (x, y)), edge(p2, p0, (x, y)), edge(p0, p1, (x, y))
                if area < 0:
                    w0, w1, w2 = -w0, -w1, -w2
                if w0 >= 0 and w1 >= 0 and w2 >= 0:
                    self.set_px(x, y, ramp, idx, part)

    def line(self, x0, y0, x1, y1, ramp, idx, part="body", width=1, no_outline=False):
        """Bresenham; width 2 thickens perpendicular to the dominant axis."""
        dx, dy = abs(x1 - x0), abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        horizontal = dx >= dy
        err = dx - dy
        x, y = x0, y0
        while True:
            self.set_px(x, y, ramp, idx, part, no_outline)
            if width >= 2:
                if horizontal:
                    self.set_px(x, y + 1, ramp, idx, part, no_outline)
                else:
                    self.set_px(x + 1, y, ramp, idx, part, no_outline)
            if x == x1 and y == y1:
                break
            e2 = 2 * err
            if e2 > -dy:
                err -= dy
                x += sx
            if e2 < dx:
                err += dx
                y += sy

    # ------------------------------------------------------------ queries

    def part_bboxes(self) -> dict[str, tuple[int, int, int, int]]:
        boxes: dict[str, list[int]] = {}
        for (x, y), c in self.cells.items():
            b = boxes.get(c.part)
            if b is None:
                boxes[c.part] = [x, y, x, y]
            else:
                b[0] = min(b[0], x); b[1] = min(b[1], y)
                b[2] = max(b[2], x); b[3] = max(b[3], y)
        return {k: tuple(v) for k, v in boxes.items()}

    def ramps_used(self) -> set[str]:
        return {c.ramp for c in self.cells.values()}

    def max_opaque_y(self) -> int:
        return max((y for (_, y) in self.cells), default=-1)


# ===========================================================================
# Finalize pipeline: sel-out outline -> directional shading -> cluster cleanup
# ===========================================================================

def apply_selout_outline(buf: PixelBuffer, pal: Palette) -> dict[tuple[int, int], str]:
    """Recolor silhouette-boundary pixels per the sel-out rule.

    Lit edges (transparent above/left only) -> darkest step of the pixel's own
    ramp. Shadow edges (transparent below or right) -> ink #2e222f. Light is
    top-left, so bottom/right wins when both apply. Returns {px: 'lit'|'shadow'}.
    """
    kinds: dict[tuple[int, int], str] = {}
    for (x, y), cell in buf.cells.items():
        if cell.no_outline:
            continue
        t_up = not buf.opaque(x, y - 1)
        t_left = not buf.opaque(x - 1, y)
        t_down = not buf.opaque(x, y + 1)
        t_right = not buf.opaque(x + 1, y)
        if not (t_up or t_left or t_down or t_right):
            continue
        kinds[(x, y)] = "shadow" if (t_down or t_right) else "lit"
    for (x, y), kind in kinds.items():
        cell = buf.cells[(x, y)]
        if kind == "shadow":
            cell.ramp, cell.idx = INK
        else:
            cell.idx = 0
    return kinds


def apply_directional_shading(buf: PixelBuffer, pal: Palette, outline: dict,
                              protected_hexes: set[str] = frozenset()) -> None:
    """One global light, top-left 45 deg.

    Highlight (+1 ramp step) on interior pixels hugging lit outline above/left,
    upper half of each part. Shadow (-1) hugging shadow outline below/right,
    lower half, plus a 1 px ambient-occlusion line under overlapping parts.
    Band depth staggers 1-2 px (middle third of each part) to avoid banding.
    Parts named ``back_*`` are pre-darkened and stay flat (depth cue).
    Shifts that would mint a ``protected_hexes`` color (eye / element accent --
    whitelist-capped at 6 px/frame) are skipped: deliberate-detail colors stay
    deliberate.
    """
    boxes = buf.part_bboxes()
    updates: dict[tuple[int, int], int] = {}
    for (x, y), cell in buf.cells.items():
        if (x, y) in outline or cell.no_outline or cell.part.startswith("back_"):
            continue
        ramp_len = pal.ramp_len(cell.ramp)
        x0, y0, x1, y1 = boxes[cell.part]
        mid_y = (y0 + y1) // 2
        third = max((x1 - x0 + 1) // 3, 1)
        in_mid = (x0 + third) <= x <= (x1 - third)

        def same_part(px, py):
            c = buf.cells.get((px, py))
            return c is not None and c.part == cell.part

        hi = False
        if outline.get((x, y - 1)) == "lit" and y <= mid_y + 1:
            hi = True
        elif in_mid and outline.get((x, y - 2)) == "lit" and same_part(x, y - 1) and y <= mid_y + 1:
            hi = True
        elif outline.get((x - 1, y)) == "lit" and y <= mid_y:
            hi = True

        lo = False
        if outline.get((x, y + 1)) == "shadow" and y >= mid_y:
            lo = True
        elif in_mid and outline.get((x, y + 2)) == "shadow" and same_part(x, y + 1) and y >= mid_y:
            lo = True
        elif outline.get((x + 1, y)) == "shadow" and x >= (x0 + x1) // 2:
            lo = True
        else:
            above = buf.cells.get((x, y - 1))
            if (above is not None and above.part != cell.part
                    and not above.no_outline and (x, y - 1) not in outline):
                lo = True  # ambient occlusion under an overlapping part

        new_idx = None
        if hi and not lo:
            new_idx = min(cell.idx + 1, ramp_len - 1)
        elif lo and not hi:
            new_idx = max(cell.idx - 1, 0)
        if new_idx is not None and pal.hex_of(cell.ramp, new_idx) not in protected_hexes:
            updates[(x, y)] = new_idx
    for px, idx in updates.items():
        buf.cells[px].idx = idx


def dissolve_small_clusters(buf: PixelBuffer, pal: Palette,
                            protected_hexes: set[str], min_size: int = 3) -> None:
    """Merge accidental sub-``min_size`` color clusters into a neighbor color.

    Enforces art rule 3 (cluster shading) at generation time. Deliberate small
    details (eyes, element accents) are protected by hex. Regions are 8-connected
    by final hex value. Boundary regions only accept outline-eligible colors so
    the sel-out contract survives the merge.
    """
    eligible = pal.darkest_step_hexes()
    for _ in range(3):
        changed = False
        hexes = {px: pal.hex_of(c.ramp, c.idx) for px, c in buf.cells.items()}
        seen: set[tuple[int, int]] = set()
        for start in list(buf.cells):
            if start in seen:
                continue
            region = [start]
            seen.add(start)
            qi = 0
            while qi < len(region):
                cx, cy = region[qi]
                qi += 1
                for nx in (cx - 1, cx, cx + 1):
                    for ny in (cy - 1, cy, cy + 1):
                        p = (nx, ny)
                        if p not in seen and hexes.get(p) == hexes[start]:
                            seen.add(p)
                            region.append(p)
            if len(region) >= min_size or hexes[start] in protected_hexes:
                continue
            # candidate replacement colors from 4-neighbors outside the region
            counts: dict[str, tuple[int, Cell]] = {}
            on_boundary = False
            rset = set(region)
            for (cx, cy) in region:
                for p in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if p in rset:
                        continue
                    c = buf.cells.get(p)
                    if c is None:
                        if not buf.cells[(cx, cy)].no_outline:
                            on_boundary = True
                        continue
                    h = hexes[p]
                    cnt, ref = counts.get(h, (0, c))
                    counts[h] = (cnt + 1, ref)
            if on_boundary and hexes[start] in eligible:
                continue  # legitimate short sel-out segment -- keep it
            # prefer non-ink colors: merging interior noise into ink fakes a
            # thick outline (lint rule d would flag the resulting 2x2 blocks).
            # Never merge INTO a protected hex (whitelist cap: 6 px/frame).
            ink_hex = pal.hex_of(*INK)
            cands = sorted(((h, v) for h, v in counts.items() if h not in protected_hexes),
                           key=lambda kv: (kv[0] == ink_hex, -kv[1][0]))
            if on_boundary:
                cands = [c for c in cands if c[0] in eligible]
            if not cands:
                if on_boundary:
                    for (cx, cy) in region:
                        cell = buf.cells[(cx, cy)]
                        cell.ramp, cell.idx = INK
                else:
                    for p in region:
                        buf.cells.pop(p, None)
                changed = True
                continue
            ref = cands[0][1][1]
            for (cx, cy) in region:
                cell = buf.cells[(cx, cy)]
                cell.ramp, cell.idx = ref.ramp, ref.idx
            changed = True
        if not changed:
            break


VOLUME_MIN_CANVAS = 48      # the flat-slab read starts at the large size tier
VOLUME_PARTS = ("torso", "body")


def apply_internal_volume(buf: PixelBuffer, pal: Palette) -> None:
    """Break the big-tier flat-slab torso into volume: repaint the lower-ventral
    interior of the main body mass one ramp step darker (a core shadow) so a
    >=48px torso reads as a rounded form, not a single flat fill. Runs BEFORE
    finalize (between draw_pose and finalize) -- a SOLID interior region, not a
    1px line, so finalize's outline/shade/dissolve handle it as ordinary
    geometry (the rim lesson: post-passes strand orphans / perturb the outline).
    Cells are flagged ``volume`` so generate_unit harvests their hexes into
    detail_exempt_colors (the ventral band's adjacent-step boundary is then
    banding-exempt). Palette-locked (ramp step). No-op below VOLUME_MIN_CANVAS."""
    if buf.h < VOLUME_MIN_CANVAS:
        return
    boxes = buf.part_bboxes()
    for part in VOLUME_PARTS:
        box = boxes.get(part)
        if box is None:
            continue
        x0, y0, x1, y1 = box
        h = y1 - y0 + 1
        if h < 6:
            continue
        venter = y0 + (h * 3) // 5  # lower ~40% of the mass = ventral core shadow
        for (x, y), cell in buf.cells.items():
            if cell.part != part or cell.no_outline or y < venter:
                continue
            new_idx = max(cell.idx - 1, 0)
            if new_idx == cell.idx:
                continue  # already darkest -> no core shadow available
            cell.idx = new_idx
            cell.volume = True


def apply_secondary_lag(poses, loop, lag: int = 1, keys=("wing",)) -> None:
    """Inject a frame-lagged copy of the secondary-motion drive value(s) into
    each pose so a trailing part (the far wing) reads motion from ``lag`` frames
    earlier while the body reads the current frame -- the follow-through that
    makes flight read alive. WRAPS for looping clips (fly/idle), CLAMPS at frame
    0 for one-shots (attack/death). Mutates the pose dicts in place; a no-op for
    poses that carry none of ``keys`` (e.g. bipeds, which have no 'wing')."""
    n = len(poses)
    for i, pose in enumerate(poses):
        src = poses[(i - lag) % n] if loop else poses[max(0, i - lag)]
        for k in keys:
            if k in src:
                pose[k + "_lag"] = src[k]


def finalize(buf: PixelBuffer, pal: Palette, protected_hexes=()) -> None:
    protected = set(protected_hexes)
    outline = apply_selout_outline(buf, pal)
    apply_directional_shading(buf, pal, outline, protected)
    dissolve_small_clusters(buf, pal, protected)


def render(buf: PixelBuffer, pal: Palette):
    """PixelBuffer -> RGBA PIL Image (binary alpha; no AA against transparency)."""
    from PIL import Image
    img = Image.new("RGBA", (buf.w, buf.h), (0, 0, 0, 0))
    px = img.load()
    for (x, y), c in buf.cells.items():
        r, g, b = pal.get(c.ramp, c.idx)
        px[x, y] = (r, g, b, 255)
    return img


# ===========================================================================
# Animation metadata (first-playable budget, art-direction.md section 5)
# ===========================================================================

@dataclass
class AnimDef:
    name: str
    frames: int
    fps: int
    loop: bool


FP_BIPED_ANIMS = [AnimDef("idle", 2, 7, True), AnimDef("walk", 4, 10, True),
                  AnimDef("attack", 4, 12, False), AnimDef("death", 3, 10, False)]
FP_AERIAL_ANIMS = [AnimDef("idle", 2, 7, True), AnimDef("fly", 6, 10, True),
                   AnimDef("attack", 4, 12, False), AnimDef("death", 3, 10, False)]
# Boss budget (pyraxis): idle 6 / fly 6 / attack 7 / death 8 = 27 (grander-boss
# frame budget, Dragon Differentiation Fix target 26-30).
BOSS_AERIAL_ANIMS = [AnimDef("idle", 6, 7, True), AnimDef("fly", 6, 10, True),
                     AnimDef("attack", 7, 12, False), AnimDef("death", 8, 10, False)]


def attack_pose_keys(contact_idx: int, frames: int = 4) -> list[str]:
    """Anticipation poses before the contact frame, recovery after.

    The sim owns timing (art doc section 10): the generator receives the
    contact index (derived from foreswing ticks) and shapes the wind-up to
    visually align with it.
    """
    contact_idx = max(1, min(contact_idx, frames - 2))
    wind = ["windup", "windup2", "windup3"]
    keys = [wind[min(i, len(wind) - 1)] for i in range(contact_idx)]
    keys.append("contact")
    rec = ["recover", "settle", "settle2"]
    while len(keys) < frames:
        keys.append(rec[min(len(keys) - contact_idx - 1, len(rec) - 1)])
    return keys


# ===========================================================================
# Biped family (melee / ranged / sniper / support share one parametric body)
# ===========================================================================

@dataclass
class BipedConfig:
    typeclass: str
    canvas: tuple[int, int]
    head_w: int
    head_h: int
    torso_w: int
    torso_h: int
    leg_h: int
    head_style: str          # 'snout' | 'round_ears' | 'plain' | 'hood'
    attack_style: str        # 'thrust' | 'bow' | 'crossbow' | 'sling' | 'staff'
    robe: bool = False
    head_fwd: int = 1
    quiver: bool = False     # back-mounted quiver block (ranged silhouette cue)
    eye_px: int = 2          # bright eye pixels (art fix 3: 1-2 px, light step)
    eye_shape: str = "round"  # 'round' (default block) | 'slit' (vertical reptilian)
    # --- per-unit shape-language (Part B). 'neutral' reproduces the pre-Part-B
    # rig BYTE-IDENTICALLY (FP units carry no build); the others reshape the
    # silhouette so different-named units stop being one body recolored.
    build: str = "neutral"   # neutral | agile (circle) | sturdy (square) | dangerous (wedge)
    seed: int = 0            # per-unit asymmetry seed (deterministic int; NEVER hash())
    scale: float = 1.0       # size-tier factor (canvas_h / base_canvas_h); 1.0 == byte-identical
    construct_style: str = "segmented"  # ConstructTemplate only: segmented (block grid) | crystalline (facets)
    # Composition slot: the base/legs are a pluggable LocomotionPart. None ->
    # HUMANOID_LEGS (the infantry default). This is the modular axis that lets any
    # upper body (the rest of BipedConfig) sit on any base -- humanoid, ogre stumps,
    # crystal shards, spider, wraith, flying mount -- without a subclass per combo.
    locomotion: "LocomotionPart | None" = None


# Round-2 calibration bodies: small ground units target a 24-28 px tall x
# 14-18 px wide body (art fix 4) on a 32x32 FP canvas. Round 3: the sniper
# moves to the art doc section 4 sniper row -- 48x64 canvas, 40-56 px body
# (height communicates range threat at 1x).
BIPED_CONFIGS = {
    "melee_biped": BipedConfig("melee_biped", (32, 32), 9, 9, 13, 10, 10,
                               "snout", "thrust"),
    "ranged_biped": BipedConfig("ranged_biped", (32, 32), 9, 9, 12, 10, 9,
                                "round_ears", "bow", quiver=True),
    "sniper_biped": BipedConfig("sniper_biped", (48, 64), 11, 12, 16, 16, 18,
                                "plain", "crossbow"),
    "support_robed": BipedConfig("support_robed", (32, 32), 8, 8, 12, 10, 9,
                                 "hood", "staff", robe=True),
}

# Hand positions per arm pose, relative to (torso_x1, torso_y0).
ARM_POSES = {
    "thrust": {
        "idle": (3, 4), "idle_b": (3, 5), "walk": (2, 4),
        "windup": (-1, 1), "windup2": (-2, 1), "contact": (3, 3),
        "recover": (2, 4), "settle": (2, 5),
        "drop": (0, 7), "drop2": (-1, 8),
    },
    "sling": {
        "idle": (1, 6), "idle_b": (1, 7), "walk": (1, 6),
        "windup": (-2, -7), "windup2": (-3, -8), "contact": (3, -6),
        "recover": (2, 4), "settle": (1, 5),
        "drop": (0, 7), "drop2": (-1, 8),
    },
    "bow": {
        "idle": (2, 4), "idle_b": (2, 5), "walk": (2, 4),
        "windup": (2, 4), "windup2": (2, 4), "contact": (2, 4),
        "recover": (2, 4), "settle": (2, 4),
        "drop": (1, 8), "drop2": (0, 10),
    },
    # braced forward aim (not a chest-centered T): the grip rides forward at
    # chest height so the shouldered stock points downrange, arm angled down-fwd.
    "crossbow": {
        "idle": (2, 4), "idle_b": (2, 5), "walk": (1, 4),
        "windup": (1, 2), "windup2": (0, 2), "contact": (3, 2),
        "recover": (2, 3), "settle": (2, 4),
        "drop": (0, 8), "drop2": (-1, 9),
    },
    "staff": {
        "idle": (1, 4), "idle_b": (1, 5), "walk": (1, 4),
        "windup": (0, 1), "windup2": (-1, 0), "contact": (3, 2),
        "recover": (2, 3), "settle": (1, 4),
        "drop": (0, 7), "drop2": (-1, 8),
    },
    # unarmed brawler: arms hang at the sides at rest/walk, cock back on wind-up,
    # drive forward on contact. OgreTemplate draws the thick arm + big fist; this
    # table only positions the hand (relative to torso x1, y0).
    "brawl": {
        "idle": (1, 13), "idle_b": (1, 14), "walk": (0, 13),
        "windup": (-3, 7), "windup2": (-4, 6), "contact": (9, 5),
        "recover": (3, 9), "settle": (1, 12),
        "drop": (0, 14), "drop2": (-1, 15),
    },
    # magic poses (Attack Archetype System): the body gestures, no weapon. cast =
    # gather-then-thrust release; channel = steady arms-forward; body_strike =
    # arms tucked while the body lunges (the contact-frame lunge stance carries it).
    "cast": {
        "idle": (2, 3), "idle_b": (2, 4), "walk": (1, 3),
        "windup": (-1, -2), "windup2": (-2, -3), "contact": (5, -1),
        "recover": (3, 1), "settle": (2, 3),
        "drop": (0, 7), "drop2": (-1, 8),
    },
    "channel": {
        "idle": (2, 4), "idle_b": (2, 5), "walk": (2, 4),
        "windup": (3, 3), "windup2": (3, 2), "contact": (4, 2),
        "recover": (3, 3), "settle": (2, 4),
        "drop": (0, 7), "drop2": (-1, 8),
    },
    "body_strike": {
        "idle": (1, 5), "idle_b": (1, 6), "walk": (1, 5),
        "windup": (-1, 4), "windup2": (-2, 4), "contact": (2, 4),
        "recover": (1, 5), "settle": (1, 5),
        "drop": (0, 7), "drop2": (-1, 8),
    },
}

# Walk cycle: (back_foot_dx, back_foot_lift, front_foot_dx, front_foot_lift, body_lift)
WALK_LEGS = [(-2, 0, 2, 0, 0), (1, 1, 0, 0, 1), (2, 0, -2, 0, 0), (0, 0, 1, 1, 1)]


def _asym(seed: int) -> dict:
    """Deterministic per-seed asymmetry offsets (art-direction rule 10 'break
    symmetry' -- symmetry + uniform scaling is the default-asset tell). A fixed
    function of an integer seed; NEVER Python hash() (salted per-process -> would
    break the identical-spec-identical-bytes pipeline contract)."""
    s = int(seed)
    if s == 0:
        return {"head_dx": 0, "spike_dx": 0, "shoulder_up": 0}  # default: no asymmetry (byte-stable)
    return {
        "head_dx": (s % 3) - 1,        # -1 | 0 | 1  head shifted off-center
        "spike_dx": (s // 3) % 2,      # which side a horn/spike leans
        "shoulder_up": (s // 2) % 2,   # raise one shoulder 1 px
    }


class BipedTemplate:
    # wood is a MID-TONE leather step (art fix 1: 2 px props must survive 1x;
    # darkest-step shafts vanish into the outline). eye is a bright light-ramp
    # step (art fix 3) -- 1-2 deliberate pixels, whitelist-capped.
    ROLE_DEFAULTS = {
        "skin": ("@secondary", 2),
        "cloth": ("@primary", 1),
        "wood": ("leather", 2),
        "metal": ("mauve_grey", 1),
        "accent": ("@primary", "@accent"),
        "eye": ("mauve_grey", 4),
    }
    # roles whose hexes go on the lint whitelist (small deliberate details,
    # capped at 6 px/frame). Mid-tone prop wood/metal go to ``prop_colors``.
    WHITELIST_ROLES = ("accent", "eye")

    def __init__(self, cfg: BipedConfig):
        self.cfg = cfg

    @property
    def canvas(self):
        return self.cfg.canvas

    def animations(self):
        return FP_BIPED_ANIMS

    # ----------------------------------------------------------- pose sets

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            return [
                {"legs": ("stand", 0), "arm": "idle", "prop": "idle"},
                {"legs": ("stand", 0), "head": (0, 1), "squash": 1,
                 "arm": "idle_b", "prop": "idle_b"},  # breathe: torso compresses, feet anchored
            ]
        if anim == "walk":
            return [{"legs": ("walk", i), "arm": "walk", "prop": "walk"} for i in range(4)]
        if anim == "attack":
            out = []
            for key in attack_pose_keys(contact_idx):
                pose = {"legs": ("stand", 0), "arm": key, "prop": key}
                if key.startswith("windup"):
                    pose["lean"] = -1
                    pose["head"] = (-1, 0)
                if key == "contact":
                    pose["legs"] = ("lunge", 0)
                    pose["lean"] = 2
                    pose["head"] = (1, 0)
                out.append(pose)
            return out
        if anim == "death":
            return [
                {"legs": ("stand", 0), "lean": -1, "head": (-1, 1), "arm": "drop", "prop": "drop"},
                {"legs": ("kneel", 0), "lift": -4, "lean": -2, "head": (-2, 1),
                 "arm": "drop2", "prop": "dropped"},
                {"special": "lying", "prop": "dropped"},
            ]
        raise KeyError(anim)

    # ----------------------------------------------------------- drawing

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        colors = unit["colors"]
        W, H = cfg.canvas
        GY = ground_row(H)
        if pose.get("special") == "lying":
            self._draw_lying(buf, unit, pal, GY)
            self._draw_props(buf, unit, pal, pose, hand=None, ground_y=GY, head_rect=None)
            return

        lift = pose.get("lift", 0)
        lean = pose.get("lean", 0)
        cx = W // 2 - 1
        tx0 = cx - cfg.torso_w // 2 + lean
        tx1 = tx0 + cfg.torso_w - 1
        leg_top = GY - cfg.leg_h + 1
        ty1 = leg_top + 1 - lift
        ty0 = ty1 - cfg.torso_h + 1
        ty0 += pose.get("squash", 0)  # squash-and-stretch: compress the torso TOP;
                                      # feet (ty1 -> legs to GY) stay anchored (lint-safe)
        hdx, hdy = pose.get("head", (0, 0))
        hx0 = tx0 + (cfg.torso_w - cfg.head_w) // 2 + cfg.head_fwd + hdx
        hx1 = hx0 + cfg.head_w - 1
        hy1 = ty0 + hdy
        hy0 = hy1 - cfg.head_h + 1

        # --- shape-language stance: hunch (agile) / sunk head (sturdy) + per-
        # seed asymmetric head shift. 'neutral' leaves everything pre-Part-B.
        bld = cfg.build
        asym = _asym(cfg.seed)
        if bld == "agile":
            hx0 += 2
            hx1 += 2
            hy0 += 2
            hy1 += 2
        elif bld == "sturdy":
            hy0 += 1
            hy1 += 1
        hx0 += asym["head_dx"]
        hx1 += asym["head_dx"]

        skin = colors["skin"]
        cloth = colors["cloth"]

        # --- legs / robe (drawn first; torso overlaps the hip line) -------
        if cfg.robe:
            self._draw_robe(buf, pose, tx0, tx1, ty1, GY, cloth)
        else:
            legs_kind, phase = pose.get("legs", ("stand", 0))
            self._draw_legs(buf, legs_kind, phase, tx0, tx1, leg_top - lift, GY, skin)

        # --- quiver (ranged silhouette cue, behind the torso) ---------------
        if cfg.quiver:
            wood = colors["wood"]
            buf.fill_rect(tx0 - 2, ty0 + 1, tx0 - 1, ty0 + 6, wood[0],
                          max(wood[1] - 1, 0), part="quiver")

        # --- torso (build-shaped: block / hunch / wedge) -------------------
        self._draw_torso(buf, bld, asym, tx0, ty0, tx1, ty1, cloth)

        # --- back arm (skip when an off-hand prop covers that side) --------
        offhand = [p for p in unit["props"] if p in ("shield", "small_shield")]
        if not offhand and not cfg.robe:
            self._draw_back_arm(buf, pose, tx0, tx1, ty0, skin)

        # --- head -----------------------------------------------------------
        self._draw_head(buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal,
                        unit.get("eye_offset", (0, 0)))

        # --- shape-language features: dangerous = head horn + shoulder spikes
        if bld == "dangerous":
            self._draw_danger_features(buf, asym, tx0, tx1, ty0, hx1, hy0, skin)

        # --- off-hand prop (shield carried proud of the torso front-low) ----
        for p in offhand:
            props_mod.draw_offhand(buf, pal, p, (tx1 + 1, ty1 - 2), colors, cfg.scale)

        # --- weapon + front arm (arm last so the hand grips the shaft) ------
        arm_key = pose.get("arm", "idle")
        adx, ady = ARM_POSES[cfg.attack_style][arm_key]
        hand = (tx1 + round(adx * cfg.scale), ty0 + round(ady * cfg.scale))
        self._draw_props(buf, unit, pal, pose, hand, GY, head_rect=(hx0, hy0, hx1, hy1))
        shoulder = (tx1 - 1, ty0 + 2)
        self._draw_arm(buf, shoulder, hand, skin)

    # ----------------------------------------------------------- part helpers

    def _draw_arm(self, buf, shoulder, hand, skin):
        """Default arm: a thin 2px limb to the hand (an armed unit's grip is the
        weapon). Brawler forms override for a thick arm + a big clenched fist."""
        buf.limb(shoulder[0], shoulder[1], hand[0], hand[1], skin[0], skin[1],
                 part="arm", bend="horizontal")

    def _draw_back_arm(self, buf, pose, tx0, tx1, ty0, skin):
        """Default far arm: a thin stub behind the torso. Brawler forms override
        for a thick hanging arm + fist that can throw the second punch."""
        buf.fill_rect(tx0 - 1, ty0 + 2, tx0, ty0 + 5, skin[0],
                      max(skin[1] - 1, 0), part="back_arm")

    def _draw_legs(self, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        # Composition seam (DR4): the base/legs are a pluggable LocomotionPart
        # selected by config; None -> the default humanoid limbs. Subclasses no
        # longer override this -- they pick a part instead (OgreStumps,
        # SegmentedPillars, FloatingShards, ...), so any base composes with any
        # upper body. See LOCOMOTION_PARTS + the *_config_from_spec resolvers.
        (self.cfg.locomotion or HUMANOID_LEGS).draw(
            self, buf, kind, phase, tx0, tx1, hip_y, GY, skin)

    def _draw_robe(self, buf, pose, tx0, tx1, ty1, GY, cloth):
        top_w = tx1 - tx0 - 2
        bottom_w = tx1 - tx0 + 4
        rows = GY - ty1 + 1
        sway = 0
        legs_kind, phase = pose.get("legs", ("stand", 0))
        if legs_kind == "walk":
            sway = (-1, 0, 1, 0)[phase % 4]
        cx = (tx0 + tx1) // 2
        for i in range(rows):
            t = i / max(rows - 1, 1)
            w = round(top_w + (bottom_w - top_w) * t)
            dx = sway if i >= rows - 3 else 0
            y = ty1 + i
            buf.fill_rect(cx - w // 2 + dx, y, cx - w // 2 + w - 1 + dx, y,
                          cloth[0], cloth[1], part="robe")

    def _draw_torso(self, buf, bld, asym, tx0, ty0, tx1, ty1, cloth):
        """Build-shaped torso. 'neutral' == the pre-Part-B round-rect exactly;
        the others reshape the body silhouette (the distinctness lever)."""
        cr, ci = cloth
        if bld == "sturdy":
            # top-heavy block: broad pauldron shoulders proud of the body,
            # one raised per seed (asymmetry). Pauldron mass scales with tier.
            s = self.cfg.scale
            pad = round(2 * s)
            ext = round(1 * s)
            buf.fill_round_rect(tx0, ty0 + pad, tx1, ty1, cr, ci, part="torso")
            up = asym["shoulder_up"]
            buf.fill_rect(tx0 - ext, ty0 + up, tx1 + ext, ty0 + pad, cr, ci, part="torso")
            return
        if bld == "agile":
            # hunched: the torso top leans forward (skew), narrowing to a lithe
            # rounded form.
            rows = ty1 - ty0
            for i in range(rows + 1):
                y = ty0 + i
                skew = round((rows - i) / max(rows, 1) * 2)
                x0, x1 = tx0 + skew, tx1 + skew
                if i == 0:
                    x0, x1 = x0 + 1, x1 - 1
                buf.fill_rect(x0, y, x1, y, cr, ci, part="torso")
            return
        if bld == "dangerous":
            # wedge: wide shoulders tapering to a narrow waist (trapezoid).
            rows = ty1 - ty0
            for i in range(rows + 1):
                y = ty0 + i
                inset = round(i / max(rows, 1) * 2)
                buf.fill_rect(tx0 + inset, y, tx1 - inset, y, cr, ci, part="torso")
            return
        buf.fill_round_rect(tx0, ty0, tx1, ty1, cr, ci, part="torso")

    def _draw_danger_features(self, buf, asym, tx0, tx1, ty0, hx1, hy0, skin):
        """Wedge-build menace cues: a swept head horn + asymmetric shoulder
        spikes (skin-colored body protrusions, >=3 px so they survive cleanup).
        Protrusion reach scales with the size tier so spikes stay legible on a
        large body."""
        d = max(skin[1] - 1, 0)
        sp = asym["spike_dx"]
        s = self.cfg.scale
        buf.fill_triangle((hx1 - 1, hy0 + 1), (hx1 + round(3 * s) + sp, hy0 - round(3 * s)),
                          (hx1 + 1, hy0 + 2), skin[0], d, part="horn")
        buf.fill_triangle((tx1 - 1, ty0), (tx1 + round(3 * s), ty0 - round(4 * s) - asym["shoulder_up"]),
                          (tx1 + 1, ty0 + 1), skin[0], d, part="spike")
        buf.fill_triangle((tx0 + 1, ty0), (tx0 - round(2 * s), ty0 - round(3 * s)),
                          (tx0 - 1, ty0 + 1), skin[0], d, part="spike")

    def _draw_eye(self, buf, eye_x, eye_y, colors, eye_offset):
        """1-2 bright eye pixels (art fix 3), position-adjustable per spec.
        eye_shape='slit' draws a 2px VERTICAL reptilian slit (identity accent);
        'round' (default) keeps the horizontal run -- byte-identical."""
        ex, ey = eye_x + eye_offset[0], eye_y + eye_offset[1]
        if getattr(self.cfg, "eye_shape", "round") == "slit":
            buf.set_px(ex, ey, *colors["eye"], part="eye", no_outline=True)
            buf.set_px(ex, ey + 1, *colors["eye"], part="eye", no_outline=True)
            return
        for i in range(max(1, min(self.cfg.eye_px, 2))):
            buf.set_px(ex - i, ey, *colors["eye"], part="eye", no_outline=True)

    def _draw_head(self, buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal,
                   eye_offset=(0, 0)):
        rx = (hx1 - hx0) / 2
        ry = (hy1 - hy0) / 2
        cx = (hx0 + hx1) / 2
        cy = (hy0 + hy1) / 2
        style = self.cfg.head_style
        if style == "hood":
            buf.fill_ellipse(cx, cy, rx, ry, cloth[0], cloth[1], part="head")
            buf.fill_rect(int(cx), int(cy) - 1, hx1, int(cy) + 2, skin[0], skin[1], part="face")
            self._draw_eye(buf, hx1 - 1, int(cy), colors, eye_offset)
            return
        buf.fill_ellipse(cx, cy, rx, ry, skin[0], skin[1], part="head")
        if style == "snout":
            buf.fill_rect(hx1, hy0 + 4, hx1 + 3, hy0 + 6, skin[0], skin[1], part="head")
        if style == "round_ears":
            buf.fill_triangle((hx0 + 1, hy0 + 1), (hx0 - 2, hy0 - 2), (hx0 + 3, hy0), skin[0], skin[1], part="head")
            buf.fill_triangle((hx1 - 2, hy0 + 1), (hx1 + 1, hy0 - 2), (hx1 - 4, hy0), skin[0],
                              max(skin[1] - 1, 0), part="back_ear")
        eye_x = hx1 - (1 if style == "snout" else 2)
        eye_y = hy0 + (hy1 - hy0) // 3 + 1
        self._draw_eye(buf, eye_x, eye_y, colors, eye_offset)

    def _draw_lying(self, buf, unit, pal, GY):
        colors = unit["colors"]
        skin, cloth = colors["skin"], colors["cloth"]
        W, H = self.cfg.canvas
        cx = W // 2 - 1
        # k scales the heap with the canvas (k=1 keeps the round-2 32x32 bodies
        # byte-identical; the 48x64 sniper collapses at k=2 so the corpse mass
        # matches its standing 40+ px read).
        k = max(1, round(H / 32))
        buf.fill_ellipse(cx + k, GY - 2 * k, 7 * k, 2 * k, cloth[0], cloth[1], part="torso")
        buf.fill_ellipse(cx - 8 * k, GY - 2 * k, 3 * k, 2 * k, skin[0], skin[1], part="head")
        buf.fill_rect(cx + 7 * k, GY - k, cx + 10 * k, GY, skin[0], skin[1], part="front_leg")
        buf.fill_rect(cx - k, GY - 4 * k, cx + 2 * k, GY - 3 * k, skin[0], skin[1], part="arm")
        buf.set_px(cx - 8 * k, GY - 3 * k, *colors["eye"], part="eye", no_outline=True)

    def _draw_props(self, buf, unit, pal, pose, hand, ground_y, head_rect):
        colors = unit["colors"]
        prop_pose = pose.get("prop", "idle")
        for p in unit["props"]:
            if p in ("shield", "small_shield"):
                continue  # drawn at the off-hand layer slot
            if p in ("helmet", "crest"):
                if head_rect is not None:
                    props_mod.draw_headgear(buf, pal, p, head_rect, colors)
                continue
            if hand is not None or prop_pose == "dropped":
                props_mod.draw_weapon(buf, pal, p, prop_pose, hand, colors,
                                      ground_y, self.cfg.canvas, self.cfg.scale)


# ===========================================================================
# Ogre (biped-variant: hulking hunched mass, small sunken tusked head)
# ===========================================================================

# A broad, top-heavy body on the 32px canvas -- wider torso + shorter legs than
# any infantry biped so the silhouette reads "not a person" before color.
OGRE_CONFIG = BipedConfig(
    "ogre", (64, 48), head_w=14, head_h=10, torso_w=30, torso_h=22, leg_h=10,
    head_style="snout", attack_style="brawl", build="sturdy")


class OgreTemplate(BipedTemplate):
    """Hulking biped-variant. Subclasses BipedTemplate and overrides only the
    part helpers (DR3 helper-override seam) -- draw_pose's pose/geometry sequence
    is reused unchanged, so the 17 shipped humanoids are untouched. The mass +
    forward hunch + sunken tusked head drop its cross-form IoU vs the humanoids
    below the gate (it must read as a different creature, not a fat human)."""

    ROLE_DEFAULTS = BipedTemplate.ROLE_DEFAULTS
    WHITELIST_ROLES = BipedTemplate.WHITELIST_ROLES

    def __init__(self, cfg: BipedConfig | None = None):
        cfg = cfg or OGRE_CONFIG
        if cfg.locomotion is None:
            cfg = replace(cfg, locomotion=OGRE_STUMPS)
        super().__init__(cfg)

    def _draw_torso(self, buf, bld, asym, tx0, ty0, tx1, ty1, cloth):
        # broad hunched slab: upper back/shoulders proud (the hunch), tapering to
        # a heavy low gut -- much wider than the biped block.
        cr, ci = cloth
        rows = ty1 - ty0
        for i in range(rows + 1):
            y = ty0 + i
            t = i / max(rows, 1)
            bulge = round((1 - t) * 5)      # shoulders/upper-back proud (hunch)
            gut = round(t * 2)              # belly proud of the front, low
            buf.fill_rect(tx0 - bulge, y, tx1 + gut, y, cr, ci, part="torso")
        # a proud back hump cresting above the shoulder line (the hunch read,
        # broken off the boxy top edge); side per seed.
        up = asym["shoulder_up"]
        buf.fill_ellipse(tx0 - 2, ty0 + up, 4, 4, cr, ci, part="torso")

    def _draw_head(self, buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal,
                   eye_offset=(0, 0)):
        # bestial head sunk LOW between the shoulders (the hunch read) + heavy
        # brow and two up-jutting tusks.
        dy = 4
        hy0 += dy
        hy1 += dy
        rx = (hx1 - hx0) / 2
        ry = (hy1 - hy0) / 2
        cx = (hx0 + hx1) / 2
        cy = (hy0 + hy1) / 2
        buf.fill_ellipse(cx, cy, rx, ry, skin[0], skin[1], part="head")
        # heavy brow ridge (one step down, reads as a scowl shadow). Kept <=6 px
        # so it does not form a banding-length successive-step band.
        buf.fill_rect(hx0 + 4, hy0, hx1 - 4, hy0, skin[0], max(skin[1] - 1, 0), part="brow")
        # two tusks jutting up past the jawline (bigger at the larger scale)
        d = max(skin[1] - 1, 0)
        buf.fill_triangle((hx0 + 1, hy1), (hx0 - 1, hy1 - 4), (hx0 + 3, hy1 - 1),
                          skin[0], d, part="face")
        buf.fill_triangle((hx1 - 1, hy1), (hx1 + 1, hy1 - 4), (hx1 - 3, hy1 - 1),
                          skin[0], d, part="face")
        eye_x = hx1 - 3
        eye_y = hy0 + (hy1 - hy0) // 3 + 1
        self._draw_eye(buf, eye_x, eye_y, colors, eye_offset)

    def _draw_arm(self, buf, shoulder, hand, skin):
        # the brute brawls (no weapon): a thick forearm + a BIG clenched fist.
        # The base 2px limb + 32px-tuned offset read as spindly on a 48px body,
        # so the forearm is a slab and the fist an ~8px knuckle mass. The
        # windup->contact hand delta still drives the punch forward.
        sx, sy = shoulder
        hx, hy = hand
        # thick arm along the shoulder->hand vector: vertical when the fist hangs
        # at rest/walk, horizontal when it drives forward on the punch.
        if abs(hx - sx) >= abs(hy - sy):
            buf.fill_rect(min(sx, hx) - 1, min(sy, hy), max(sx, hx), max(sy, hy) + 3,
                          skin[0], skin[1], part="arm")
        else:
            buf.fill_rect(min(sx, hx) - 1, min(sy, hy), max(sx, hx) + 2, max(sy, hy),
                          skin[0], skin[1], part="arm")
        # BIG clenched fist at the hand
        buf.fill_round_rect(hx - 2, hy - 2, hx + 4, hy + 4, skin[0], skin[1], part="fist")
        # knuckle ridges (darker step): read as fingers AND break the fist's flat
        # shading into <7px runs.
        d = max(skin[1] - 1, 0)
        for ky in (hy - 1, hy + 2):
            buf.fill_rect(hx, ky, hx + 3, ky, skin[0], d, part="fist", no_outline=True)

    def _draw_back_arm(self, buf, pose, tx0, tx1, ty0, skin):
        # the far arm. On the recover frame it throws the SECOND punch (the one-
        # two: near fist lands on contact, far fist the frame after), otherwise it
        # rests behind the body. back_* is pre-darkened + shading-exempt -> reads
        # as depth and is immune to the banding lint.
        d = max(skin[1] - 1, 0)
        if pose.get("arm") == "recover":
            by = ty0 + 1                # higher than the near arm so both fists read
            reach = tx1 + 11
            buf.fill_rect(tx1 - 2, by, reach, by + 3, skin[0], d, part="back_arm")
            buf.fill_round_rect(reach, by - 2, reach + 6, by + 4, skin[0], d, part="back_arm")
            return
        buf.fill_rect(tx0 - 2, ty0 + 4, tx0 + 1, ty0 + 9, skin[0], d, part="back_arm")
        buf.fill_round_rect(tx0 - 4, ty0 + 8, tx0 + 1, ty0 + 13, skin[0], d, part="back_arm")


# ===========================================================================
# Construct (biped-variant: an assembled/material golem)
# ===========================================================================

# A bulky humanoid-skeleton golem. Proportions come from the per-unit spec
# overlay (size tier scales them); the base is a stocky sturdy frame.
CONSTRUCT_CONFIG = BipedConfig(
    "construct", (32, 32), head_w=8, head_h=8, torso_w=16, torso_h=14, leg_h=8,
    head_style="plain", attack_style="thrust", build="sturdy",
    construct_style="segmented")


class ConstructTemplate(BipedTemplate):
    """An assembled/material golem. Reuses the biped pose+geometry sequence (the
    DR3 helper-override seam) but swaps the organic round-rect body for one of two
    material shape-languages, selected by ``cfg.construct_style``:

      * ``segmented`` -- a wall of discrete carved blocks separated by 1px gaps.
        Each block is kept under the canvas-scaled banding floor, so the seams
        read as assembled masonry, not a directional-shading gradient. (Stone.)
      * ``crystalline`` -- a faceted angular core (a cluster of diamonds) with
        triangular shards erupting from the shoulders/back/crown. Every facet
        edge is diagonal, sidestepping the axis-aligned banding lint the way the
        ogre's tapered hunch does. (Ice / gem.)

    Both drop cross-form IoU vs the humanoids well below the gate; the two styles
    also stay distinct from each other (block grid vs shard cluster)."""

    ROLE_DEFAULTS = BipedTemplate.ROLE_DEFAULTS
    WHITELIST_ROLES = BipedTemplate.WHITELIST_ROLES

    def __init__(self, cfg: BipedConfig | None = None):
        cfg = cfg or CONSTRUCT_CONFIG
        if cfg.locomotion is None:
            cfg = replace(cfg, locomotion=_default_construct_base(cfg.construct_style))
        super().__init__(cfg)

    # ----------------------------------------------------------- block grid
    def _chunk(self) -> int:
        """Max block edge that stays under the canvas-scaled banding floor. The
        floor is round(MAX_BAND_RUN * frame_h/32); a block edge two below it
        keeps every flat shaded run short, so masonry seams never read as a
        directional-shading band at any tier."""
        band_limit = round(6 * (self.cfg.canvas[1] / 32))
        return max(3, band_limit - 2)

    def _draw_block_grid(self, buf, x0, y0, x1, y1, cr, ci, part="torso", row_seed=0):
        """Brick-staggered blocks (1px gaps) tiling a rect. Each block edge <
        band_limit, so the auto directional-shading on a block stays a short
        per-block run and the gaps (outlined by sel-out) read as mortar seams."""
        cw = self._chunk()
        step = cw + 1
        row, y = 0, y0
        while y <= y1:
            bh = min(cw, y1 - y + 1)
            off = -(cw // 2) if ((row + row_seed) % 2) else 0   # brick stagger
            bx = x0 + off
            while bx <= x1:
                bx0, bx1 = max(bx, x0), min(bx + cw - 1, x1)
                if bx1 >= bx0:
                    buf.fill_rect(bx0, y, bx1, y + bh - 1, cr, ci, part=part)
                bx += step
            row += 1
            y += bh + 1                                         # 1px gap between rows

    # ----------------------------------------------------------- crystal facets
    def _diamond(self, buf, cx, top, bot, halfw, cr, lit, shadow, part="torso"):
        """A 4-point crystal split into a lit (left) and shadow (right) facet --
        all-diagonal edges, so no axis-aligned shaded band forms."""
        _diamond_facets(buf, cx, top, bot, halfw, cr, lit, shadow, part)

    def _draw_crystal_body(self, buf, asym, tx0, ty0, tx1, ty1, cr, ci):
        d = max(ci - 1, 0)
        cx = (tx0 + tx1) // 2
        halfw = max(2, (tx1 - tx0) // 2)
        sp = asym.get("spike_dx", 0)
        up = asym.get("shoulder_up", 0)
        # central crystal prism + two flanking shards (a cluster, asymmetric)
        self._diamond(buf, cx, ty0, ty1 + 1, halfw, cr, ci, d)
        self._diamond(buf, tx0 + halfw // 2 + sp, ty0 + 2, ty1 - 2, max(2, halfw // 2), cr, ci, d)
        self._diamond(buf, tx1 - halfw // 2, ty0 + 4 - up, ty1, max(2, halfw // 2 - 1), cr, d, d)
        # erupting shards (BOLD, asymmetric) -- the crystalline silhouette tell.
        # A tall crown + long shoulder spikes reaching past the body width + a
        # lower side pair make a spiky STAR, not a humanoid; this is what drops the
        # cross-form IoU vs the bipeds (the chunky legs alone read too person-like).
        sh = max(4, halfw + 1)
        # tall crown shard
        buf.fill_triangle((cx - 2, ty0 + 2), (cx + sp, ty0 - sh - 2), (cx + 3, ty0 + 2),
                          cr, ci, part="shard")
        # long upper shoulder spikes (reach well past the body edges). The right
        # tip is clamped 2px inside the canvas so a lunge-frame spike that would
        # overshoot the edge keeps room for its outline (else a 2x2 ink block).
        rtip_x = min(self.cfg.canvas[0] - 2, tx1 + sh)
        buf.fill_triangle((tx1 - 2, ty0 + 3), (rtip_x, ty0 - sh + 1), (tx1, ty0 + 6),
                          cr, d, part="shard")
        buf.fill_triangle((tx0 + 2, ty0 + 3), (tx0 - sh + 1, ty0 - sh + 2), (tx0, ty0 + 6),
                          cr, ci, part="shard")
        # lower side shards (mid-body) -- spikier outline, asymmetric per seed
        midy = (ty0 + ty1) // 2
        buf.fill_triangle((tx1 - 1, midy), (tx1 + sh - 2, midy + 1 + sp), (tx1 - 1, midy + 4),
                          cr, d, part="shard")
        buf.fill_triangle((tx0 + 1, midy + 1), (tx0 - sh + 3, midy + 2), (tx0 + 1, midy + 5),
                          cr, ci, part="shard")

    # ----------------------------------------------------------- part helpers
    def _draw_torso(self, buf, bld, asym, tx0, ty0, tx1, ty1, cloth):
        cr, ci = cloth
        if self.cfg.construct_style == "crystalline":
            self._draw_crystal_body(buf, asym, tx0, ty0, tx1, ty1, cr, ci)
            return
        self._draw_block_grid(buf, tx0, ty0, tx1, ty1, cr, ci, row_seed=asym.get("spike_dx", 0))

    def _draw_head(self, buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal, eye_offset=(0, 0)):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (hx0 + hx1) // 2
        cy = (hy0 + hy1) // 2
        if self.cfg.construct_style == "crystalline":
            # a faceted gem head (up-pointing shard) with a glowing core glint
            buf.fill_triangle((cx, hy0 - 1), (hx0, hy1), (cx, hy1), sr, si, part="head")
            buf.fill_triangle((cx, hy0 - 1), (hx1, hy1), (cx, hy1), sr, d, part="head")
            buf.set_px(cx, cy, *colors["accent"], part="emblem", no_outline=True)
            buf.set_px(cx, cy + 1, *colors["accent"], part="emblem", no_outline=True)
            return
        # segmented: a cubic carved head + a dark eye-slit
        self._draw_block_grid(buf, hx0, hy0, hx1, hy1, sr, si, part="head")
        self._draw_eye(buf, hx1 - 2, cy, colors, eye_offset)


# ===========================================================================
# Locomotion parts -- the composition base/legs slot (DR4)
# ===========================================================================
# Each part draws ONE creature's base region (the leg band, hip_y..GY) given the
# same geometry the old _draw_legs received. Parts are stateless singletons, so a
# unit = [an upper body (BipedConfig)] x [a LocomotionPart], selectable per spec
# via the ``base`` key. The old subclass leg overrides (ogre stumps, segmented
# pillars, crystalline legs) became parts verbatim -- byte-identical -- and the
# new bases (floating shards, spider, wraith, mount) drop in without a subclass.


def _diamond_facets(buf, cx, top, bot, halfw, cr, lit, shadow, part="torso"):
    """A 4-point crystal split into a lit (left) and shadow (right) facet --
    all-diagonal edges, so no axis-aligned shaded band forms. Shared by the
    crystal body and the floating-shard base."""
    midy = (top + bot) // 2
    apex, base = (cx, top), (cx, bot)
    buf.fill_triangle(apex, (cx - halfw, midy), base, cr, lit, part=part)
    buf.fill_triangle(apex, (cx + halfw, midy), base, cr, shadow, part=part)


class LocomotionPart(Protocol):
    """The base/legs of one creature. ``draw`` paints the leg band (hip_y..GY)
    for one pose frame; the template owns everything above the hip line."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin) -> None: ...


class HumanoidLegs:
    """Two organic limbs (vertical bend + toe pixel). The infantry default."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        back_hip = tx0 + 1
        front_hip = tx1 - 2
        if kind == "stand":
            stances = [(0, 0), (1, 0)]
        elif kind == "lunge":
            stances = [(-3, 0), (3, 0)]
        elif kind == "kneel":
            buf.fill_rect(back_hip - 1, GY - 2, back_hip + 2, GY, skin[0], max(skin[1] - 1, 0), part="back_leg")
            buf.fill_rect(front_hip - 1, GY - 2, front_hip + 3, GY, skin[0], skin[1], part="front_leg")
            return
        else:  # walk
            b = WALK_LEGS[phase % 4]
            stances = [(b[0], b[1]), (b[2], b[3])]
        for (fdx, flift), hip, part, didx in zip(
                stances, (back_hip, front_hip), ("back_leg", "front_leg"),
                (max(skin[1] - 1, 0), skin[1])):
            foot_y = GY - flift
            buf.limb(hip, hip_y, hip + fdx, foot_y, skin[0], didx, part=part, bend="vertical")
            buf.set_px(hip + fdx + 2, foot_y, skin[0], didx, part=part)  # toe


class OgreStumps:
    """Short thick planted stumps -- the ogre brute's base."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        if kind == "kneel":
            HUMANOID_LEGS.draw(tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin)
            return
        back = tx0 + 3
        front = tx1 - 6
        dx = 0
        if kind == "lunge":
            back -= 3
            front += 3
        elif kind == "walk":
            dx = WALK_LEGS[phase % 4][0]
        buf.fill_rect(back - 2 + dx, hip_y, back + 2 + dx, GY, skin[0],
                      max(skin[1] - 1, 0), part="back_leg")
        buf.fill_rect(front - 2, hip_y, front + 3, GY, skin[0], skin[1],
                      part="front_leg")


def _band_limit(tmpl) -> int:
    """Max same-ramp run before the banding lint trips, scaled to the canvas."""
    return round(6 * (tmpl.cfg.canvas[1] / 32))


class SegmentedPillars:
    """Stocky stone pillar legs (< band_limit wide) -- segmented construct base."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        if kind == "kneel":
            HUMANOID_LEGS.draw(tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin)
            return
        sr, si = skin
        lw = max(2, max(3, _band_limit(tmpl) - 2) // 2)
        back = tx0 + 2
        front = tx1 - 2 - lw
        dx = 0
        if kind == "lunge":
            back -= 2
            front += 2
        elif kind == "walk":
            dx = WALK_LEGS[phase % 4][0]
        buf.fill_rect(back - lw + dx, hip_y, back + dx, GY, sr, max(si - 1, 0), part="back_leg")
        buf.fill_rect(front, hip_y, front + lw, GY, sr, si, part="front_leg")


class CrystallineLegs:
    """Thick wide-stance legs with a knee-facet band-breaker. Legacy crystalline
    base, kept as a selectable archetype -- superseded as the crystalline DEFAULT
    by FloatingShards (the legs read too humanoid; see the body-plan rework)."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        if kind == "kneel":
            HUMANOID_LEGS.draw(tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin)
            return
        sr, si = skin
        cx = (tx0 + tx1) // 2
        d = max(si - 1, 0)
        if kind == "lunge":
            stances = [(-2, 0), (3, 0)]
        elif kind == "walk":
            b = WALK_LEGS[phase % 4]
            stances = [(b[0], b[1]), (b[2], b[3])]
        else:
            stances = [(0, 0), (1, 0)]
        for (fdx, flift), hip, part, didx in zip(
                stances, (cx - 5, cx + 2), ("back_leg", "front_leg"), (d, si)):
            foot = hip + fdx
            base = GY - flift
            mid = (hip_y + base) // 2
            knee = max(didx - 1, 0)
            buf.fill_rect(hip, hip_y, hip + 2, mid - 1, sr, didx, part=part)
            buf.fill_rect(min(hip, foot), mid, max(hip, foot) + 2, mid + 1, sr, knee, part=part)
            buf.fill_rect(foot, mid + 2, foot + 2, base, sr, didx, part=part)


class FloatingShards:
    """Detached crystal shards levitating below the body -- a non-humanoid base for
    crystal/gem constructs. N diamonds hover near the lane line with a clear GAP
    above them (so the body reads as unmoored / floating), drifting on the walk
    cycle. All-diagonal facet edges sidestep the axis-aligned banding lint, the
    same trick the crystal body uses. Count auto-scales with body width unless
    pinned. This is the thematic answer to 'crystal constructs read too humanoid':
    the solution is a different base archetype, not repositioned legs."""

    def __init__(self, count=None):
        self.count = count          # None -> auto from body width

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (tx0 + tx1) // 2
        span = max(tx1 - tx0, 6)
        bob = 0
        if kind == "walk":
            bob = (0, 1, 0, -1)[phase % 4]      # drift, as if unmoored
        elif kind == "lunge":
            bob = -1
        region = GY - hip_y
        gap = max(2, region // 4)               # floating gap between body and shards
        top = hip_y + gap
        bot = GY                                 # lowest shard grounded (feet-on-line);
                                                 # the float read comes from the body gap
        n = self.count or max(2, min(3, span // 10))
        mid = n // 2
        for i in range(n):
            sx = (cx if n == 1 else tx0 + round((i + 0.5) * span / n)) + bob
            lit, shadow = (si, d) if (i % 2 == 0) else (d, d)
            hw = max(2, span // (2 * n))
            t = top if i == mid else top + 1     # centre shard tallest (cluster read)
            w = hw if i == mid else max(2, hw - 1)
            _diamond_facets(buf, sx, t, bot, w, sr, lit, shadow, part=f"shard_base_{i}")


class SpiderLegs:
    """N arachnid legs per side, arching up off the body then angling down to splayed
    ground feet -- a wide, low, distinctly non-humanoid base. The upper body (any
    BipedConfig) rides on top, so 'spider base + caster torso' is just this part plus
    a robed/cast config; 'spider + biter' is this plus a body_strike pose."""

    def __init__(self, per_side=4):
        self.per_side = per_side

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (tx0 + tx1) // 2
        span = max(tx1 - tx0, 6)
        reach = span // 2 + 4
        for side in (-1, 1):
            part = "front_leg" if side == 1 else "back_leg"
            didx = si if side == 1 else d
            for i in range(self.per_side):
                t = (i + 1) / (self.per_side + 1)        # fan position 0..1
                gait = 0
                if kind == "walk":
                    gait = (1, -1)[(phase + i) % 2]      # alternating shuffle
                elif kind == "lunge" and side == 1:
                    gait = 2
                hipx = cx + side
                knee_x = cx + side * (1 + round(reach * t * 0.55))
                knee_y = hip_y - 4 - i                    # outer legs arch higher (knee above body base)
                foot_x = cx + side * (2 + round(reach * t)) + side * gait
                foot_y = GY - (i % 2)                     # stagger feet 1px for an organic line
                buf.line(hipx, hip_y, knee_x, knee_y, sr, didx, part=part)      # body -> knee (up/out)
                buf.line(knee_x, knee_y, foot_x, foot_y, sr, didx, part=part)   # knee -> foot (down/out)


class WraithTail:
    """No legs -- a tattered shroud tapering from the hip into frayed wisp tongues
    that hover just off the ground (a floating gap). The undead/wraith base; the
    upper body rides on top as usual. Drawn in the unit's skin/cloth color."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        cr, ci = skin
        d = max(ci - 1, 0)
        cx = (tx0 + tx1) // 2
        sway = 0
        if kind == "walk":
            sway = (-1, 0, 1, 0)[phase % 4]
        elif kind == "lunge":
            sway = 2
        region = max(GY - hip_y, 4)
        bottom = GY - 2                                   # shroud hovers 2px off the ground
        half = max(2, (tx1 - tx0) // 2)
        # tapering shroud: wide at the hip, narrowing toward the hover line
        for i in range(bottom - hip_y + 1):
            t = i / max(bottom - hip_y, 1)
            w = max(1, round(half * (1 - 0.6 * t)))
            dx = round(sway * t)
            y = hip_y + i
            buf.fill_rect(cx - w + dx, y, cx + w + dx, y, cr, ci, part="robe")
        # frayed wisp tongues trailing below the shroud (darker; the dissolving edge)
        for k, off in enumerate((-half + 1, 0, half - 1)):
            fx = cx + off + round(sway * 0.5)
            buf.fill_triangle((fx - 1, hip_y + region // 2), (fx, GY - (k % 2)),
                              (fx + 1, hip_y + region // 2), cr, d, part="wisp")


class FlyingMount:
    """A hovering platform/disc the unit RIDES (flying machinery / a magic item):
    no legs of its own -- short feet plant on a disc that floats with a gap below.
    Pure composition: a humanoid upper body + this base = a sky-rider, with NO flyer
    template needed (the aerial-creature flyers stay their own family)."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (tx0 + tx1) // 2
        span = max(tx1 - tx0, 6)
        halfw = span // 2 + 1
        # short planted feet from the body down onto the deck
        for fx, didx in ((cx - 2, d), (cx + 1, si)):
            buf.fill_rect(fx, hip_y, fx + 1, GY - 1, sr, didx, part="legs")
        # the platform deck: an ellipse with its bottom on the lane line + a darker
        # rim row. (A real airborne rider sits in Stratum.Air with the aerial lint
        # profile, which lets the whole deck hover off the line -- see methodology doc.)
        buf.fill_ellipse(cx, GY - 1, halfw, 1.6, sr, si, part="mount")
        buf.fill_rect(cx - halfw + 1, GY, cx + halfw - 1, GY, sr, d, part="mount")


class CavalrySteed:
    """A quadruped war-beast the rider sits astride: a horizontal barrel on four thin legs,
    a forward neck+head and a trailing tail. Band-safe — the barrel is overlapping ellipses
    (cluster fill, never one wide gradient band) and the legs are 2px lines."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (tx0 + tx1) // 2
        span = max(tx1 - tx0, 8)
        half = span // 2 + 2
        # barrel sits just under the rider's seat, but never below the ground line (a
        # death-frame body-drop must not push the steed past GY).
        by = min(hip_y + 2, GY - 3)
        # barrel body: two overlapping ellipses (lit body + shaded underline)
        buf.fill_ellipse(cx - 1, by, half, 3, sr, si, part="barrel")
        buf.fill_ellipse(cx - 1, by + 1, half - 1, 2, sr, d, part="barrelshade")
        # forward neck + blunt head (the steed faces right, with the rider)
        nx = cx + half - 1
        buf.fill_rect(nx, by - 3, nx + 1, by, sr, si, part="neck")
        buf.fill_ellipse(nx + 2, by - 3, 2, 2, sr, si, part="steedhead")
        # trailing tail
        buf.line(cx - half, by, cx - half - 3, by + 3, sr, d, part="tail")
        # four thin legs (band-safe lines); gait shuffles on walk, plants on lunge
        if kind == "walk":
            gait = WALK_LEGS[phase % 4]
            offs = (gait[0], gait[2], gait[0], gait[2])
        elif kind == "lunge":
            offs = (-2, -2, 3, 3)
        else:
            offs = (0, 0, 1, 1)
        legxs = (cx - half + 2, cx - 1, cx + 1, cx + half - 3)
        for i, lx in enumerate(legxs):
            front = i >= 2
            didx = si if front else d
            buf.line(lx, by + 2, lx + offs[i], GY, sr, didx,
                     part="front_leg" if front else "back_leg")


class NagaCoil:
    """A coiled serpent base: the upright torso rises on a thick trunk out of a piled coil.
    Built from STACKED overlapping ellipse loops (cluster fill) — the earlier wide single-fill
    coil banded; segmented piling is the fix (same trick as the leviathan hull)."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (tx0 + tx1) // 2
        span = max(tx1 - tx0, 8)
        half = span // 2 + 1
        sway = 0
        if kind == "walk":
            sway = (-1, 0, 1, 0)[phase % 4]
        elif kind == "lunge":
            sway = 2
        base_y = GY - 2  # the lowest loop's radius reaches exactly to the ground line
        # piled coil: overlapping ellipse loops, widest at the base, narrowing upward
        loops = [
            (cx + sway, base_y, half, 2, si),
            (cx - 1 + sway, base_y - 3, half - 1, 2, d),
            (cx + 1 + sway, base_y - 5, max(half - 3, 2), 2, si),
        ]
        for i, (lx, ly, rw, rh, idx) in enumerate(loops):
            buf.fill_ellipse(lx, ly, rw, rh, sr, idx, part=f"coil{i}")
        # thick trunk rising from the top loop to the rider's seat
        top = base_y - 6
        buf.fill_rect(cx - 1 + sway, hip_y, cx + 1 + sway, top, sr, si, part="trunk")
        # tail tip curling out of the base coil
        buf.line(cx + half + sway, base_y, cx + half + 3 + sway, base_y - 2, sr, d, part="tail")


class RootLegs:
    """A rooted base: a central trunk splaying into several tapering roots that grip the lane
    line. Near-static (the monolith barely crawls, anchors when rooted). Band-safe — a thin
    trunk + tapering root lines, never a wide fill."""

    def draw(self, tmpl, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
        sr, si = skin
        d = max(si - 1, 0)
        cx = (tx0 + tx1) // 2
        half = max(3, (tx1 - tx0) // 2)
        creep = 0
        if kind == "walk":
            creep = (-1, 0, 1, 0)[phase % 4]  # barely crawls
        elif kind == "lunge":
            creep = 1
        # central trunk column down to the ground line
        buf.fill_rect(cx - 1 + creep, hip_y, cx + 1 + creep, GY, sr, si, part="trunk")
        # splayed roots gripping the ground (tapering lines from a mid fork)
        fork_y = GY - max(3, (GY - hip_y) // 2)
        for off, idx in ((-half, d), (-half // 2, si), (half // 2, d), (half, si)):
            buf.line(cx + creep, fork_y, cx + off + creep, GY, sr, idx, part="root")


HUMANOID_LEGS = HumanoidLegs()
OGRE_STUMPS = OgreStumps()
SEGMENTED_PILLARS = SegmentedPillars()
CRYSTALLINE_LEGS = CrystallineLegs()
FLOATING_SHARDS = FloatingShards()
SPIDER_LEGS = SpiderLegs()
WRAITH_TAIL = WraithTail()
FLYING_MOUNT = FlyingMount()
CAVALRY_STEED = CavalrySteed()
NAGA_COIL = NagaCoil()
ROOT_LEGS = RootLegs()

# Spec ``base`` key -> part. Adding a base archetype = one entry here + the class.
LOCOMOTION_PARTS = {
    "humanoid": HUMANOID_LEGS,
    "ogre_stumps": OGRE_STUMPS,
    "segmented": SEGMENTED_PILLARS,
    "crystalline_legs": CRYSTALLINE_LEGS,
    "shards": FLOATING_SHARDS,
    "spider": SPIDER_LEGS,
    "wraith": WRAITH_TAIL,
    "mount": FLYING_MOUNT,
    "cavalry": CAVALRY_STEED,
    "naga": NAGA_COIL,
    "root": ROOT_LEGS,
}


def _default_construct_base(style: str) -> LocomotionPart:
    """The base a construct gets when its spec names no explicit ``base``.
    Crystalline constructs float on detached shards (the legged version read too
    humanoid -- the thematic fix is a different base archetype, not repositioned
    legs); segmented stone constructs stand on stocky pillars. Either is still
    overridable per-unit via the spec ``base`` key (e.g. ``crystalline_legs``)."""
    return FLOATING_SHARDS if style == "crystalline" else SEGMENTED_PILLARS


# ===========================================================================
# Aerial flyer
# ===========================================================================

# Fly loop: near-wing tip dy (relative to wing root) and body bob, 6 frames.
FLY_WING_DY = [-8, -4, 1, 5, 2, -4]
FLY_BODY_DY = [1, 0, 0, -1, -1, 0]


@dataclass
class FlyerConfig:
    """Parametric flyer sizing/features. Pose tables stay in base-32 units;
    the scaled path multiplies every coordinate by ``s`` (proportion scaling,
    NOT pixel-doubling -- FP batch rule for the 48/64/96 canvases)."""

    canvas: tuple[int, int] = (32, 32)
    s: float = 1.0            # body-part proportion scale
    wing_mult: float = 1.0    # wing length multiplier on top of s
    tail_mult: float = 1.0    # whip-tail length multiplier
    head_style: str = "wyvern"  # wyvern | beaked | horned | crowned
    crest: str = "none"         # none | head (swept crest) | ridge (back spikes)
    fire_tail: bool = False     # ember flame cluster at the tail tip
    eye_px: int = 1             # 1 | 2 | 4 (4 = 2x2 block)
    eye_shape: str = "round"    # 'round' (block) | 'slit' (vertical reptilian)
    boss: bool = False          # boss budget: idle 6 / fly 6 / attack 7 / death 8
    seed: int = 0               # per-unit asymmetry seed (0 = byte-stable default)
    body_dx: int = 0            # absolute canvas shift (right-edge headroom)
    body_dy: int = 0
    dragon: bool = False        # dedicated dragon anatomy (S-neck, fan wings)
    feather_wing: bool = False  # bird/griffin: a feathered vane (skin-colored + feather
                                # strokes) instead of a translucent dragon membrane
    insect_wing: bool = False   # faerie/sprite: two thin gossamer wing pairs (a long
                                # forewing + short hindwing), no membrane / no feathers
    body_plan: str = "drake"    # drake (default winged biped, byte-stable) | wyrm
                                # (serpentine eastern dragon) | seraph (multi-wing) |
                                # wisp (wingless mote-ring) | manta (sky-ray glider)


def _flyer_eye(buf, ex, ey, colors, eye_px, shape="round"):
    """Flyer eye accent: 'slit' = a 2px VERTICAL reptilian slit (identity accent);
    'round' (default) = the eye_px block (1 | 2 | 4=2x2) -- byte-identical.
    Whitelisted detail, no_outline (kept off the sel-out pass)."""
    if shape == "slit":
        buf.set_px(ex, ey, *colors["eye"], part="eye", no_outline=True)
        buf.set_px(ex, ey + 1, *colors["eye"], part="eye", no_outline=True)
        return
    for dx_, dy_ in [(0, 0), (-1, 0), (0, 1), (-1, 1)][: max(1, min(eye_px, 4))]:
        buf.set_px(ex + dx_, ey + dy_, *colors["eye"], part="eye", no_outline=True)


def _dragon_horns(hx, hy, R):
    """Crowned dragon-horn geometry as (p0, apex, p2, part) triangles. Each apex
    must RAKE BACK over the neck (apex.dx < 0, |dx| >= |dy|) so the pair reads as
    heavy dragon horns, not upward-splaying deer antlers. Pure -> unit-tested
    (test_templates.test_dragon_horns_backswept). R is the _draw_dragon scale fn."""
    return [
        # near horn -- chunky wedge raking back from the skull crown (wide base
        # p0->p2 = thickness; apex p1 sweeps back so it reads horn, not antler)
        ((hx + R(1.2), hy - R(0.6)), (hx - R(4.6), hy - R(2.8)), (hx - R(0.4), hy - R(3.0)), "horn"),
        # far horn -- the depth pair, a touch higher + shorter behind the near horn
        ((hx + R(0.6), hy - R(1.6)), (hx - R(3.4), hy - R(3.6)), (hx - R(0.2), hy - R(3.6)), "horn_far"),
    ]


def _insect_wing_pair(buf, root, wm, fill, part):
    """Two thin gossamer wings from a shoulder root -- a long forewing angled up-back
    + a shorter hindwing down-back. The faerie/insect silhouette (small path only)."""
    fr, fi = fill
    rx, ry = root
    buf.fill_triangle((rx, ry), (rx - round(11 * wm), ry - 5),
                      (rx - round(6 * wm), ry + 1), fr, fi, part=part)
    buf.fill_triangle((rx, ry + 1), (rx - round(8 * wm), ry + 5),
                      (rx - round(5 * wm), ry + 2), fr, fi, part=part)


def _feather_wing_fill(buf, root, tip, trail, fill, sep_idx, part, fingers=3):
    """A feathered wing vane: a skin-colored fill (NOT a translucent membrane) with
    dark feather-separation strokes fanning from the root across the outer edge --
    reads as layered flight feathers (bird / griffin), the differentiator from the
    dragon membrane wing. Same triangle footprint, so it still clears the wingspan lint."""
    fr, fi = fill
    buf.fill_triangle(root, tip, trail, fr, fi, part=part)
    for i in range(1, fingers + 1):
        t = i / (fingers + 1)
        ex = round(tip[0] + (trail[0] - tip[0]) * t)
        ey = round(tip[1] + (trail[1] - tip[1]) * t)
        buf.line(root[0], root[1], ex, ey, fr, sep_idx, part=part, no_outline=True)


class AerialFlyerTemplate:
    """Wyvern/whelp-shaped flyer family. Hovers above the ground line (engine
    adds the drop-shadow ellipse, art-direction.md section 6). Larger canvases
    scale body-part proportions through FlyerConfig; the default config keeps
    the accepted 32x32 whelp byte-identical via the verbatim small path."""

    # hot = crest spikes / maw glow / tail flame: bright primary step drawn as
    # OUTLINED >=3 px geometry (not whitelisted -- the 6 px cap stays for
    # accent+eye). horn = bone-grey head spikes on horned/crowned styles.
    ROLE_DEFAULTS = {
        "skin": ("@primary", 2),
        "belly": ("@secondary", 3),
        "membrane": ("@secondary", 1),
        "accent": ("@primary", "@accent"),
        "hot": ("@primary", 4),
        "horn": ("mauve_grey", 2),
        "eye": ("mauve_grey", 4),
    }
    WHITELIST_ROLES = ("accent", "eye")

    def __init__(self, cfg: FlyerConfig | None = None):
        self.cfg = cfg or FlyerConfig()

    @property
    def canvas(self):
        return self.cfg.canvas

    def animations(self):
        return BOSS_AERIAL_ANIMS if self.cfg.boss else FP_AERIAL_ANIMS

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            if self.cfg.boss:
                return [{"wing": -5, "body": (0, 0)}, {"wing": -3, "body": (0, 0)},
                        {"wing": -1, "body": (0, 1)}, {"wing": -2, "body": (0, 1)},
                        {"wing": -4, "body": (0, 1)}, {"wing": -5, "body": (0, 0)}]
            return [{"wing": -5, "body": (0, 0)}, {"wing": -2, "body": (0, 1)}]
        if anim == "fly":
            return [{"wing": FLY_WING_DY[i], "body": (0, FLY_BODY_DY[i])} for i in range(6)]
        if anim == "attack":
            table = {
                "windup": {"wing": -6, "body": (-1, -1), "head": (-1, -1)},
                "windup2": {"wing": -8, "body": (-2, -1), "head": (-1, -2)},
                "windup3": {"wing": -9, "body": (-2, -2), "head": (-2, -3)},
                "contact": {"wing": 2, "body": (3, 1), "head": (3, 2), "mouth_open": True},
                "recover": {"wing": -2, "body": (1, 0), "head": (1, 1)},
                "settle": {"wing": -4, "body": (0, 0), "head": (0, 0)},
                "settle2": {"wing": -5, "body": (0, 1), "head": (0, 1)},
            }
            frames = 7 if self.cfg.boss else 4
            return [dict(table[k]) for k in attack_pose_keys(contact_idx, frames)]
        if anim == "death":
            if self.cfg.boss:
                return [
                    {"wing": 3, "body": (0, 1), "head": (0, 1)},
                    {"wing": 4, "body": (0, 1), "head": (0, 2)},
                    {"wing": 5, "body": (0, 2), "head": (0, 3)},
                    {"wing": 6, "body": (0, 3), "head": (1, 4), "fold": True},
                    {"wing": 7, "body": (0, 4), "head": (1, 5), "fold": True},
                    {"wing": 8, "body": (0, 5), "head": (0, 6), "fold": True, "crumple": True},
                    {"wing": 8, "body": (0, 5), "head": (0, 7), "fold": True, "crumple": True},
                    {"wing": 8, "body": (0, 5), "head": (0, 7), "fold": True, "crumple": True},
                ]
            return [
                {"wing": 4, "body": (0, 1), "head": (0, 2)},
                {"wing": 7, "body": (0, 3), "head": (1, 5), "fold": True},
                {"wing": 8, "body": (0, 5), "head": (0, 7), "fold": True, "crumple": True},
            ]
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        bp = self.cfg.body_plan
        if bp == "wyrm":
            self._draw_wyrm(buf, pose, unit, pal)
        elif bp == "seraph":
            self._draw_seraph(buf, pose, unit, pal)
        elif bp == "wisp":
            self._draw_wisp(buf, pose, unit, pal)
        elif bp == "manta":
            self._draw_manta(buf, pose, unit, pal)
        elif bp == "ordnance":
            self._draw_ordnance(buf, pose, unit, pal)
        elif bp == "leviathan":
            self._draw_leviathan(buf, pose, unit, pal)
        elif self.cfg.dragon:
            self._draw_dragon(buf, pose, unit, pal)
        elif self.cfg.s < 1.5:
            self._draw_small(buf, pose, unit, pal)
        else:
            self._draw_scaled(buf, pose, unit, pal)

    # -------------------------------------------------- 32px path (whelp)
    # Verbatim round-2 calibration drawing: the accepted frost_whelp must
    # stay byte-identical, so this path takes no scaling parameters.

    def _draw_small(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        colors = unit["colors"]
        skin, belly = colors["skin"], colors["belly"]
        membrane = colors["membrane"]
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        W, H = self.cfg.canvas
        bx = W // 2 - 1 + self.cfg.body_dx + bdx
        by = H // 2 + 1 + self.cfg.body_dy + bdy
        wing_dy = pose.get("wing", -4)
        # wing_mult + head_style differentiate same-canvas flyers (Part B); for
        # the byte-stable frost_whelp these are 1.0 / 'wyvern' -> the verbatim path.
        wm = self.cfg.wing_mult
        hs = self.cfg.head_style
        fold = pose.get("fold", False)

        # far wing (behind body): pre-darkened, flat. Tips reach far enough
        # that tip-to-snout wingspan clears the 24 px aerial floor (lint c).
        root_far = (bx - 2, by - 4)
        far_dark = max(membrane[1] - 1, 0)
        if fold:
            buf.fill_triangle(root_far, (root_far[0] - 4, root_far[1] + 2),
                              (root_far[0] - 1, root_far[1] + 3),
                              membrane[0], far_dark, part="back_wing")
        else:
            ftip = (root_far[0] - round(12 * wm), root_far[1] + wing_dy + 1)
            ftrail = (root_far[0] - 2, root_far[1] + 3)
            if self.cfg.insect_wing:
                _insect_wing_pair(buf, root_far, wm, (membrane[0], far_dark), "back_wing")
            elif self.cfg.feather_wing:
                _feather_wing_fill(buf, root_far, ftip, ftrail,
                                   (skin[0], max(skin[1] - 1, 0)), max(skin[1] - 2, 0), "back_wing")
            else:
                buf.fill_triangle(root_far, ftip, ftrail, membrane[0], far_dark, part="back_wing")

        # tail: draconic whip (default) or a short raptor fan (beaked)
        td = max(skin[1] - 1, 0)
        if hs == "beaked":
            buf.fill_triangle((bx - 3, by), (bx - 8, by - 2), (bx - 8, by + 4),
                              skin[0], td, part="tail")
        else:
            buf.line(bx - 4, by + 1, bx - 9, by + 5, skin[0], td, part="tail", width=2)
            buf.set_px(bx - 10, by + 6, skin[0], td, part="tail")

        # body + belly
        ry = 2 if pose.get("crumple") else 3
        buf.fill_ellipse(bx, by, 4, ry, skin[0], skin[1], part="body")
        buf.fill_rect(bx - 2, by + ry - 2, bx + 3, by + ry - 1, belly[0], belly[1], part="belly")

        # tucked haunch under the belly (one mass; 2x2 stubs read as specks)
        if not pose.get("crumple"):
            buf.fill_rect(bx, by + ry, bx + 3, by + ry + 1, skin[0],
                          max(skin[1] - 1, 0), part="legs")

        # neck connects body mass to head (no floating-head silhouette)
        hx, hy = bx + 6 + hdx, by - 5 + hdy
        buf.fill_rect(bx + 3, hy + 1, hx - 1, by - 1, skin[0], skin[1], part="neck")
        buf.fill_ellipse(hx, hy, 2.6, 2.4, skin[0], skin[1], part="head")
        acc = colors["accent"]
        if hs == "beaked":
            # raptor: forward beak wedge + a single swept head crest, NO horns
            buf.fill_triangle((hx + 2, hy - 1), (hx + 6, hy + 1), (hx + 2, hy + 2),
                              skin[0], skin[1], part="head")
            buf.fill_triangle((hx - 1, hy - 2), (hx - 4, hy - 5), (hx, hy - 1),
                              skin[0], td, part="horn")
        else:
            if pose.get("mouth_open"):
                buf.fill_rect(hx + 2, hy - 1, hx + 4, hy - 1, skin[0], skin[1], part="head")
                buf.fill_rect(hx + 2, hy + 1, hx + 4, hy + 1, skin[0], skin[1], part="head")
            else:
                buf.fill_rect(hx + 2, hy, hx + 4, hy + 1, skin[0], skin[1], part="head")
            if hs == "horned":
                # draconic swept horns (a bigger >=3 px cluster than the whelp)
                buf.fill_triangle((hx - 1, hy - 1), (hx - 5, hy - 5), (hx + 1, hy - 2),
                                  skin[0], td, part="horn")
            else:
                buf.set_px(hx - 1, hy - 3, acc[0], acc[1], part="horn", no_outline=True)
                buf.set_px(hx, hy - 3, acc[0], acc[1], part="horn", no_outline=True)
        eo = unit.get("eye_offset", (0, 0))
        buf.set_px(hx + 1 + eo[0], hy - 1 + eo[1], *colors["eye"], part="eye",
                   no_outline=True)

        # near wing (over body), wing-finger ridge along the top edge
        root = (bx + 1, by - 3)
        if fold:
            buf.fill_triangle(root, (root[0] - 5, root[1] + 1), (root[0] - 1, root[1] + 3),
                              membrane[0], membrane[1], part="wing")
        else:
            tip = (root[0] - round(9 * wm), root[1] + wing_dy)
            trail = (root[0] + 2, root[1] + 2)
            if self.cfg.insect_wing:
                _insect_wing_pair(buf, root, wm, belly, "wing")
            elif self.cfg.feather_wing:
                _feather_wing_fill(buf, root, tip, trail, skin, max(skin[1] - 1, 0), "wing")
            else:
                buf.fill_triangle(root, tip, trail, membrane[0], membrane[1], part="wing")
                # wing-finger ridge: darkest step so the boundary stays sel-out dark
                buf.line(root[0], root[1], tip[0], tip[1], skin[0], 0,
                         part="wing_finger", no_outline=True)

    # ------------------------------------------- scaled path (48/64/96 px)
    # Dragon/gryphon detailing: broad scalloped wing membranes with finger
    # ridges, thick tapered whip tail, wedge muzzle with a dark jaw line and
    # brow ridge, chunky outlined horn/crest geometry, glowing maw at the
    # breath-lunge contact. Proportions scale with cfg.s -- no pixel-doubling.

    def _draw_scaled(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin, belly = colors["skin"], colors["belly"]
        membrane = colors["membrane"]
        dark_skin = max(skin[1] - 1, 0)
        W, H = cfg.canvas
        wm = cfg.wing_mult
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        bx = W // 2 - 1 + cfg.body_dx + R(bdx)
        by = H // 2 + R(1) + cfg.body_dy + R(bdy)
        wing_dy = R(pose.get("wing", -4))
        far_wing_dy = R(pose.get("wing_lag", pose.get("wing", -4)))  # far wing trails 1 frame
        fold = pose.get("fold", False)
        mouth_open = pose.get("mouth_open")

        # --- far wing: broad scalloped membrane, pre-darkened -------------
        rf = (bx - R(1.5), by - R(3.6))
        far_dark = max(membrane[1] - 1, 0)
        if fold:
            buf.fill_triangle(rf, (rf[0] - R(5), rf[1] + R(2)),
                              (rf[0] - R(1), rf[1] + R(3.5)),
                              membrane[0], far_dark, part="back_wing")
        else:
            tip_f = (rf[0] - R(12 * wm), rf[1] + far_wing_dy + R(1))
            sc1_f = (rf[0] - R(7.5 * wm), rf[1] + far_wing_dy + R(4.6))
            sc2_f = (rf[0] - R(2.5), rf[1] + R(3.2))
            if cfg.feather_wing:
                _feather_wing_fill(buf, rf, tip_f, sc2_f,
                                   (skin[0], max(skin[1] - 1, 0)), max(skin[1] - 2, 0),
                                   "back_wing", fingers=4)
            else:
                buf.fill_triangle(rf, tip_f, sc1_f, membrane[0], far_dark, part="back_wing")
                buf.fill_triangle(rf, sc1_f, sc2_f, membrane[0], far_dark, part="back_wing")

        # --- tail: tapered triangle wedge (buf.line width caps at 2 px and
        # reads as wire at scale), tip segment whips back up ----------------
        tm = cfg.tail_mult
        t1 = (bx - R(8.5 * tm), by + R(4.6 * tm))
        buf.fill_triangle((bx - R(2.5), by - R(0.8)), (bx - R(2.5), by + R(2.2)),
                          t1, skin[0], dark_skin, part="tail")
        t2 = (t1[0] + R(1.2), t1[1] - R(3))
        buf.fill_triangle((t1[0] - R(0.8), t1[1] + R(0.6)),
                          (t1[0] + R(0.9), t1[1] - R(0.6)), t2,
                          skin[0], dark_skin, part="tail2")
        if cfg.fire_tail:
            hot = colors["hot"]
            buf.fill_triangle((t2[0] - R(1.6), t2[1] + R(0.8)),
                              (t2[0] - R(0.4), t2[1] - R(2.6)),
                              (t2[0] + R(1.6), t2[1] + R(0.4)),
                              hot[0], hot[1], part="tail_flame")

        # --- body + belly + tucked haunch ----------------------------------
        ry = R(2) if pose.get("crumple") else R(3)
        buf.fill_ellipse(bx, by, R(4.6), ry, skin[0], skin[1], part="body")
        buf.fill_rect(bx - R(2.4), by + ry - R(1.8), bx + R(3), by + ry - 1,
                      belly[0], belly[1], part="belly")
        if not pose.get("crumple"):
            buf.fill_ellipse(bx + R(1.2), by + ry - R(0.8), R(1.8), R(1.2),
                             skin[0], dark_skin, part="legs")

        # --- dorsal ember crest --------------------------------------------
        if cfg.crest == "ridge":
            hot = colors["hot"]
            top = by - ry
            for i, ox in enumerate((2.6, 0.2, -2.4)):
                cx0 = bx + R(ox)
                hgt = R(3.0 - 0.5 * i)
                buf.fill_triangle((cx0 - R(1.3), top + 2), (cx0 + R(0.2), top - hgt),
                                  (cx0 + R(1.3), top + 2), hot[0], hot[1],
                                  part=f"crest{i}")

        # --- neck + head (brow ridge for the angular dragon read) ----------
        hx = bx + R(6) + R(hdx)
        hy = by - R(5) + R(hdy)
        buf.fill_rect(bx + R(2.5), hy + R(1.2), hx, by - 1, skin[0], skin[1], part="neck")
        buf.fill_ellipse(hx, hy, 2.8 * s, 2.3 * s, skin[0], skin[1], part="head")
        buf.fill_rect(hx - R(0.6), hy - R(2.6), hx + R(1.8), hy - R(1.8),
                      skin[0], skin[1], part="head")

        # --- muzzle: wedge snout + jaw (or beak) ----------------------------
        snout = R(5.2) if cfg.head_style in ("horned", "crowned") else R(4.4)
        if cfg.head_style == "beaked":
            bk = belly
            if mouth_open:
                buf.fill_triangle((hx + R(1.6), hy - R(2)), (hx + R(5.5), hy - R(1.5)),
                                  (hx + R(1.6), hy - R(0.4)), bk[0], bk[1], part="beak")
                buf.fill_triangle((hx + R(1.6), hy + R(0.4)), (hx + R(4.5), hy + R(1.9)),
                                  (hx + R(1.6), hy + R(2)), bk[0], bk[1], part="beak")
            else:
                buf.fill_triangle((hx + R(1.6), hy - R(1.6)), (hx + R(5.5), hy + R(0.2)),
                                  (hx + R(1.6), hy + R(1.6)), bk[0], bk[1], part="beak")
        elif mouth_open:
            # breath-lunge read: upper wedge tilts up, lower jaw drops, the
            # maw glows hot (projectiles stay sim-spawned; this is held)
            buf.fill_triangle((hx + R(1), hy - R(2.2)), (hx + snout, hy - R(1.2)),
                              (hx + R(1), hy + R(0.2)), skin[0], skin[1], part="snout")
            buf.fill_triangle((hx + R(0.8), hy + R(0.8)), (hx + R(3.8), hy + R(2.8)),
                              (hx + R(0.8), hy + R(2)), skin[0], dark_skin, part="jaw")
            hot = colors["hot"]
            buf.fill_rect(hx + R(1.4), hy - R(0.8), hx + R(3.6), hy + R(0.4),
                          hot[0], hot[1], part="maw")
        else:
            buf.fill_triangle((hx + R(1), hy - R(1.8)), (hx + snout, hy + R(0.4)),
                              (hx + R(1), hy + R(1.4)), skin[0], skin[1], part="snout")
            buf.fill_rect(hx + R(1), hy + R(1), hx + R(3.2), hy + R(1.5),
                          skin[0], dark_skin, part="jaw")

        # --- horn / crest geometry (outlined >=3 px clusters, never accent px)
        if cfg.head_style == "horned":
            horn = colors["horn"]
            buf.fill_triangle((hx - R(0.4), hy - R(1.8)), (hx - R(3.8), hy - R(4.6)),
                              (hx + R(1.2), hy - R(2)), horn[0], horn[1], part="horn")
            buf.fill_triangle((hx + R(1.2), hy - R(2)), (hx - R(1), hy - R(5.2)),
                              (hx + R(2.4), hy - R(1.8)), horn[0], horn[1], part="horn2")
        elif cfg.head_style == "crowned":
            horn = colors["horn"]
            buf.fill_triangle((hx - R(1.2), hy - R(1.6)), (hx - R(4.8), hy - R(4.4)),
                              (hx + R(0.2), hy - R(2)), horn[0], horn[1], part="horn")
            buf.fill_triangle((hx + R(0.2), hy - R(2)), (hx - R(1.4), hy - R(6)),
                              (hx + R(1.6), hy - R(2.2)), horn[0], horn[1], part="horn2")
            buf.fill_triangle((hx + R(1.6), hy - R(2.2)), (hx + R(1.4), hy - R(5.4)),
                              (hx + R(2.8), hy - R(1.8)), horn[0], horn[1], part="horn3")
        if cfg.crest == "head":
            hot = colors["hot"]
            buf.fill_triangle((hx - R(0.4), hy - R(2)), (hx - R(3.6), hy - R(4.2)),
                              (hx + R(1.6), hy - R(2.2)), hot[0], hot[1], part="crest")

        eo = unit.get("eye_offset", (0, 0))
        ex, ey = hx + R(0.6) + eo[0], hy - R(0.9) + eo[1]
        _flyer_eye(buf, ex, ey, colors, cfg.eye_px, getattr(cfg, "eye_shape", "round"))

        # --- near wing: scalloped membrane + finger ridges ------------------
        rn = (bx + R(1.5), by - R(3))
        if fold:
            buf.fill_triangle(rn, (rn[0] - R(6), rn[1] + R(1.5)),
                              (rn[0] - R(1), rn[1] + R(3.5)),
                              membrane[0], membrane[1], part="wing")
        else:
            tip = (rn[0] - R(11 * wm), rn[1] + wing_dy)
            sc1 = (rn[0] - R(6.5 * wm), rn[1] + wing_dy + R(3.8))
            sc2 = (rn[0] - R(2), rn[1] + R(3))
            if cfg.feather_wing:
                _feather_wing_fill(buf, rn, tip, sc2, skin, max(skin[1] - 1, 0),
                                   "wing", fingers=4)
            else:
                buf.fill_triangle(rn, tip, sc1, membrane[0], membrane[1], part="wing")
                buf.fill_triangle(rn, sc1, sc2, membrane[0], membrane[1], part="wing")
                buf.line(rn[0], rn[1], tip[0], tip[1], skin[0], 0,
                         part="wing_finger", no_outline=True)
                buf.line(rn[0], rn[1], sc1[0], sc1[1], skin[0], 0,
                         part="wing_finger2", no_outline=True)

    # ------------------------------------------------- dragon path (64/96 px)
    # Showpiece anatomy the generic scaled path can't reach: two-mass tapered
    # torso, 3-segment S-curve neck carrying a SMALL head high, fan wings with
    # wrist-radiating finger spars on BOTH wings, haunch + tucked foreleg,
    # long whip tail ending in a membrane spade (or ember flame).

    def _draw_dragon(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin, belly = colors["skin"], colors["belly"]
        membrane = colors["membrane"]
        dark_skin = max(skin[1] - 1, 0)
        W, H = cfg.canvas
        wm = cfg.wing_mult
        tm = cfg.tail_mult
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        bx = W // 2 - 1 + cfg.body_dx + R(bdx)
        by = H // 2 + R(1) + cfg.body_dy + R(bdy)
        wing_dy = R(pose.get("wing", -4))
        far_wing_dy = R(pose.get("wing_lag", pose.get("wing", -4)))  # far wing beats behind
        fold = pose.get("fold", False)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")

        # --- far wing: fan panels, pre-darkened, beats slightly behind ------
        rf = (bx - R(0.5), by - R(2.6))
        far_dark = max(membrane[1] - 1, 0)
        if fold:
            buf.fill_triangle(rf, (rf[0] - R(5.5), rf[1] + R(1.5)),
                              (rf[0] - R(1), rf[1] + R(3.2)),
                              membrane[0], far_dark, part="back_wing")
        else:
            fw = int(round(far_wing_dy * 0.9)) - R(1)
            tip_f = (rf[0] - R(12.0 * wm), rf[1] + fw)
            f1_f = (rf[0] - R(9.5 * wm), rf[1] + fw + R(3.4))
            f2_f = (rf[0] - R(3.4 * wm), rf[1] + R(2.0))
            buf.fill_triangle(rf, tip_f, f1_f, membrane[0], far_dark, part="back_wing")
            buf.fill_triangle(rf, f1_f, f2_f, membrane[0], far_dark, part="back_wing")

        # --- whip tail: wedge, curl-up tip, spade or ember flame ------------
        t1 = (bx - R(9.0 * tm), by + R(3.6 * tm))
        buf.fill_triangle((bx - R(2.0), by - R(1.0)), (bx - R(2.0), by + R(1.8)),
                          t1, skin[0], dark_skin, part="tail")
        t2 = (t1[0] - R(2.0 * tm), t1[1] - R(2.2 * tm))
        buf.fill_triangle((t1[0] - R(0.9), t1[1] + R(0.6)),
                          (t1[0] + R(0.9), t1[1] - R(0.6)), t2,
                          skin[0], dark_skin, part="tail2")
        if cfg.fire_tail:
            hot = colors["hot"]
            buf.fill_triangle((t2[0] - R(1.6), t2[1] + R(0.8)),
                              (t2[0] - R(0.2), t2[1] - R(2.6)),
                              (t2[0] + R(1.6), t2[1] + R(0.4)),
                              hot[0], hot[1], part="tail_flame")
        else:
            buf.fill_triangle((t2[0] - R(1.5), t2[1] + R(0.7)),
                              (t2[0] + R(0.1), t2[1] - R(1.9)),
                              (t2[0] + R(1.3), t2[1] + R(0.7)),
                              membrane[0], membrane[1], part="tail_spade")

        # --- torso: hind mass + deeper chest, bridged (no egg) --------------
        if crumple:
            buf.fill_ellipse(bx, by + R(1.0), R(5.0), R(1.6), skin[0], skin[1],
                             part="body")
        else:
            buf.fill_ellipse(bx - R(2.2), by + R(0.5), R(2.9), R(2.2),
                             skin[0], skin[1], part="body")
            buf.fill_ellipse(bx + R(1.8), by - R(0.7), R(3.4), R(2.5),
                             skin[0], skin[1], part="body")
            buf.fill_rect(bx - R(2.0), by - R(1.4), bx + R(2.5), by + R(1.6),
                          skin[0], skin[1], part="body")
            buf.fill_rect(bx - R(1.6), by + R(0.8), bx + R(3.8), by + R(1.8),
                          belly[0], belly[1], part="belly")

        # --- haunch + trailing hind leg, tucked foreleg ----------------------
        if not crumple:
            buf.fill_ellipse(bx - R(2.6), by + R(1.6), R(1.9), R(1.5),
                             skin[0], dark_skin, part="haunch")
            buf.line(bx - R(2.4), by + R(2.6), bx - R(2.9), by + R(4.4),
                     skin[0], dark_skin, part="hind_leg", width=2)
            buf.fill_rect(bx - R(3.3), by + R(4.4), bx - R(2.4), by + R(4.7),
                          skin[0], 0, part="hind_foot")
            buf.line(bx + R(2.4), by + R(1.6), bx + R(3.0), by + R(3.0),
                     skin[0], dark_skin, part="foreleg", width=2)

        # --- dorsal ridge spikes along the spine -----------------------------
        if cfg.crest == "ridge" and not crumple:
            hot = colors["hot"]
            for ox, base_dy, hgt in ((3.2, -2.9, 2.3), (1.2, -3.1, 2.8),
                                     (-0.9, -2.6, 2.3), (-3.0, -2.0, 1.7)):
                cx0 = bx + R(ox)
                top = by + R(base_dy)
                buf.fill_triangle((cx0 - R(1.0), top + R(0.8)),
                                  (cx0 + R(0.1), top - R(hgt)),
                                  (cx0 + R(1.0), top + R(0.8)),
                                  hot[0], hot[1], part=f"crest{ox}")

        # --- S-curve neck: three tapering segments climbing forward-up ------
        # per-seed asymmetry (reuses the shared _asym helper): a 1px head lean
        # breaks the mechanical-symmetry "default-asset tell". seed 0 -> no shift.
        asym = _asym(getattr(cfg, "seed", 0))
        hx = bx + R(6.4) + R(hdx) + asym["head_dx"]
        hy = by - R(8.6) + R(hdy) - asym["shoulder_up"]
        if crumple:
            # damped head drop: the pose-table dy is tuned for the 32px whelp;
            # at dragon scale the full offset sinks the head through the
            # ground-line lint floor
            hx = bx + R(6.0) + R(hdx)
            hy = by + R(0.4) + R(hdy * 0.35)
            buf.fill_ellipse(bx + R(3.6), by + R(0.8), R(1.7), R(1.2),
                             skin[0], skin[1], part="neck")
        else:
            neck_pts = (((hx - bx) * 0.36, (hy - by) * 0.30, 1.9, 1.7),
                        ((hx - bx) * 0.62, (hy - by) * 0.60, 1.6, 1.5),
                        ((hx - bx) * 0.84, (hy - by) * 0.85, 1.4, 1.3))
            for ndx, ndy, rx_, ry_ in neck_pts:
                buf.fill_ellipse(bx + int(round(ndx)) , by + int(round(ndy)),
                                 R(rx_), R(ry_), skin[0], skin[1], part="neck")

        # --- head: small, high, browed ---------------------------------------
        buf.fill_ellipse(hx, hy, R(2.0), R(1.6), skin[0], skin[1], part="head")
        buf.fill_rect(hx - R(0.6), hy - R(2.0), hx + R(1.6), hy - R(1.4),
                      skin[0], skin[1], part="brow")

        # --- muzzle: wedge snout + jaw; glowing maw on the breath contact ----
        snout = R(5.0) if cfg.head_style in ("horned", "crowned") else R(4.2)
        if mouth_open:
            buf.fill_triangle((hx + R(1), hy - R(2.0)), (hx + snout, hy - R(1.2)),
                              (hx + R(1), hy + R(0.2)), skin[0], skin[1], part="snout")
            buf.fill_triangle((hx + R(0.8), hy + R(0.8)), (hx + R(3.6), hy + R(2.6)),
                              (hx + R(0.8), hy + R(1.9)), skin[0], dark_skin, part="jaw")
            hot = colors["hot"]
            buf.fill_rect(hx + R(1.3), hy - R(0.8), hx + R(3.8), hy + R(0.5),
                          hot[0], hot[1], part="maw")
        else:
            buf.fill_triangle((hx + R(1), hy - R(1.7)), (hx + snout, hy + R(0.3)),
                              (hx + R(1), hy + R(1.3)), skin[0], skin[1], part="snout")
            buf.fill_rect(hx + R(1), hy + R(0.9), hx + R(3.0), hy + R(1.4),
                          skin[0], dark_skin, part="jaw")

        # --- swept-back horns / crown ----------------------------------------
        if cfg.head_style == "horned":
            horn = colors["horn"]
            buf.fill_triangle((hx - R(0.4), hy - R(1.6)), (hx - R(3.6), hy - R(4.2)),
                              (hx + R(1.0), hy - R(1.8)), horn[0], horn[1], part="horn")
            buf.fill_triangle((hx + R(1.0), hy - R(1.8)), (hx - R(0.8), hy - R(4.8)),
                              (hx + R(2.2), hy - R(1.6)), horn[0], horn[1], part="horn2")
        elif cfg.head_style == "crowned":
            h = colors["horn"]
            for p0, p1, p2, hpart in _dragon_horns(hx, hy, R):
                buf.fill_triangle(p0, p1, p2, h[0], h[1], part=hpart)
        if cfg.crest == "head":
            hot = colors["hot"]
            buf.fill_triangle((hx - R(0.4), hy - R(1.8)), (hx - R(3.4), hy - R(3.8)),
                              (hx + R(1.4), hy - R(2.0)), hot[0], hot[1], part="crest")

        eo = unit.get("eye_offset", (0, 0))
        ex, ey = hx + R(0.6) + eo[0], hy - R(0.7) + eo[1]
        _flyer_eye(buf, ex, ey, colors, cfg.eye_px, getattr(cfg, "eye_shape", "round"))
        # fire-ember identity accent (crowned hero only): a 2px brow ember in the
        # whitelisted no_outline accent role -> blooms under signature_fire and
        # stays within the 6px eye+accent cap (slit eye 2 + accent 2 = 4).
        if cfg.head_style == "crowned":
            acc = colors["accent"]
            buf.set_px(ex - R(0.5), ey - R(0.7), acc[0], acc[1], part="accent", no_outline=True)
            buf.set_px(ex + R(0.1), ey - R(0.7), acc[0], acc[1], part="accent", no_outline=True)

        # --- near wing: 3-panel fan + wrist-radiating finger spars -----------
        rn = (bx + R(1.2), by - R(2.4))
        if fold:
            buf.fill_triangle(rn, (rn[0] - R(6), rn[1] + R(1.2)),
                              (rn[0] - R(1), rn[1] + R(3.2)),
                              membrane[0], membrane[1], part="wing")
        else:
            tip = (rn[0] - R(13.0 * wm), rn[1] + wing_dy)
            f1 = (rn[0] - R(9.8 * wm), rn[1] + wing_dy + R(4.2))
            f2 = (rn[0] - R(4.0 * wm), rn[1] + int(round(wing_dy * 0.35)) + R(4.6))
            trail = (rn[0] - R(0.8), rn[1] + R(2.8))
            buf.fill_triangle(rn, tip, f1, membrane[0], membrane[1], part="wing")
            buf.fill_triangle(rn, f1, f2, membrane[0], membrane[1], part="wing")
            buf.fill_triangle(rn, f2, trail, membrane[0], membrane[1], part="wing")
            wrist = (rn[0] - R(3.8 * wm), rn[1] + int(round(wing_dy * 0.7)))
            buf.line(rn[0], rn[1], wrist[0], wrist[1], skin[0], skin[1],
                     part="wing_arm", width=2)
            buf.line(wrist[0], wrist[1], tip[0], tip[1], skin[0], 0,
                     part="wing_finger", no_outline=True)
            buf.line(wrist[0], wrist[1], f1[0], f1[1], skin[0], 0,
                     part="wing_finger2", no_outline=True)
            buf.line(wrist[0], wrist[1], f2[0], f2[1], skin[0], 0,
                     part="wing_finger3", no_outline=True)

    # ------------------------------------------- wyrm (serpentine eastern dragon)
    # A long undulating ribbon body, no large wings: dorsal fins, a swept mane,
    # trailing whiskers, a high antlered head. Reads as a wholly different
    # CREATURE from the winged drake (body-plan axis, not wing trim).

    def _draw_wyrm(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin, belly = colors["skin"], colors["belly"]
        membrane = colors["membrane"]
        dark_skin = max(skin[1] - 1, 0)
        hot, horn = colors["hot"], colors["horn"]
        W, H = cfg.canvas
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        phase = pose.get("wing", -4)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")
        bx = W // 2 - 1 + cfg.body_dx + R(bdx)
        by = H // 2 + R(1) + cfg.body_dy + R(bdy)

        # --- serpentine spine: tail (left) -> neck (right) on a sine wave; the
        # pose phase slides the wave so the body slithers, flattens on death ----
        nseg = 10
        amp = R(0.6) if crumple else R(2.8)
        head_x = bx + R(7)
        tail_x = bx - R(13)
        span = head_x - tail_x
        ph = phase * 0.5
        lift = R(0.8) if crumple else R(3.0)
        seg = []
        for i in range(nseg):
            t = i / (nseg - 1)
            sx = round(tail_x + span * t)
            sy = by + round(amp * math.sin(ph + t * 6.28)) - round(lift * t)
            # thin tail swelling to a thick fore-body, easing toward the neck
            rad = R(1.0 + 2.3 * (t ** 0.7) * (1.0 - 0.25 * t))
            seg.append((sx, sy, rad))

        # smooth ribbon: dense FLAT ellipses (ry < rx) overlap into one body,
        # not a chain of round segments (which read as a grub)
        for sx, sy, rad in seg:
            buf.fill_ellipse(sx, sy, rad, max(R(0.9), int(round(rad * 0.7))),
                             skin[0], skin[1], part="body")
        for sx, sy, rad in seg[nseg // 2:]:
            ry = max(R(0.9), int(round(rad * 0.7)))
            buf.fill_rect(sx - rad + R(0.5), sy + ry - R(0.8), sx + rad - R(0.5),
                          sy + ry - 1, belly[0], belly[1], part="belly")
        # dorsal spine: a few deliberate fins along the back, not one per segment
        if not crumple:
            for idx in (2, 4, 6):
                sx, sy, rad = seg[idx]
                ry = max(R(0.9), int(round(rad * 0.7)))
                buf.fill_triangle((sx - R(1.1), sy - ry + R(0.2)),
                                  (sx, sy - ry - R(2.0)), (sx + R(1.1), sy - ry + R(0.2)),
                                  membrane[0], membrane[1], part=f"fin{idx}")
        tsx, tsy, _ = seg[0]
        buf.fill_triangle((tsx + R(0.6), tsy), (tsx - R(3.4), tsy - R(2.2)),
                          (tsx - R(0.8), tsy + R(1.2)), skin[0], dark_skin, part="tail")

        # --- head: high, antlered, maned, whiskered --------------------------
        nx, ny, _ = seg[-1]
        hx = head_x + R(2) + R(hdx)
        hy = (ny - R(3) + R(hdy)) if not crumple else (ny + int(round(R(hdy) * 0.3)))
        if not crumple:
            for j, (mx, my, mh) in enumerate(((-1.8, -1.0, 2.6), (-3.0, -0.2, 2.2),
                                              (-4.2, 0.8, 1.8))):
                cx0, cy0 = hx + R(mx), hy + R(my)
                buf.fill_triangle((cx0 - R(0.9), cy0 + R(0.6)),
                                  (cx0 - R(0.3), cy0 - R(mh)),
                                  (cx0 + R(0.9), cy0 + R(0.6)),
                                  skin[0], dark_skin, part=f"mane{j}")
        buf.fill_ellipse(hx, hy, R(2.4), R(2.0), skin[0], skin[1], part="head")
        if mouth_open:
            buf.fill_triangle((hx + R(1.4), hy - R(1.6)), (hx + R(4.8), hy - R(1.2)),
                              (hx + R(1.4), hy + R(0.2)), skin[0], skin[1], part="snout")
            buf.fill_triangle((hx + R(1.2), hy + R(0.8)), (hx + R(4.0), hy + R(2.4)),
                              (hx + R(1.2), hy + R(1.8)), skin[0], dark_skin, part="jaw")
            buf.fill_rect(hx + R(1.6), hy - R(0.6), hx + R(3.4), hy + R(0.6),
                          hot[0], hot[1], part="maw")
        else:
            buf.fill_triangle((hx + R(1.2), hy - R(1.4)), (hx + R(4.8), hy + R(0.2)),
                              (hx + R(1.2), hy + R(1.6)), skin[0], skin[1], part="snout")
            buf.fill_rect(hx + R(1.2), hy + R(1.0), hx + R(3.2), hy + R(1.5),
                          skin[0], dark_skin, part="jaw")
        buf.fill_triangle((hx - R(0.4), hy - R(1.6)), (hx - R(3.4), hy - R(5.0)),
                          (hx + R(0.6), hy - R(1.9)), horn[0], horn[1], part="antler")
        buf.fill_triangle((hx + R(1.0), hy - R(1.9)), (hx + R(1.2), hy - R(5.2)),
                          (hx + R(2.4), hy - R(2.1)), horn[0], horn[1], part="antler2")
        if not crumple:
            buf.fill_triangle((hx + R(2.2), hy + R(0.6)), (hx + R(5.4), hy + R(1.8)),
                              (hx + R(2.2), hy + R(1.4)), skin[0], dark_skin, part="barbel")
        eo = unit.get("eye_offset", (0, 0))
        ex, ey = hx + R(0.2) + eo[0], hy - R(0.6) + eo[1]
        _flyer_eye(buf, ex, ey, colors, cfg.eye_px, getattr(cfg, "eye_shape", "round"))

    # ------------------------------------------ seraph (multi-winged celestial)
    # A frontal, hovering luminous being: a slim glowing core, a halo, and THREE
    # symmetric wing-pairs fanning out (6 wings). Radial awe, not a side bird.

    def _draw_seraph(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin = colors["skin"]
        membrane = colors["membrane"]
        dark_skin = max(skin[1] - 1, 0)
        hot = colors["hot"]
        W, H = cfg.canvas
        wm = cfg.wing_mult
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        wing = pose.get("wing", -4)
        fold = pose.get("fold", False)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")
        cx = W // 2 - 1 + cfg.body_dx + R(bdx)
        cy = H // 2 + R(1) + cfg.body_dy + R(bdy)
        beat = 0 if fold else int(round(R(wing) * 0.35))

        # three symmetric wing tiers (root_dy, length, tip_dy, fill); broad solid
        # vanes outline cleanly. The mid pair is brightest (skin) + widest -> sets
        # the wingspan; upper/lower are darker membrane, layered behind the body.
        tiers = ((-5.0, 12.5, -4.5, "mem"), (0.5, 15.5, 0.0, "skin"),
                 (5.5, 11.5, 4.5, "mem"))
        if crumple:
            tiers = ((-1.5, 6.0, 4.0, "mem"), (1.0, 7.5, 5.0, "skin"),
                     (3.5, 5.0, 6.0, "mem"))
        for k, (rdy, ln, tdy, col) in enumerate(tiers):
            fill = (skin[0], skin[1]) if col == "skin" else (membrane[0], membrane[1])
            for sgn in (-1, 1):
                rt = (cx + sgn * R(1.6), cy + R(rdy) - R(1.8))
                rb = (cx + sgn * R(1.6), cy + R(rdy) + R(1.8))
                tip = (cx + sgn * R(ln * wm), cy + R(tdy) + (beat if k != 1 else 0))
                buf.fill_triangle(rt, rb, tip, fill[0], fill[1], part=f"wing{k}_{sgn}")
                buf.line(rt[0], rt[1], tip[0], tip[1], skin[0], 0,
                         part=f"wf{k}_{sgn}", no_outline=True)

        # glowing core body + trailing robe wisp
        buf.fill_ellipse(cx, cy, R(2.0), R(3.2), skin[0], skin[1], part="body")
        if not crumple:
            buf.fill_triangle((cx - R(1.6), cy + R(2.6)), (cx, cy + R(6.5)),
                              (cx + R(1.6), cy + R(2.6)), skin[0], dark_skin, part="robe")
        buf.fill_rect(cx - R(0.8), cy - R(1.8), cx + R(0.8), cy + R(2.2),
                      hot[0], hot[1], part="core")

        # head + halo
        hx = cx + R(hdx)
        hy = cy - R(4.6) + R(hdy)
        buf.fill_ellipse(hx, hy, R(1.7), R(1.7), skin[0], skin[1], part="head")
        if not crumple:
            buf.fill_ellipse(hx, hy - R(2.8), R(2.4), R(0.9), hot[0], hot[1], part="halo")
        eo = unit.get("eye_offset", (0, 0))
        for sgn in (-1, 1):
            buf.set_px(hx + sgn * R(0.8) + eo[0], hy - R(0.2) + eo[1],
                       *colors["eye"], part="eye", no_outline=True)

        if mouth_open:
            buf.fill_ellipse(cx, cy, R(1.4), R(1.4), hot[0], hot[1], part="maw")

    # ------------------------------------------ wisp (wingless floating elemental)
    # No wings, no head, no limbs: a glowing core ringed by orbiting motes. The
    # pose phase spins the motes; the orbit width carries the aerial wingspan.

    def _draw_wisp(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin = colors["skin"]
        membrane = colors["membrane"]
        belly = colors["belly"]
        dark_skin = max(skin[1] - 1, 0)
        W, H = cfg.canvas
        bdx, bdy = pose.get("body", (0, 0))
        wing = pose.get("wing", -4)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")
        cx = W // 2 - 1 + cfg.body_dx + R(bdx)
        cy = H // 2 + R(1) + cfg.body_dy + R(bdy)

        # orbiting motes (diamonds) spun by the pose phase; death scatters them
        spin = wing * 0.5
        nmote = 6
        # death implodes the ring inward (motes fall into the dimming core),
        # which also keeps the death frames inside the airborne band
        dying = crumple or pose.get("fold", False)
        ring = R(5) if dying else R(12)
        sz = R(1.1) if dying else R(1.6)
        for i in range(nmote):
            ang = spin + i * (6.28 / nmote)
            mx = cx + int(round(ring * math.cos(ang)))
            my = cy + int(round(ring * 0.62 * math.sin(ang)))
            buf.fill_triangle((mx - sz, my), (mx, my - sz), (mx + sz, my),
                              skin[0], skin[1], part=f"mote{i}")
            buf.fill_triangle((mx - sz, my), (mx, my + sz), (mx + sz, my),
                              skin[0], dark_skin, part=f"mote{i}b")

        # core: membrane aura ring + skin orb + a bright energy heart (BELLY, a
        # bright non-whitelisted glow -- avoids the hot==accent collision)
        if not crumple:
            buf.fill_ellipse(cx, cy, R(3.2), R(3.0), membrane[0], membrane[1], part="aura")
        buf.fill_ellipse(cx, cy, R(2.2), R(2.1), skin[0], skin[1], part="body")
        cr = R(1.3) if not crumple else R(0.8)
        buf.fill_ellipse(cx, cy, cr, cr, belly[0], belly[1], part="core")
        eo = unit.get("eye_offset", (0, 0))
        buf.set_px(cx + eo[0], cy + eo[1], *colors["eye"], part="eye", no_outline=True)
        if mouth_open:
            buf.fill_triangle((cx + R(2), cy - R(1.3)), (cx + R(8), cy),
                              (cx + R(2), cy + R(1.3)), belly[0], belly[1], part="lance")

    # ------------------------------------------------- manta (sky-ray glider)
    # A wide flat delta body (pectoral wings continuous with the body, not
    # attached limbs), twin cephalic horns at the nose, a long trailing
    # whip-tail. The pose 'wing' value undulates the wing tips.

    def _draw_manta(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin, belly = colors["skin"], colors["belly"]
        dark_skin = max(skin[1] - 1, 0)
        hot = colors["hot"]
        W, H = cfg.canvas
        wm = cfg.wing_mult
        tm = cfg.tail_mult
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        wing = pose.get("wing", -4)
        fold = pose.get("fold", False)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")
        cx = W // 2 - 1 + cfg.body_dx + R(bdx)
        cy = H // 2 - R(1) + cfg.body_dy + R(bdy)
        dying = fold or crumple
        beat = 0 if dying else int(round(R(wing) * 0.4))

        nose = (cx + R(hdx), cy - R(6) + R(hdy))
        if dying:
            lt = (cx - R(6), cy - R(2))
            rt = (cx + R(6), cy - R(2))
            tail_base = (cx, cy + R(3))
        else:
            wtip = cy + R(1) + beat
            lt = (cx - R(13 * wm), wtip)
            rt = (cx + R(13 * wm), wtip)
            tail_base = (cx, cy + R(5))

        # flat delta body: two halves of a swept kite, pectoral tips = wings
        buf.fill_triangle(nose, lt, tail_base, skin[0], skin[1], part="body")
        buf.fill_triangle(nose, rt, tail_base, skin[0], skin[1], part="body")
        # shaded trailing edge for a banked-glide read
        buf.fill_triangle(lt, tail_base, (cx - R(5), cy + R(2)), skin[0], dark_skin,
                          part="wingshadeL")
        buf.fill_triangle(rt, tail_base, (cx + R(5), cy + R(2)), skin[0], dark_skin,
                          part="wingshadeR")
        # pale underside chevron near the nose
        buf.fill_triangle(nose, (cx - R(3.6), cy), (cx + R(3.6), cy),
                          belly[0], belly[1], part="belly")
        # twin cephalic horns (the manta's forward lobes)
        if not dying:
            buf.fill_triangle((nose[0] - R(0.8), nose[1] + R(0.4)),
                              (nose[0] - R(3.0), nose[1] - R(2.2)),
                              (nose[0] - R(1.8), nose[1] + R(0.8)),
                              skin[0], dark_skin, part="cephL")
            buf.fill_triangle((nose[0] + R(0.8), nose[1] + R(0.4)),
                              (nose[0] + R(3.0), nose[1] - R(2.2)),
                              (nose[0] + R(1.8), nose[1] + R(0.8)),
                              skin[0], dark_skin, part="cephR")
        if mouth_open:
            buf.fill_rect(nose[0] - R(1.4), nose[1] + R(0.6), nose[0] + R(1.4),
                          nose[1] + R(1.8), hot[0], hot[1], part="maw")
        eo = unit.get("eye_offset", (0, 0))
        for sgn in (-1, 1):
            buf.set_px(nose[0] + sgn * R(1.9) + eo[0], nose[1] + R(1.2) + eo[1],
                       *colors["eye"], part="eye", no_outline=True)
        # long trailing whip-tail streaming back
        if not crumple:
            tx, ty = cx - R(7 * tm), tail_base[1] + R(5 * tm)
            buf.line(tail_base[0], tail_base[1], tx, ty, skin[0], dark_skin,
                     part="tail", width=2)
            buf.set_px(tx - 1, ty + 1, skin[0], dark_skin, part="tail")

    # ------------------------------------------- ordnance-flyer (heavy bomber)
    # A bulky winged bomber: fat fuselage body, blunt head, short swept wings
    # (wide enough to clear the aerial wingspan floor), and a slung ordnance pod
    # beneath. The attack pose lights a bombsight glow under the payload.

    def _draw_ordnance(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin, belly, membrane = colors["skin"], colors["belly"], colors["membrane"]
        dark_skin = max(skin[1] - 1, 0)
        hot = colors["hot"]
        W, H = cfg.canvas
        wm = cfg.wing_mult
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        wing = pose.get("wing", -4)
        fold = pose.get("fold", False)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")
        cx = W // 2 - 1 + cfg.body_dx + R(bdx)
        cy = H // 2 - R(1) + cfg.body_dy + R(bdy)
        dying = fold or crumple
        beat = 0 if dying else int(round(R(wing) * 0.35))

        # short swept wings (membrane) — span clears the aerial wingspan floor
        if dying:
            for sgn, part in ((-1, "back_wing"), (1, "wing")):
                root = (cx + sgn * R(2), cy + R(1))
                buf.fill_triangle(root, (root[0] + sgn * R(6), root[1] + R(3)),
                                  (root[0] + sgn * R(1), root[1] + R(4)),
                                  membrane[0], max(membrane[1] - 1, 0), part=part)
        else:
            for sgn, part, shade in ((-1, "back_wing", 1), (1, "wing", 0)):
                root = (cx + sgn * R(3), cy - R(1))
                tip = (cx + sgn * R(13 * wm), cy + R(2) + beat)
                trail = (cx + sgn * R(4), cy + R(4))
                buf.fill_triangle(root, tip, trail, membrane[0],
                                  max(membrane[1] - shade, 0), part=part)

        # fat fuselage (skin) with a shaded underbelly band
        buf.fill_ellipse(cx, cy, R(8), R(4.5), skin[0], skin[1], part="body")
        buf.fill_ellipse(cx, cy + R(2), R(6.5), R(2.4), skin[0], dark_skin, part="bodyshade")
        # blunt head at the front (facing right)
        hx, hy = cx + R(7) + R(hdx), cy - R(1) + R(hdy)
        buf.fill_ellipse(hx, hy, R(3.4), R(3.0), skin[0], skin[1], part="head")
        # slung ordnance pod (belly) under the fuselage
        if not crumple:
            buf.fill_ellipse(cx - R(1), cy + R(5), R(3.6), R(2.4), belly[0], belly[1], part="pod")
        if mouth_open:
            buf.fill_rect(cx - R(1.6), cy + R(6.5), cx + R(1.6), cy + R(8.4),
                          hot[0], hot[1], part="payload")
        eo = unit.get("eye_offset", (0, 0))
        buf.set_px(hx + R(1) + eo[0], hy - R(0.6) + eo[1], *colors["eye"],
                   part="eye", no_outline=True)
        # blunt tail fin at the rear
        if not dying:
            buf.fill_triangle((cx - R(8), cy - R(1)), (cx - R(11), cy - R(5)),
                              (cx - R(6), cy + R(1)), skin[0], dark_skin, part="tailfin")

    # ------------------------------------------- leviathan (slow flying barge)
    # A vast slow cetacean barge: a long deep fuselage hull, a broad blunt head,
    # small stabilizer fins (kept wide enough for the wingspan floor), and a
    # flat dorsal deck. Built from stacked ellipses so the huge hull never bands.

    def _draw_leviathan(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        cfg = self.cfg
        s = cfg.s

        def R(v):
            return int(round(v * s))

        colors = unit["colors"]
        skin, belly, membrane = colors["skin"], colors["belly"], colors["membrane"]
        dark_skin = max(skin[1] - 1, 0)
        hot = colors["hot"]
        W, H = cfg.canvas
        wm = cfg.wing_mult
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        wing = pose.get("wing", -4)
        fold = pose.get("fold", False)
        crumple = pose.get("crumple", False)
        mouth_open = pose.get("mouth_open")
        cx = W // 2 - 1 + cfg.body_dx + R(bdx)
        cy = H // 2 + cfg.body_dy + R(bdy)
        dying = fold or crumple
        beat = 0 if dying else int(round(R(wing) * 0.3))

        # stabilizer fins (membrane) — small but span the wingspan floor
        if not dying:
            for sgn, part, shade in ((-1, "back_wing", 1), (1, "wing", 0)):
                root = (cx + sgn * R(5), cy + R(1))
                tip = (cx + sgn * R(14 * wm), cy + R(4) + beat)
                trail = (cx + sgn * R(6), cy + R(5))
                buf.fill_triangle(root, tip, trail, membrane[0],
                                  max(membrane[1] - shade, 0), part=part)

        # long deep hull: overlapping ellipses (no single huge flat fill -> no band)
        buf.fill_ellipse(cx, cy, R(12), R(5), skin[0], skin[1], part="body")
        buf.fill_ellipse(cx + R(4), cy, R(8), R(4.4), skin[0], skin[1], part="body2")
        buf.fill_ellipse(cx, cy + R(2.5), R(11), R(2.6), skin[0], dark_skin, part="hullshade")
        # dorsal deck + tower: a barge superstructure that adds height UPWARD (away from
        # the ground line), so a wide flat hull still clears the aerial body-height floor.
        if not dying:
            buf.fill_ellipse(cx - R(2), cy - R(6), R(7), R(2.4), skin[0], skin[1], part="deck")
            buf.fill_ellipse(cx - R(3), cy - R(9), R(2.6), R(2.4), skin[0], dark_skin, part="tower")
        # pale waterline belly band
        if not crumple:
            buf.fill_ellipse(cx, cy + R(3.5), R(9), R(1.6), belly[0], belly[1], part="belly")
        # broad blunt head at the front
        hx, hy = cx + R(11) + R(hdx), cy - R(1) + R(hdy)
        buf.fill_ellipse(hx, hy, R(4), R(3.4), skin[0], skin[1], part="head")
        if mouth_open:
            buf.fill_rect(hx - R(1.0), hy + R(1.2), hx + R(3.0), hy + R(2.6),
                          hot[0], hot[1], part="maw")
        eo = unit.get("eye_offset", (0, 0))
        buf.set_px(hx + R(1.4) + eo[0], hy - R(0.4) + eo[1], *colors["eye"],
                   part="eye", no_outline=True)
        # blunt flukes at the rear
        if not dying:
            buf.fill_triangle((cx - R(12), cy), (cx - R(15), cy - R(4)),
                              (cx - R(10), cy + R(1)), skin[0], dark_skin, part="flukeU")
            buf.fill_triangle((cx - R(12), cy + R(1)), (cx - R(15), cy + R(5)),
                              (cx - R(10), cy + R(2)), skin[0], dark_skin, part="flukeL")


# ===========================================================================
# Siege machine (battering-ram cart: wide low chassis, wheels, ram beam)
# ===========================================================================

class SiegeMachineTemplate:
    # Round 3: chassis colors come from the ELEMENT ramps (stone unit -> stone
    # greys/moss); leather appears only as trim (wheels, ram beam). The pale
    # 1 px crew-light satisfies art rule 11 (every unit carries an eye pixel).
    ROLE_DEFAULTS = {
        "hull": ("@primary", 1),
        "metal": ("mauve_grey", 1),
        "wheel": ("@secondary", 0),
        "wood": ("@secondary", 2),
        "accent": ("@primary", "@accent"),
        "eye": ("mauve_grey", 4),
    }
    WHITELIST_ROLES = ("accent", "eye")

    # 40 px wide: a 26 px chassis + protruding ram head + the 5 px contact
    # lunge + 2 px impact burst need the extra room over the 32 px FP canvas.
    canvas = (40, 32)

    def __init__(self, cfg=None):
        pass

    def animations(self):
        return FP_BIPED_ANIMS  # idle 2 / walk(roll) 4 / attack 4 / death 3

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            return [{"ram_dx": 0}, {"ram_dx": 0, "ram_dy": 1}]
        if anim == "walk":
            return [{"spoke": i, "bounce": (0, 1, 0, 1)[i]} for i in range(4)]
        if anim == "attack":
            # contact lunges the ram 5 px past rest (art fix: readable at 1x)
            table = {
                "windup": {"ram_dx": -2},
                "windup2": {"ram_dx": -3},
                "contact": {"ram_dx": 5, "burst": True},
                "recover": {"ram_dx": 2},
                "settle": {"ram_dx": 0},
            }
            return [dict(table[k]) for k in attack_pose_keys(contact_idx)]
        if anim == "death":
            return [{"ram_dx": -1, "ram_dy": 2}, {"collapse": 1}, {"collapse": 2}]
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        colors = unit["colors"]
        hull, metal, wheel = colors["hull"], colors["metal"], colors["wheel"]
        wood = colors["wood"]
        acc = colors["accent"]
        GY = ground_row(self.canvas[1])
        bounce = pose.get("bounce", 0)
        collapse = pose.get("collapse", 0)

        if collapse:
            off = collapse
            for wx in (9 - off, 17, 25 + off):
                buf.fill_ellipse(wx, GY - 3, 3, 3, wheel[0], wheel[1], part="wheel")
            buf.fill_round_rect(4, 17 + collapse * 2, 29, GY - 3, hull[0], hull[1],
                                part="chassis")
            # ram beam dropped flat on the ground in front
            buf.fill_rect(8, GY - 2, 30, GY - 1, wood[0], wood[1],
                          part="prop_ram", no_outline=True)
            buf.fill_rect(31, GY - 3, 32, GY, metal[0], metal[1],
                          part="prop_ram_head", no_outline=True)
            return

        # wheels (grounded), rotating 3 px spoke line during the roll (a 1 px
        # spoke is sub-cluster and would be dissolved by the cleanup pass)
        spoke = pose.get("spoke", 0)
        for i, wx in enumerate((9, 17, 25)):
            buf.fill_ellipse(wx, GY - 3, 3, 3, wheel[0], wheel[1], part=f"wheel{i}")
            ddx, ddy = ((0, -2), (2, 0), (0, 2), (-2, 0))[spoke % 4]
            buf.line(wx, GY - 3, wx + ddx, GY - 3 + ddy, metal[0], metal[1],
                     part=f"hub{i}", no_outline=True)

        # chassis: 26 px wide with a 2-tier roof -- with the wheels the body
        # clears the siege mass floor (26w x 20h; a siege engine outweighs
        # infantry). Tiers share the chassis part/color so the shading pass
        # treats them as one mass (a pre-darkened cap reads as 1px banding).
        cy0 = 14 - bounce
        buf.fill_round_rect(4, cy0, 29, 23 - bounce, hull[0], hull[1], part="chassis")
        buf.fill_rect(6, cy0 - 2, 27, cy0 - 1, hull[0], hull[1], part="chassis")
        buf.fill_rect(8, cy0 - 4, 25, cy0 - 3, hull[0], hull[1], part="chassis")
        buf.fill_rect(16, cy0 + 2, 17, cy0 + 2, acc[0], acc[1], part="emblem",
                      no_outline=True)
        # pale crew-light in the front viewing slit (art rule 11, 1 px)
        buf.set_px(27, cy0 + 1, *colors["eye"], part="eye", no_outline=True)

        # battering ram: mid-tone 2 px beam protruding past the chassis front
        # into a tall metal head; the assembly slides on attack (pull -> slam)
        dx = pose.get("ram_dx", 0)
        by0 = 19 + pose.get("ram_dy", 0) - bounce
        buf.fill_rect(10 + dx, by0, 30 + dx, by0 + 1, wood[0], wood[1],
                      part="prop_ram", no_outline=True)
        buf.fill_rect(31 + dx, by0 - 2, 32 + dx, by0 + 3, metal[0], metal[1],
                      part="prop_ram_head", no_outline=True)
        if pose.get("burst"):
            # 2 px impact burst at the strike point -- pale light color (the
            # whitelisted eye hex), not the muted stone accent: it must pop at 1x
            buf.set_px(33 + dx, by0, *colors["eye"], part="burst", no_outline=True)
            buf.set_px(33 + dx, by0 + 1, *colors["eye"], part="burst", no_outline=True)


# ===========================================================================
# Slime (novel morphology: a limbless blob -- no skeleton)
# ===========================================================================

class SlimeTemplate:
    """A wobbling limbless dome. Implements the BodyPlanTemplate contract
    directly (no biped skeleton) -- the proof that the family admits a genuinely
    different morphology, not just reproportioned humanoids. Wobbles in place
    (squash/stretch) so every grounded frame keeps its base on the floor line;
    the attack lunges the mass forward."""

    ROLE_DEFAULTS = {
        "body": ("@primary", 1),
        "belly": ("@secondary", 2),
        "accent": ("@primary", "@accent"),
        "eye": ("mauve_grey", 4),
    }
    WHITELIST_ROLES = ("accent", "eye")

    # 48px wide: a settled ooze spreads a soft-body skirt well past its dome and
    # the forward attack lunge needs the room -- a low wide blob, not a 32px ball.
    canvas = (48, 32)

    def __init__(self, cfg=None):
        pass

    def animations(self):
        return FP_BIPED_ANIMS  # idle 2 / move 4 / attack 4 / death 3

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            return [{"squash": 0}, {"squash": 1}]
        if anim == "walk":
            # wobble in place (a hop would lift the base off the ground line and
            # fail the grounded-frame lint); lateral motion is engine translation.
            return [{"squash": s} for s in (0, 1, 0, 2)]
        if anim == "attack":
            table = {
                "windup": {"squash": 2},
                "windup2": {"squash": 3},
                "contact": {"lunge": 6, "squash": -2, "burst": True},
                "recover": {"lunge": 2},
                "settle": {"squash": 0},
            }
            return [dict(table[k]) for k in attack_pose_keys(contact_idx)]
        if anim == "death":
            return [{"squash": 3}, {"melt": 1}, {"melt": 2}]
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        colors = unit["colors"]
        body, belly, acc = colors["body"], colors["belly"], colors["accent"]
        W, H = self.canvas
        GY = ground_row(H)
        cx = W // 2 - 1 + pose.get("lunge", 0)

        melt = pose.get("melt", 0)
        if melt:
            ry = max(2, 5 - 2 * melt)
            buf.fill_ellipse(cx, GY - ry, 16, ry, body[0], body[1], part="body")
            return

        squash = pose.get("squash", 0)
        # wide + FLAT (a puddle, not a round dome): the low aspect ratio is what
        # separates the slime's silhouette from vertical bodies (ogre/robe mass).
        rx = 16 + squash       # squash widens + flattens, stretch (lunge) narrows
        ry = 7 - squash
        cy = GY - ry           # base sits exactly on the ground line
        buf.fill_ellipse(cx, cy, rx, ry, body[0], body[1], part="body")
        # SOFT-BODY base: where the ooze meets the floor it spreads + flattens
        # into a wide low skirt, conforming to the ground like settled jelly --
        # this is what kills the 'bouncing ball' read. The skirt flares wider as
        # the body squashes/settles and pulls in when it stretches up to lunge.
        spread = rx + 5 + max(0, squash)
        buf.fill_ellipse(cx, GY - 1, spread, 1, body[0], body[1], part="body")
        buf.fill_ellipse(cx, GY - 3, spread - 4, 1, body[0], body[1], part="body")
        # bumpy, lopsided outline -- an ooze, not a stamped circle. A left-heavy
        # crown bulge + small rim blebs break the smooth ellipse; each is small so
        # the puddle stays flat (distinct from the vertical ogre/robe mass) and no
        # bleb lengthens a banding-run horizontal shading band.
        buf.fill_ellipse(cx - rx // 3, cy - 1, rx // 4 + 2, max(2, ry - 2),
                         body[0], body[1], part="body")
        for bx, by, br in ((rx - 4, -1, 2), (-rx + 5, 1, 2), (rx // 3, -ry + 1, 2)):
            buf.fill_ellipse(cx + bx, cy + by, br, br, body[0], body[1], part="body")
        # a couple of thin drip nubs sliding to the floor at uneven x (wet bottom)
        for dx in (-rx + 6, rx - 6):
            buf.set_px(cx + dx, GY, body[0], body[1], part="body")
        # lighter belly band in the lower dome
        buf.fill_ellipse(cx, GY - max(2, ry // 2), rx - 3, max(2, ry // 2),
                         belly[0], belly[1], part="belly")
        # froth specks (BODY ramp dropped into the belly's upper-shading row): a
        # different ramp fragments the belly's wide flat @secondary shading band
        # into <7px runs AND reads as bubbles welling up through the underbelly.
        for bx in (-7, -2, 4, 9):
            buf.set_px(cx + bx, GY - 4, body[0], body[1], part="body", no_outline=True)
        # crown gloss + a short wet rivulet down the lean side (whitelisted accent)
        buf.fill_rect(cx - rx // 3 - 1, cy - ry, cx - rx // 3, cy - ry, acc[0], acc[1],
                      part="emblem", no_outline=True)
        buf.set_px(cx - rx // 3, cy - ry + 2, acc[0], acc[1], part="emblem", no_outline=True)
        # asymmetric eyes (one a touch higher) near the crown
        buf.set_px(cx + 3, cy - 1, *colors["eye"], part="eye", no_outline=True)
        buf.set_px(cx - 3, cy - 2, *colors["eye"], part="eye", no_outline=True)
        if pose.get("burst"):
            buf.set_px(cx + rx + 1, GY - 2, *colors["eye"], part="burst", no_outline=True)


# ===========================================================================
# Registry
# ===========================================================================

def parse_canvas(value) -> tuple[int, int]:
    """'48x48' / [48, 48] -> (48, 48)."""
    if isinstance(value, str):
        w, h = (int(v) for v in value.lower().split("x"))
        return (w, h)
    return (int(value[0]), int(value[1]))


def flyer_config_from_spec(spec: dict | None) -> FlyerConfig:
    """Spec keys: ``canvas`` ('WxH') + optional ``flyer`` dict of knobs.
    Absent spec -> the byte-stable 32x32 whelp default."""
    if not spec:
        return FlyerConfig()
    canvas = parse_canvas(spec.get("canvas", (32, 32)))
    fl = dict(spec.get("flyer") or {})
    return FlyerConfig(
        canvas=canvas,
        s=float(fl.get("scale", canvas[1] / 32)),
        wing_mult=float(fl.get("wing_mult", 1.0)),
        tail_mult=float(fl.get("tail_mult", 1.0)),
        head_style=fl.get("head", "wyvern"),
        crest=fl.get("crest", "none"),
        fire_tail=bool(fl.get("fire_tail", False)),
        eye_px=int(fl.get("eye_px", 1 if canvas[1] <= 32 else 2)),
        eye_shape=str(fl.get("eye_shape", "round")),
        boss=bool(fl.get("boss", False)),
        seed=int(fl.get("seed", 0)),
        body_dx=int(fl.get("body_dx", 0)),
        body_dy=int(fl.get("body_dy", 0)),
        dragon=bool(fl.get("dragon", False)),
        feather_wing=bool(fl.get("feather_wing", False)),
        insect_wing=bool(fl.get("insect_wing", False)),
        body_plan=str(fl.get("body_plan", "drake")),
    )


def _overlay_biped_config(base: BipedConfig, spec: dict | None) -> BipedConfig:
    """Overlay per-unit shape-language (build / seed / proportions) onto a base
    BipedConfig, scaling the silhouette to the size tier the spec's canvas
    implies. A larger ``canvas`` than the base's drives ``s = canvas_h /
    base_h``; every body proportion (base, or a per-unit override) scales by s
    so a unit's Part-B distinct shape is preserved at any tier. A same-size
    canvas with no overrides -> the base unchanged (byte-identical)."""
    if not spec:
        return base
    target = parse_canvas(spec["canvas"]) if spec.get("canvas") else base.canvas
    s = target[1] / base.canvas[1]
    over: dict = {}
    if spec.get("build"):
        over["build"] = spec["build"]
    if "seed" in spec:
        over["seed"] = int(spec["seed"])
    prop = spec.get("proportions") or {}
    for k in ("head_w", "head_h", "torso_w", "torso_h", "leg_h"):
        src = prop[k] if k in prop else getattr(base, k)
        scaled = max(1, round(src * s))
        if k in prop or scaled != getattr(base, k):
            over[k] = scaled
    if "head_fwd" in prop:
        over["head_fwd"] = int(prop["head_fwd"])
    if spec.get("construct_style"):
        over["construct_style"] = spec["construct_style"]
    if spec.get("head_style"):
        # upper-body knob: pick a head independent of the typeclass default
        # (e.g. a hood on a wraith, a snout on a beast).
        over["head_style"] = spec["head_style"]
    if spec.get("eye_shape"):
        over["eye_shape"] = spec["eye_shape"]  # identity accent: slit (reptile) | round
    if spec.get("attack_pose"):
        # the archetype magic pose (cast/channel/body_strike) drives the arm via
        # ARM_POSES, overriding the typeclass's default weapon pose.
        over["attack_style"] = spec["attack_pose"]
    if spec.get("base"):
        # explicit base archetype (composition slot) -- overrides the typeclass
        # default base (e.g. a humanoid biped on a spider or flying-mount base).
        over["locomotion"] = LOCOMOTION_PARTS[spec["base"]]
    if s != 1.0:
        over["canvas"] = target
        over["scale"] = s
    return replace(base, **over) if over else base


def biped_config_from_spec(typeclass: str, spec: dict | None) -> BipedConfig:
    """Overlay per-unit shape-language onto the base typeclass config (mirrors
    ``flyer_config_from_spec``). Spec keys: ``build`` (neutral|agile|sturdy|
    dangerous), ``seed`` (deterministic int), optional ``proportions`` dict
    overriding head_w/head_h/torso_w/torso_h/leg_h/head_fwd. Absent keys -> the
    base config unchanged (byte-identical to the pre-Part-B rig)."""
    return _overlay_biped_config(BIPED_CONFIGS[typeclass], spec)


def ogre_config_from_spec(spec: dict | None) -> BipedConfig:
    """OGRE_CONFIG with the same optional per-unit overlay as the bipeds (seed
    asymmetry, proportion tuning). Defaults the base to the ogre stumps unless the
    spec names an explicit ``base``."""
    cfg = _overlay_biped_config(OGRE_CONFIG, spec)
    if cfg.locomotion is None:
        cfg = replace(cfg, locomotion=OGRE_STUMPS)
    return cfg


def construct_config_from_spec(spec: dict | None) -> BipedConfig:
    """CONSTRUCT_CONFIG with the per-unit overlay (build/seed/proportions/scale)
    plus the ``construct_style`` selector (segmented | crystalline). Defaults the
    base per style (segmented -> pillars, crystalline -> see _default_construct_base)
    unless the spec names an explicit ``base``."""
    cfg = _overlay_biped_config(CONSTRUCT_CONFIG, spec)
    if cfg.locomotion is None:
        cfg = replace(cfg, locomotion=_default_construct_base(cfg.construct_style))
    return cfg


class WormTemplate:
    """A segmented burrower breaching the lane: a chain of overlapping ellipse segments
    arcing out of the ground and back in, head at the front. Directly implements the
    BodyPlanTemplate contract (no skeleton). Band-safe — overlapping ellipse segments, never
    a wide flat fill. Both ends rest on the ground line (a breach, not a float)."""

    ROLE_DEFAULTS = {
        "body": ("@primary", 2),
        "belly": ("@secondary", 2),
        "accent": ("@primary", "@accent"),
        "eye": ("mauve_grey", 4),
    }
    WHITELIST_ROLES = ("accent", "eye")
    canvas = (48, 32)

    def __init__(self, cfg=None):
        pass

    def animations(self):
        return FP_BIPED_ANIMS  # idle 2 / walk 4 / attack 4 / death 3

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            return [{"peak": 8}, {"peak": 7}]
        if anim == "walk":
            return [{"peak": p} for p in (7, 8, 9, 8)]  # peristalsis ripple
        if anim == "attack":
            table = {
                "windup": {"peak": 10},
                "windup2": {"peak": 11},
                "contact": {"peak": 9, "lunge": 5, "maw": True},
                "recover": {"peak": 8, "lunge": 2},
                "settle": {"peak": 8},
            }
            return [dict(table[k]) for k in attack_pose_keys(contact_idx)]
        if anim == "death":
            return [{"peak": 5}, {"peak": 3}, {"peak": 1}]  # the arc flattens / sinks
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        colors = unit["colors"]
        body, belly, acc = colors["body"], colors["belly"], colors["accent"]
        dark = max(body[1] - 1, 0)
        W, H = self.canvas
        GY = ground_row(H)
        cx = W // 2 - 1
        peak = pose.get("peak", 8)
        lunge = pose.get("lunge", 0)
        maw = pose.get("maw", False)
        n = 6
        span = 17
        seg = []
        for i in range(n):
            t = i / (n - 1)                       # 0 (back) .. 1 (front/head)
            end = i == 0 or i == n - 1
            rw = 4 if end else 5
            rh = 3 if end else 4
            x = cx - span + round(t * 2 * span) + lunge
            # inverted-U arc: ends rest on the ground line, the middle humps up
            y = GY - rh - round(peak * math.sin(math.pi * t))
            seg.append((x, y, rw, rh))
        # back-to-front so the head segment composites on top
        for i, (x, y, rw, rh) in enumerate(seg):
            idx = dark if i % 2 == 0 else body[1]
            buf.fill_ellipse(x, y, rw, rh, body[0], idx, part=f"seg{i}")
        # pale plated underside on the apex segment
        ax, ay, arw, arh = seg[n // 2]
        buf.fill_ellipse(ax, ay + 1, arw - 1, 1, belly[0], belly[1], part="belly")
        # head (front): eye, a chitin accent ridge, and a maw on the strike
        hx, hy, hrw, hrh = seg[-1]
        eo = unit.get("eye_offset", (0, 0))
        buf.set_px(hx + eo[0], hy - 1 + eo[1], *colors["eye"], part="eye", no_outline=True)
        buf.fill_ellipse(hx, hy - hrh, 2, 1, acc[0], acc[1], part="accent")
        if maw:
            buf.fill_rect(hx + hrw - 1, hy, hx + hrw + 1, hy + 1, body[0], dark, part="maw")


def make_template(typeclass: str, spec: dict | None = None):
    if typeclass in BIPED_CONFIGS:
        return BipedTemplate(biped_config_from_spec(typeclass, spec))
    if typeclass == "ogre":
        return OgreTemplate(ogre_config_from_spec(spec))
    if typeclass == "construct":
        return ConstructTemplate(construct_config_from_spec(spec))
    if typeclass == "aerial_flyer":
        return AerialFlyerTemplate(flyer_config_from_spec(spec))
    if typeclass == "siege_machine":
        return SiegeMachineTemplate()
    if typeclass == "slime":
        return SlimeTemplate()
    if typeclass == "burrower":
        return WormTemplate()
    raise KeyError(f"unknown typeclass '{typeclass}'; available: {TEMPLATE_NAMES}")


TEMPLATE_NAMES = (sorted(BIPED_CONFIGS)
                  + ["ogre", "construct", "aerial_flyer", "siege_machine", "slime", "burrower"])
