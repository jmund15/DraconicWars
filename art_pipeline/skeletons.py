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

from dataclasses import dataclass

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


def apply_directional_shading(buf: PixelBuffer, pal: Palette, outline: dict) -> None:
    """One global light, top-left 45 deg.

    Highlight (+1 ramp step) on interior pixels hugging lit outline above/left,
    upper half of each part. Shadow (-1) hugging shadow outline below/right,
    lower half, plus a 1 px ambient-occlusion line under overlapping parts.
    Band depth staggers 1-2 px (middle third of each part) to avoid banding.
    Parts named ``back_*`` are pre-darkened and stay flat (depth cue).
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

        if hi and not lo:
            updates[(x, y)] = min(cell.idx + 1, ramp_len - 1)
        elif lo and not hi:
            updates[(x, y)] = max(cell.idx - 1, 0)
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
            # thick outline (lint rule d would flag the resulting 2x2 blocks)
            ink_hex = pal.hex_of(*INK)
            cands = sorted(counts.items(), key=lambda kv: (kv[0] == ink_hex, -kv[1][0]))
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
    outline = apply_selout_outline(buf, pal)
    apply_directional_shading(buf, pal, outline)
    dissolve_small_clusters(buf, pal, set(protected_hexes))


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


def attack_pose_keys(contact_idx: int, frames: int = 4) -> list[str]:
    """Anticipation poses before the contact frame, recovery after.

    The sim owns timing (art doc section 10): the generator receives the
    contact index (derived from foreswing ticks) and shapes the wind-up to
    visually align with it.
    """
    contact_idx = max(1, min(contact_idx, frames - 2))
    keys = ["windup", "windup2", "windup3"][:contact_idx]
    keys.append("contact")
    keys.extend(["recover", "settle", "settle"][: frames - 1 - contact_idx])
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
    attack_style: str        # 'thrust' | 'sling' | 'bow' | 'staff'
    robe: bool = False
    head_fwd: int = 1


BIPED_CONFIGS = {
    "melee_biped": BipedConfig("melee_biped", (32, 32), 9, 9, 9, 9, 9,
                               "snout", "thrust"),
    "ranged_biped": BipedConfig("ranged_biped", (32, 32), 9, 9, 7, 8, 8,
                                "round_ears", "sling"),
    "sniper_biped": BipedConfig("sniper_biped", (48, 64), 10, 11, 11, 16, 20,
                                "plain", "bow"),
    "support_robed": BipedConfig("support_robed", (32, 32), 8, 8, 9, 9, 9,
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
        "idle": (3, 4), "idle_b": (3, 5), "walk": (3, 4),
        "windup": (3, 4), "windup2": (3, 4), "contact": (3, 4),
        "recover": (3, 4), "settle": (3, 4),
        "drop": (1, 8), "drop2": (0, 10),
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


class BipedTemplate:
    ROLE_DEFAULTS = {
        "skin": ("@secondary", 2),
        "cloth": ("@primary", 1),
        "wood": ("leather", 0),
        "metal": ("mauve_grey", 1),
        "accent": ("@primary", "@accent"),
        "eye": ("ink", 0),
    }

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

        skin = colors["skin"]
        cloth = colors["cloth"]

        # --- legs / robe (drawn first; torso overlaps the hip line) -------
        if cfg.robe:
            self._draw_robe(buf, pose, tx0, tx1, ty1, GY, cloth)
        else:
            legs_kind, phase = pose.get("legs", ("stand", 0))
            self._draw_legs(buf, legs_kind, phase, tx0, tx1, leg_top - lift, GY, skin)

        # --- torso ---------------------------------------------------------
        buf.fill_round_rect(tx0, ty0, tx1, ty1, cloth[0], cloth[1], part="torso")

        # --- back arm (skip when an off-hand prop covers that side) --------
        offhand = [p for p in unit["props"] if p in ("shield", "small_shield")]
        if not offhand and not cfg.robe:
            buf.fill_rect(tx0 - 1, ty0 + 2, tx0, ty0 + 5, skin[0],
                          max(skin[1] - 1, 0), part="back_arm")

        # --- head -----------------------------------------------------------
        self._draw_head(buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal)

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

    def _draw_head(self, buf, hx0, hy0, hx1, hy1, skin, cloth, colors, pal):
        rx = (hx1 - hx0) / 2
        ry = (hy1 - hy0) / 2
        cx = (hx0 + hx1) / 2
        cy = (hy0 + hy1) / 2
        style = self.cfg.head_style
        if style == "hood":
            buf.fill_ellipse(cx, cy, rx, ry, cloth[0], cloth[1], part="head")
            buf.fill_rect(int(cx), int(cy) - 1, hx1, int(cy) + 2, skin[0], skin[1], part="face")
            buf.set_px(hx1 - 1, int(cy), *colors["eye"], part="eye", no_outline=True)
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
        buf.set_px(eye_x, eye_y, *colors["eye"], part="eye", no_outline=True)

    def _draw_lying(self, buf, unit, pal, GY):
        colors = unit["colors"]
        skin, cloth = colors["skin"], colors["cloth"]
        W = self.cfg.canvas[0]
        cx = W // 2 - 1
        buf.fill_ellipse(cx + 1, GY - 2, 6, 2, cloth[0], cloth[1], part="torso")
        buf.fill_ellipse(cx - 7, GY - 2, 3, 2, skin[0], skin[1], part="head")
        buf.fill_rect(cx + 6, GY - 1, cx + 9, GY, skin[0], skin[1], part="front_leg")
        buf.fill_rect(cx - 1, GY - 4, cx + 2, GY - 3, skin[0], skin[1], part="arm")
        buf.set_px(cx - 7, GY - 3, *colors["eye"], part="eye", no_outline=True)

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


class AerialFlyerTemplate:
    """Wyvern/whelp-shaped flyer. Hovers above the ground line (engine adds the
    drop-shadow ellipse, art-direction.md section 6)."""

    ROLE_DEFAULTS = {
        "skin": ("@primary", 2),
        "belly": ("@secondary", 3),
        "membrane": ("@secondary", 1),
        "accent": ("@primary", "@accent"),
        "eye": ("ink", 0),
    }

    canvas = (32, 32)

    def __init__(self, cfg=None):
        pass

    def animations(self):
        return FP_AERIAL_ANIMS

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            return [{"wing": -5, "body": (0, 0)}, {"wing": -2, "body": (0, 1)}]
        if anim == "fly":
            return [{"wing": FLY_WING_DY[i], "body": (0, FLY_BODY_DY[i])} for i in range(6)]
        if anim == "attack":
            table = {
                "windup": {"wing": -6, "body": (-1, -1), "head": (-1, -1)},
                "windup2": {"wing": -8, "body": (-2, -1), "head": (-1, -2)},
                "contact": {"wing": 2, "body": (3, 1), "head": (3, 2), "mouth_open": True},
                "recover": {"wing": -2, "body": (1, 0), "head": (1, 1)},
                "settle": {"wing": -4, "body": (0, 0), "head": (0, 0)},
            }
            return [dict(table[k]) for k in attack_pose_keys(contact_idx)]
        if anim == "death":
            return [
                {"wing": 4, "body": (0, 1), "head": (0, 2)},
                {"wing": 7, "body": (0, 3), "head": (1, 5), "fold": True},
                {"wing": 8, "body": (0, 5), "head": (0, 7), "fold": True, "crumple": True},
            ]
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        colors = unit["colors"]
        skin, belly = colors["skin"], colors["belly"]
        membrane = colors["membrane"]
        bdx, bdy = pose.get("body", (0, 0))
        hdx, hdy = pose.get("head", (0, 0))
        bx, by = 15 + bdx, 17 + bdy
        wing_dy = pose.get("wing", -4)
        fold = pose.get("fold", False)

        # far wing (behind body): pre-darkened, flat
        root_far = (bx - 2, by - 4)
        far_dark = max(membrane[1] - 1, 0)
        if fold:
            buf.fill_triangle(root_far, (root_far[0] - 4, root_far[1] + 2),
                              (root_far[0] - 1, root_far[1] + 3),
                              membrane[0], far_dark, part="back_wing")
        else:
            buf.fill_triangle(root_far, (root_far[0] - 9, root_far[1] + wing_dy + 1),
                              (root_far[0] - 2, root_far[1] + 3),
                              membrane[0], far_dark, part="back_wing")

        # tail
        buf.line(bx - 4, by + 1, bx - 9, by + 5, skin[0], max(skin[1] - 1, 0),
                 part="tail", width=2)
        buf.set_px(bx - 10, by + 6, skin[0], max(skin[1] - 1, 0), part="tail")

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
        if pose.get("mouth_open"):
            buf.fill_rect(hx + 2, hy - 1, hx + 4, hy - 1, skin[0], skin[1], part="head")
            buf.fill_rect(hx + 2, hy + 1, hx + 4, hy + 1, skin[0], skin[1], part="head")
        else:
            buf.fill_rect(hx + 2, hy, hx + 4, hy + 1, skin[0], skin[1], part="head")
        acc = colors["accent"]
        buf.set_px(hx - 1, hy - 3, acc[0], acc[1], part="horn", no_outline=True)
        buf.set_px(hx, hy - 3, acc[0], acc[1], part="horn", no_outline=True)
        buf.set_px(hx + 1, hy - 1, *colors["eye"], part="eye", no_outline=True)

        # near wing (over body), wing-finger ridge along the top edge
        root = (bx + 1, by - 3)
        if fold:
            buf.fill_triangle(root, (root[0] - 5, root[1] + 1), (root[0] - 1, root[1] + 3),
                              membrane[0], membrane[1], part="wing")
        else:
            tip = (root[0] - 7, root[1] + wing_dy)
            buf.fill_triangle(root, tip, (root[0] + 2, root[1] + 2),
                              membrane[0], membrane[1], part="wing")
            # wing-finger ridge: darkest step so the boundary stays sel-out dark
            buf.line(root[0], root[1], tip[0], tip[1], skin[0], 0,
                     part="wing_finger", no_outline=True)


# ===========================================================================
# Siege machine (mortar cart)
# ===========================================================================

class SiegeMachineTemplate:
    ROLE_DEFAULTS = {
        "hull": ("leather", 1),
        "metal": ("mauve_grey", 1),
        "wheel": ("leather", 0),
        "accent": ("@primary", "@accent"),
        "eye": ("ink", 0),
    }

    canvas = (48, 48)

    def __init__(self, cfg=None):
        pass

    def animations(self):
        return FP_BIPED_ANIMS  # idle 2 / walk(roll) 4 / attack 4 / death 3

    def poses(self, anim: str, contact_idx: int) -> list[dict]:
        if anim == "idle":
            return [{"spoke": 0}, {"spoke": 0, "tube_sag": 1}]
        if anim == "walk":
            return [{"spoke": i, "bounce": (0, 1, 0, 1)[i]} for i in range(4)]
        if anim == "attack":
            table = {
                "windup": {"spoke": 0, "recoil": -1, "tube_sag": 1},
                "windup2": {"spoke": 0, "recoil": -2, "tube_sag": 2},
                "contact": {"spoke": 0, "recoil": 1, "flare": True},
                "recover": {"spoke": 0, "recoil": 0},
                "settle": {"spoke": 0, "recoil": 0},
            }
            return [dict(table[k]) for k in attack_pose_keys(contact_idx)]
        if anim == "death":
            return [{"spoke": 0, "tube_sag": 3}, {"collapse": 1}, {"collapse": 2}]
        raise KeyError(anim)

    def draw_pose(self, buf: PixelBuffer, pose: dict, unit: dict, pal: Palette) -> None:
        colors = unit["colors"]
        hull, metal, wheel = colors["hull"], colors["metal"], colors["wheel"]
        acc = colors["accent"]
        GY = ground_row(48)
        bounce = pose.get("bounce", 0)
        collapse = pose.get("collapse", 0)

        if collapse:
            top = 34 + collapse * 2
            buf.fill_round_rect(10, top, 37, GY, hull[0], hull[1], part="chassis")
            buf.line(14, GY - 1, 30, GY - 1, metal[0], metal[1], part="tube", width=2)
            for wx in (12, 35):
                buf.fill_ellipse(wx, GY - 3, 3, 3, wheel[0], wheel[1], part="wheel")
            return

        # wheels (grounded)
        spoke = pose.get("spoke", 0)
        for i, wx in enumerate((15, 24, 33)):
            buf.fill_ellipse(wx, GY - 4, 4, 4, wheel[0], wheel[1], part=f"wheel{i}")
            buf.fill_rect(wx, GY - 4, wx + 1, GY - 3, metal[0], metal[1], part=f"hub{i}")
            ddx, ddy = ((0, -3), (3, 0), (0, 3), (-3, 0))[spoke % 4]
            buf.set_px(wx + ddx, GY - 4 + ddy, metal[0], metal[1], part=f"hub{i}")

        # chassis + accent stripe
        cy0 = 29 - bounce
        buf.fill_round_rect(10, cy0, 37, 38 - bounce, hull[0], hull[1], part="chassis")
        buf.fill_rect(13, cy0 + 4, 30, cy0 + 4, acc[0], acc[1], part="stripe", no_outline=True)

        # mortar tube
        recoil = pose.get("recoil", 0)
        sag = pose.get("tube_sag", 0)
        bx0, by0 = 22 + recoil, cy0 + 2
        bx1, by1 = 34 + recoil * 2, cy0 - 8 + sag * 2
        buf.line(bx0, by0, bx1, by1, metal[0], metal[1], part="tube", width=2)
        buf.line(bx0 + 1, by0 + 1, bx1 + 1, by1 + 1, metal[0], metal[1], part="tube", width=2)
        if pose.get("flare"):
            buf.fill_rect(bx1 + 1, by1 - 2, bx1 + 2, by1 - 1, acc[0], acc[1],
                          part="muzzle", no_outline=True)


# ===========================================================================
# Registry
# ===========================================================================

def make_template(typeclass: str):
    if typeclass in BIPED_CONFIGS:
        return BipedTemplate(BIPED_CONFIGS[typeclass])
    if typeclass == "aerial_flyer":
        return AerialFlyerTemplate()
    if typeclass == "siege_machine":
        return SiegeMachineTemplate()
    raise KeyError(
        f"unknown typeclass '{typeclass}'; available: "
        f"{sorted(BIPED_CONFIGS) + ['aerial_flyer', 'siege_machine']}"
    )


TEMPLATE_NAMES = sorted(BIPED_CONFIGS) + ["aerial_flyer", "siege_machine"]
