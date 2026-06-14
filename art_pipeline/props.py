"""props.py -- prop layer primitives drawn at a template's prop anchor.

Weapons (spear, sword, sling, bow, crossbow, staff), off-hand (small shield),
and headgear (helmet, crest). Element-accent coloring comes from the unit's
resolved ``colors`` dict (role -> (ramp, index)), which generate_unit.py builds
through the Palette API -- no raw colors here.

Round-2 calibration conventions:
- Shafts/stocks/limbs are 2 px wide in MID-TONE wood (leather ramp mid step),
  drawn ``no_outline`` so the sel-out pass cannot ink them solid; the manifest
  ``prop_colors`` list exempts them from the outline-coverage lint. They must
  break the body silhouette in idle AND walk frames (art rule 1 at 1x).
- Bright element accents (spearhead, arrow/bolt tips, staff gem) are small
  deliberate ``no_outline`` clusters, whitelisted and capped together with the
  eyes at 6 px per frame (lint whitelist cap).
- Strings are 1 px ink (outline-eligible), never the eye color.
- Poses mirror the skeleton arm pose keys: idle / idle_b / walk / windup /
  windup2 / contact / recover / settle / drop / dropped.

Round-3 calibration conventions (TEMPLATE-level rules):
- NO BAKED PROJECTILES: ranged/sniper attack frames never draw an in-flight
  arrow/bolt -- projectiles are separate sim-spawned assets (art doc sections
  5/10/11). The contact frame shows the RELEASE read instead: bow string
  snapped forward + empty; crossbow prongs empty + a 2-3 px muzzle flash.
  A nocked/loaded projectile HELD by the unit (windup arrow on the drawn
  string, idle loaded bolt tip) is part of the weapon read and stays.
- Weapon reach scales with the template canvas (the sniper template moved to
  48x64): the crossbow stock derives its span from ``canvas[0]``.
"""

from __future__ import annotations

INK = ("ink", 0)


def _shaft(buf, x0, y0, x1, y1, colors, part="prop_shaft"):
    """2 px mid-tone wood shaft (art rule 1: must survive 1x)."""
    buf.line(x0, y0, x1, y1, colors["wood"][0], colors["wood"][1],
             part=part, no_outline=True, width=2)


def _accent_diamond(buf, x, y, colors, part="prop_tip"):
    """4 px diamond accent (cluster-rule compliant, whitelist-cap aware)."""
    a = colors["accent"]
    for dx, dy in ((0, 0), (1, 0), (0, -1), (0, 1)):
        buf.set_px(x + dx, y + dy, a[0], a[1], part=part, no_outline=True)


# ---------------------------------------------------------------- weapons

def draw_spear(buf, pose, hand, colors, ground_y, canvas):
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 8, ground_y - 1, cx + 4, ground_y - 1, colors)
        _accent_diamond(buf, cx + 6, ground_y - 2, colors)
        return
    hx, hy = hand
    # diagonal carry: the tip clears the head and the butt drops below the
    # hip, so the 1-bit silhouette reads "spear-armed" at 1x (art rule 1);
    # contact levels out into the thrust.
    ends = {
        "idle": ((-4, 9), (4, -13)),
        "idle_b": ((-4, 8), (4, -12)),
        "walk": ((-4, 9), (4, -13)),
        "windup": ((-7, 2), (4, -2)),
        "windup2": ((-8, 2), (4, -2)),
        "contact": ((-5, 0), (5, 0)),
        "recover": ((-4, 4), (5, -7)),
        "settle": ((-3, 6), (4, -10)),
        "drop": ((-3, 8), (3, -4)),
    }
    (bdx, bdy), (tdx, tdy) = ends.get(pose, ends["idle"])
    tip = (hx + tdx, hy + tdy)
    _shaft(buf, hx + bdx, hy + bdy, tip[0], tip[1], colors)
    ddx = 1 if tdx >= 0 else -1
    ddy = 0 if tdy == 0 else (1 if tdy > 0 else -1)
    _accent_diamond(buf, tip[0] + ddx, tip[1] + ddy, colors)


