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

from dataclasses import dataclass, replace

import props as props_mod
from palette import Palette

GROUND_MARGIN = 2          # empty rows below the feet line
LIGHT_SOURCE = "top-left 45deg"  # global; baked into outline + shading passes
INK = ("ink", 0)           # near-black #2e222f -- reserved for outline / eyes


def ground_row(canvas_h: int) -> int:
    """Last opaque row for a grounded unit (feet line)."""
    return canvas_h - GROUND_MARGIN - 1


# ===========================================================================
# Pixel buffer + primitives
# ===========================================================================

@dataclass
class Cell:
    ramp: str
    idx: int
    part: str
    no_outline: bool = False  # thin props (shafts, straps) keep their own color


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
# Boss budget (pyraxis): idle 4 / fly 6 / attack 6 / death 6.
BOSS_AERIAL_ANIMS = [AnimDef("idle", 4, 7, True), AnimDef("fly", 6, 10, True),
                     AnimDef("attack", 6, 12, False), AnimDef("death", 6, 10, False)]


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
    # --- per-unit shape-language (Part B). 'neutral' reproduces the pre-Part-B
    # rig BYTE-IDENTICALLY (FP units carry no build); the others reshape the
    # silhouette so different-named units stop being one body recolored.
    build: str = "neutral"   # neutral | agile (circle) | sturdy (square) | dangerous (wedge)
    seed: int = 0            # per-unit asymmetry seed (deterministic int; NEVER hash())


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
                {"legs": ("stand", 0), "head": (0, 1), "arm": "idle_b", "prop": "idle_b"},
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
            buf.fill_rect(tx0 - 1, ty0 + 2, tx0, ty0 + 5, skin[0],
                          max(skin[1] - 1, 0), part="back_arm")

        # --- head -----------------------------------------------------------
        self._draw_head(buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal,
                        unit.get("eye_offset", (0, 0)))

        # --- shape-language features: dangerous = head horn + shoulder spikes
        if bld == "dangerous":
            self._draw_danger_features(buf, asym, tx0, tx1, ty0, hx1, hy0, skin)

        # --- off-hand prop (shield carried proud of the torso front-low) ----
        for p in offhand:
            props_mod.draw_offhand(buf, pal, p, (tx1 + 1, ty1 - 2), colors)

        # --- weapon + front arm (arm last so the hand grips the shaft) ------
        arm_key = pose.get("arm", "idle")
        adx, ady = ARM_POSES[cfg.attack_style][arm_key]
        hand = (tx1 + adx, ty0 + ady)
        self._draw_props(buf, unit, pal, pose, hand, GY, head_rect=(hx0, hy0, hx1, hy1))
        shoulder = (tx1 - 1, ty0 + 2)
        buf.limb(shoulder[0], shoulder[1], hand[0], hand[1], skin[0], skin[1],
                 part="arm", bend="horizontal")

    # ----------------------------------------------------------- part helpers

    def _draw_legs(self, buf, kind, phase, tx0, tx1, hip_y, GY, skin):
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
            # one raised per seed (asymmetry).
            buf.fill_round_rect(tx0, ty0 + 2, tx1, ty1, cr, ci, part="torso")
            up = asym["shoulder_up"]
            buf.fill_rect(tx0 - 1, ty0 + up, tx1 + 1, ty0 + 2, cr, ci, part="torso")
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
        spikes (skin-colored body protrusions, >=3 px so they survive cleanup)."""
        d = max(skin[1] - 1, 0)
        sp = asym["spike_dx"]
        buf.fill_triangle((hx1 - 1, hy0 + 1), (hx1 + 3 + sp, hy0 - 3),
                          (hx1 + 1, hy0 + 2), skin[0], d, part="horn")
        buf.fill_triangle((tx1 - 1, ty0), (tx1 + 3, ty0 - 4 - asym["shoulder_up"]),
                          (tx1 + 1, ty0 + 1), skin[0], d, part="spike")
        buf.fill_triangle((tx0 + 1, ty0), (tx0 - 2, ty0 - 3),
                          (tx0 - 1, ty0 + 1), skin[0], d, part="spike")

    def _draw_eye(self, buf, eye_x, eye_y, colors, eye_offset):
        """1-2 bright eye pixels (art fix 3), position-adjustable per spec."""
        ex, ey = eye_x + eye_offset[0], eye_y + eye_offset[1]
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
                                      ground_y, self.cfg.canvas)


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
    boss: bool = False          # boss budget: idle 4 / fly 6 / attack 6 / death 6
    body_dx: int = 0            # absolute canvas shift (right-edge headroom)
    body_dy: int = 0
    dragon: bool = False        # dedicated dragon anatomy (S-neck, fan wings)


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
                        {"wing": -2, "body": (0, 1)}, {"wing": -4, "body": (0, 1)}]
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
            frames = 6 if self.cfg.boss else 4
            return [dict(table[k]) for k in attack_pose_keys(contact_idx, frames)]
        if anim == "death":
            if self.cfg.boss:
                return [
                    {"wing": 3, "body": (0, 1), "head": (0, 1)},
                    {"wing": 5, "body": (0, 2), "head": (0, 3)},
                    {"wing": 7, "body": (0, 3), "head": (1, 4), "fold": True},
                    {"wing": 8, "body": (0, 4), "head": (1, 5), "fold": True},
                    {"wing": 8, "body": (0, 5), "head": (0, 6), "fold": True, "crumple": True},
                    {"wing": 8, "body": (0, 5), "head": (0, 7), "fold": True, "crumple": True},
                ]
            return [
                {"wing": 4, "body": (0, 1), "head": (0, 2)},
                {"wing": 7, "body": (0, 3), "head": (1, 5), "fold": True},
                {"wing": 8, "body": (0, 5), "head": (0, 7), "fold": True, "crumple": True},
            ]
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        if self.cfg.dragon:
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
            buf.fill_triangle(root_far, (root_far[0] - round(12 * wm), root_far[1] + wing_dy + 1),
                              (root_far[0] - 2, root_far[1] + 3),
                              membrane[0], far_dark, part="back_wing")

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
            buf.fill_triangle(root, tip, (root[0] + 2, root[1] + 2),
                              membrane[0], membrane[1], part="wing")
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
            tip_f = (rf[0] - R(12 * wm), rf[1] + wing_dy + R(1))
            sc1_f = (rf[0] - R(7.5 * wm), rf[1] + wing_dy + R(4.6))
            sc2_f = (rf[0] - R(2.5), rf[1] + R(3.2))
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
        for dx_, dy_ in [(0, 0), (-1, 0), (0, 1), (-1, 1)][: max(1, min(cfg.eye_px, 4))]:
            buf.set_px(ex + dx_, ey + dy_, *colors["eye"], part="eye", no_outline=True)

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
            fw = int(round(wing_dy * 0.9)) - R(1)
            tip_f = (rf[0] - R(9.5 * wm), rf[1] + fw)
            f1_f = (rf[0] - R(6.5 * wm), rf[1] + fw + R(2.6))
            f2_f = (rf[0] - R(3.0 * wm), rf[1] + R(1.8))
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
        hx = bx + R(6.4) + R(hdx)
        hy = by - R(7.2) + R(hdy)
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
            horn = colors["horn"]
            buf.fill_triangle((hx - R(1.2), hy - R(1.4)), (hx - R(4.6), hy - R(4.0)),
                              (hx + R(0.2), hy - R(1.8)), horn[0], horn[1], part="horn")
            buf.fill_triangle((hx + R(0.2), hy - R(1.8)), (hx - R(1.2), hy - R(5.6)),
                              (hx + R(1.4), hy - R(2.0)), horn[0], horn[1], part="horn2")
            buf.fill_triangle((hx + R(1.4), hy - R(2.0)), (hx + R(1.4), hy - R(5.0)),
                              (hx + R(2.6), hy - R(1.6)), horn[0], horn[1], part="horn3")
        if cfg.crest == "head":
            hot = colors["hot"]
            buf.fill_triangle((hx - R(0.4), hy - R(1.8)), (hx - R(3.4), hy - R(3.8)),
                              (hx + R(1.4), hy - R(2.0)), hot[0], hot[1], part="crest")

        eo = unit.get("eye_offset", (0, 0))
        ex, ey = hx + R(0.6) + eo[0], hy - R(0.7) + eo[1]
        for dx_, dy_ in [(0, 0), (-1, 0), (0, 1), (-1, 1)][: max(1, min(cfg.eye_px, 4))]:
            buf.set_px(ex + dx_, ey + dy_, *colors["eye"], part="eye", no_outline=True)

        # --- near wing: 3-panel fan + wrist-radiating finger spars -----------
        rn = (bx + R(1.2), by - R(2.4))
        if fold:
            buf.fill_triangle(rn, (rn[0] - R(6), rn[1] + R(1.2)),
                              (rn[0] - R(1), rn[1] + R(3.2)),
                              membrane[0], membrane[1], part="wing")
        else:
            tip = (rn[0] - R(10.5 * wm), rn[1] + wing_dy)
            f1 = (rn[0] - R(7.0 * wm), rn[1] + wing_dy + R(3.4))
            f2 = (rn[0] - R(3.2 * wm), rn[1] + int(round(wing_dy * 0.35)) + R(4.2))
            trail = (rn[0] - R(0.8), rn[1] + R(2.6))
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
        boss=bool(fl.get("boss", False)),
        body_dx=int(fl.get("body_dx", 0)),
        body_dy=int(fl.get("body_dy", 0)),
        dragon=bool(fl.get("dragon", False)),
    )


def biped_config_from_spec(typeclass: str, spec: dict | None) -> BipedConfig:
    """Overlay per-unit shape-language onto the base typeclass config (mirrors
    ``flyer_config_from_spec``). Spec keys: ``build`` (neutral|agile|sturdy|
    dangerous), ``seed`` (deterministic int), optional ``proportions`` dict
    overriding head_w/head_h/torso_w/torso_h/leg_h/head_fwd. Absent keys -> the
    base config unchanged (byte-identical to the pre-Part-B rig)."""
    base = BIPED_CONFIGS[typeclass]
    if not spec:
        return base
    over: dict = {}
    if spec.get("build"):
        over["build"] = spec["build"]
    if "seed" in spec:
        over["seed"] = int(spec["seed"])
    prop = spec.get("proportions") or {}
    for k in ("head_w", "head_h", "torso_w", "torso_h", "leg_h", "head_fwd"):
        if k in prop:
            over[k] = int(prop[k])
    return replace(base, **over) if over else base


def make_template(typeclass: str, spec: dict | None = None):
    if typeclass in BIPED_CONFIGS:
        return BipedTemplate(biped_config_from_spec(typeclass, spec))
    if typeclass == "aerial_flyer":
        return AerialFlyerTemplate(flyer_config_from_spec(spec))
    if typeclass == "siege_machine":
        return SiegeMachineTemplate()
    raise KeyError(
        f"unknown typeclass '{typeclass}'; available: "
        f"{sorted(BIPED_CONFIGS) + ['aerial_flyer', 'siege_machine']}"
    )


TEMPLATE_NAMES = sorted(BIPED_CONFIGS) + ["aerial_flyer", "siege_machine"]