def draw_sword(buf, pose, hand, colors, ground_y, canvas):
    m = colors["metal"]
    a = colors["accent"]
    if pose == "dropped":
        cx = canvas[0] // 2
        buf.fill_rect(cx - 2, ground_y - 1, cx + 6, ground_y, m[0], m[1], part="prop_blade")
        return
    hx, hy = hand
    # guard stays at 3 accent px: 3 + 2 eye px keeps the whitelist cap (6).
    if pose == "contact":  # horizontal slash, edge forward
        buf.fill_rect(hx + 2, hy - 1, hx + 9, hy + 1, m[0], m[1], part="prop_blade")
        buf.set_px(hx + 10, hy, m[0], m[1], part="prop_blade")
        buf.fill_rect(hx + 1, hy - 1, hx + 1, hy + 1, a[0], a[1], part="prop_guard", no_outline=True)
        return
    rise = {"windup": (-3, -7), "windup2": (-4, -8)}.get(pose, (0, -8))
    bx, by = hx + rise[0], hy + rise[1]
    buf.fill_rect(min(hx - 1, bx - 1), by, max(hx + 1, bx + 1), hy - 2, m[0], m[1], part="prop_blade")
    buf.fill_rect(hx - 1, hy - 1, hx + 1, hy - 1, a[0], a[1], part="prop_guard", no_outline=True)


def draw_sling(buf, pose, hand, colors, ground_y, canvas):
    w = colors["wood"]
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 2, ground_y, cx + 3, ground_y, colors, part="prop_strap")
        buf.fill_rect(cx + 3, ground_y - 1, cx + 4, ground_y, w[0],
                      min(w[1] + 1, 4), part="prop_pouch", no_outline=True)
        return
    hx, hy = hand
    strap_end = {
        "idle": (0, 4), "idle_b": (0, 4), "walk": (0, 4),
        "windup": (-4, -2), "windup2": (-5, -1),
        "contact": (5, 1), "recover": (2, 4), "settle": (1, 4),
        "drop": (0, 5), "drop2": (0, 5),
    }.get(pose, (0, 4))
    ex, ey = hx + strap_end[0], hy + strap_end[1]
    _shaft(buf, hx, hy, ex, ey, colors, part="prop_strap")
    buf.fill_rect(ex - 1, ey, ex, ey + 1, w[0], min(w[1] + 1, 4),
                  part="prop_pouch", no_outline=True)


def draw_bow(buf, pose, hand, colors, ground_y, canvas):
    """2 px-wide arc held forward; the string draws back on windup carrying
    the nocked arrow. The CONTACT frame is the release read: string snapped
    FORWARD and empty, arrow GONE (round-3 template rule: projectiles are
    sim-spawned, never baked into attack frames)."""
    w = colors["wood"]
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 7, ground_y - 1, cx - 1, ground_y - 2, colors, part="prop_bow")
        _shaft(buf, cx - 1, ground_y - 2, cx + 5, ground_y - 1, colors, part="prop_bow")
        return
    hx, hy = hand
    top = (hx - 2, hy - 8)
    bot = (hx - 2, hy + 8)
    belly = hx + 1
    buf.line(top[0], top[1], belly, hy - 4, w[0], w[1],
             part="prop_bow", no_outline=True, width=2)
    buf.line(belly, hy - 4, belly, hy + 4, w[0], w[1],
             part="prop_bow", no_outline=True, width=2)
    buf.line(belly, hy + 4, bot[0], bot[1], w[0], w[1],
             part="prop_bow", no_outline=True, width=2)
    if pose in ("windup", "windup2"):
        sx = hx - (5 if pose == "windup" else 6)
        buf.line(top[0], top[1], sx, hy, *INK, part="prop_string", no_outline=True)
        buf.line(sx, hy, bot[0], bot[1], *INK, part="prop_string", no_outline=True)
        _shaft(buf, sx + 1, hy, belly + 3, hy, colors, part="prop_arrow")
        _accent_diamond(buf, belly + 4, hy, colors)
    elif pose == "contact":
        # full release: string snapped forward of its rest line, empty -- the
        # arrow is a sim-spawned projectile and is never baked into this frame
        buf.line(top[0], top[1], hx, hy, *INK, part="prop_string", no_outline=True)
        buf.line(hx, hy, bot[0], bot[1], *INK, part="prop_string", no_outline=True)
    else:
        buf.line(top[0], top[1], bot[0], bot[1], *INK, part="prop_string", no_outline=True)


def draw_crossbow(buf, pose, hand, colors, ground_y, canvas):
    """Long rifle-like crossbow held LEVEL: the stock spans past the body on
    both sides so the silhouette reads 'longer weapon than the archer's bow'
    at 1x. Reach scales with the canvas (48x64 sniper template). The contact
    frame shows EMPTY prongs + a 3 px muzzle flash -- never an in-flight bolt
    (round-3 template rule: projectiles are sim-spawned)."""
    m = colors["metal"]
    a = colors["accent"]
    # SHOULDERED, not chest-spanning: butt sits just behind the grip, the stock
    # reaches FORWARD to a long muzzle -> reads as a leveled aim, not a T-bar
    # across the body (the quarry_slinger T-pose fix). Reach scales with canvas.
    back = canvas[0] * 3 // 32    # stock reach behind the hand (butt at shoulder)
    front = canvas[0] * 10 // 32  # stock reach forward (long aiming muzzle)
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - back + 5, ground_y - 1, cx + front - 1, ground_y - 1,
               colors, part="prop_stock")
        return
    hx, hy = hand
    # contact recoils BACK (-2): a heavy crossbow kicks as the bolt leaves.
    dx = {"windup": -1, "windup2": -1, "contact": -2, "recover": -1}.get(pose, 0)
    x0, x1 = hx - back + dx, hx + front + dx
    buf.fill_rect(x0, hy, x1, hy + 1, colors["wood"][0], colors["wood"][1],
                  part="prop_stock", no_outline=True)
    # prong arms: short vertical metal bar at the muzzle (taller reads hammer)
    buf.fill_rect(x1 - 1, hy - 2, x1, hy + 2, m[0], m[1], part="prop_prongs",
                  no_outline=True)
    if pose == "contact":
        # muzzle flash only, 3 px (allowed at contact); the bolt itself is a
        # sim-spawned projectile asset and is never baked into the frame
        buf.set_px(x1 + 1, hy, a[0], a[1], part="prop_flash", no_outline=True)
        buf.set_px(x1 + 1, hy + 1, a[0], a[1], part="prop_flash", no_outline=True)
        buf.set_px(x1 + 2, hy, a[0], a[1], part="prop_flash", no_outline=True)
    else:
        # loaded bolt tip showing past the prongs (held, not in flight)
        buf.set_px(x1 + 1, hy, a[0], a[1], part="prop_bolt_tip", no_outline=True)
        buf.set_px(x1 + 1, hy + 1, a[0], a[1], part="prop_bolt_tip", no_outline=True)


def draw_staff(buf, pose, hand, colors, ground_y, canvas):
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 6, ground_y - 1, cx + 4, ground_y - 1, colors)
        _accent_diamond(buf, cx + 6, ground_y - 2, colors)
        return
    hx, hy = hand
    tilt = {
        "windup": (-2, -10), "windup2": (-3, -10), "contact": (3, -9),
    }.get(pose, (0, -11))
    tx, ty = hx + tilt[0], hy + tilt[1]
    _shaft(buf, hx, hy + 6, tx, ty, colors)
    _accent_diamond(buf, tx, ty - 2, colors, part="prop_gem")


def draw_quarterstaff(buf, pose, hand, colors, ground_y, canvas):
    """Horizontal 2 px staff spanning both sides of the grip -- the monk read.
    2 px electric accent tips at each end (4 px + 2 eye px = whitelist cap 6).
    The contact thrust clamps against the canvas edge so the tip survives."""
    a = colors["accent"]
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 7, ground_y - 1, cx + 6, ground_y - 1, colors)
        for x in (cx - 8, cx + 7):
            buf.set_px(x, ground_y - 1, a[0], a[1], part="prop_tip", no_outline=True)
            buf.set_px(x, ground_y, a[0], a[1], part="prop_tip", no_outline=True)
        return
    hx, hy = hand
    dx = {"windup": -3, "windup2": -4, "contact": 4, "recover": 2, "settle": 1,
          "drop": -1, "drop2": -1}.get(pose, 0)
    x0, x1 = hx - 9 + dx, hx + 6 + dx
    shift = max(0, x1 - (canvas[0] - 2))
    x0, x1 = x0 - shift, x1 - shift
    _shaft(buf, x0, hy, x1, hy, colors)
    for x in (x0 - 1, x1 + 1):
        buf.set_px(x, hy, a[0], a[1], part="prop_tip", no_outline=True)
        buf.set_px(x, hy + 1, a[0], a[1], part="prop_tip", no_outline=True)


_WEAPONS = {
    "spear": draw_spear,
    "sword": draw_sword,
    "sling": draw_sling,
    "bow": draw_bow,
    "crossbow": draw_crossbow,
    "staff": draw_staff,
    "quarterstaff": draw_quarterstaff,
}


def draw_weapon(buf, pal, name, pose, hand, colors, ground_y, canvas):
    try:
        fn = _WEAPONS[name]
    except KeyError:
        raise KeyError(f"unknown weapon prop '{name}'; available: {sorted(_WEAPONS)}") from None
    fn(buf, pose, hand, colors, ground_y, canvas)


# ---------------------------------------------------------------- off-hand

def draw_offhand(buf, pal, name, anchor, colors):
    """Round shield carried proud of the torso front-low edge so it breaks
    the silhouette (the far hand brought across the body). ``small_shield``
    is the skirmisher buckler; ``shield`` is the LARGE walking-wall plate
    (chest-to-knee, the tank class identity)."""
    if name not in ("shield", "small_shield"):
        raise KeyError(f"unknown off-hand prop '{name}'")
    ax, ay = anchor
    w = colors["wood"]
    m = colors["metal"]
    face = min(w[1] + 1, pal.ramp_len(w[0]) - 1)
    metal_lit = min(m[1] + 1, pal.ramp_len(m[0]) - 1)
    if name == "shield":
        buf.fill_round_rect(ax - 3, ay - 6, ax + 4, ay + 4, w[0], face, part="prop_shield")
        buf.fill_rect(ax, ay - 2, ax + 1, ay - 1, m[0], metal_lit, part="prop_boss")
        return
    buf.fill_round_rect(ax - 2, ay - 3, ax + 3, ay + 3, w[0], face, part="prop_shield")
    buf.fill_rect(ax, ay - 1, ax + 1, ay, m[0], metal_lit, part="prop_boss")


# ---------------------------------------------------------------- headgear

def draw_headgear(buf, pal, name, head_rect, colors):
    hx0, hy0, hx1, hy1 = head_rect
    m = colors["metal"]
    a = colors["accent"]
    if name == "helmet":
        buf.fill_rect(hx0 + 1, hy0, hx1 - 1, hy0 + 2, m[0], m[1], part="helmet")
        buf.fill_rect(hx1, hy0 + 2, hx1, hy0 + 2, m[0], m[1], part="helmet")
        return
    if name == "crest":
        mid = (hx0 + hx1) // 2
        for i, x in enumerate(range(mid - 2, mid + 2)):
            buf.set_px(x, hy0 - 1 + (i % 2) * 0, a[0], a[1], part="crest", no_outline=True)
        return
    raise KeyError(f"unknown headgear prop '{name}'")
