"""props.py -- prop layer primitives drawn at a template's prop anchor.

Weapons (spear, sword, sling, bow, staff), off-hand (small shield), and
headgear (helmet, crest). Element-accent coloring comes from the unit's
resolved ``colors`` dict (role -> (ramp, index)), which generate_unit.py builds
through the Palette API -- no raw colors here.

Conventions:
- 1 px shafts/straps are drawn ``no_outline`` in a darkest-step wood color
  (the sel-out pass cannot both outline and fill a 1 px shape); the lint
  whitelist covers them.
- Bright element accents (spearhead, staff gem, horns) are small deliberate
  clusters, also ``no_outline`` + whitelisted.
- Poses mirror the skeleton arm pose keys: idle / idle_b / walk / windup /
  windup2 / contact / recover / settle / drop / dropped.
"""

from __future__ import annotations


def _shaft(buf, x0, y0, x1, y1, colors, part="prop_shaft"):
    buf.line(x0, y0, x1, y1, colors["wood"][0], colors["wood"][1],
             part=part, no_outline=True)


def _accent_diamond(buf, x, y, colors, part="prop_tip"):
    """4 px diamond accent (cluster-rule compliant)."""
    a = colors["accent"]
    for dx, dy in ((0, 0), (1, 0), (0, -1), (0, 1)):
        buf.set_px(x + dx, y + dy, a[0], a[1], part=part, no_outline=True)


# ---------------------------------------------------------------- weapons

def draw_spear(buf, pose, hand, colors, ground_y, canvas):
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 6, ground_y - 1, cx + 5, ground_y - 1, colors)
        _accent_diamond(buf, cx + 7, ground_y - 1, colors)
        return
    hx, hy = hand
    # steep idle/walk carry: the tip clears the head so the silhouette reads
    # "spear-armed" at 1x (art rule 1); contact levels out into the thrust.
    ends = {
        "idle": ((-1, 5), (3, -10)),
        "idle_b": ((-1, 5), (3, -9)),
        "walk": ((-1, 5), (3, -10)),
        "windup": ((-5, 1), (3, -1)),
        "windup2": ((-6, 1), (3, -1)),
        "contact": ((-3, 0), (6, 0)),
        "recover": ((-3, 2), (5, -4)),
        "settle": ((-2, 4), (4, -7)),
        "drop": ((-2, 6), (3, -2)),
    }
    (tdx, tdy), (pdx, pdy) = ends.get(pose, ends["idle"])
    tip = (hx + pdx, hy + pdy)
    _shaft(buf, hx + tdx, hy + tdy, tip[0], tip[1], colors)
    dx = 1 if pdx >= 0 else -1
    dy = 0 if pdy == 0 else (1 if pdy > 0 else -1)
    _accent_diamond(buf, tip[0] + dx, tip[1] + dy, colors)


def draw_sword(buf, pose, hand, colors, ground_y, canvas):
    m = colors["metal"]
    a = colors["accent"]
    if pose == "dropped":
        cx = canvas[0] // 2
        buf.fill_rect(cx - 2, ground_y - 1, cx + 6, ground_y, m[0], m[1], part="prop_blade")
        return
    hx, hy = hand
    if pose == "contact":  # horizontal slash, edge forward
        buf.fill_rect(hx + 2, hy - 1, hx + 9, hy + 1, m[0], m[1], part="prop_blade")
        buf.set_px(hx + 10, hy, m[0], m[1], part="prop_blade")
        buf.fill_rect(hx + 1, hy - 2, hx + 1, hy + 2, a[0], a[1], part="prop_guard", no_outline=True)
        return
    rise = {"windup": (-3, -7), "windup2": (-4, -8)}.get(pose, (0, -8))
    bx, by = hx + rise[0], hy + rise[1]
    buf.fill_rect(min(hx - 1, bx - 1), by, max(hx + 1, bx + 1), hy - 2, m[0], m[1], part="prop_blade")
    buf.fill_rect(hx - 2, hy - 1, hx + 2, hy - 1, a[0], a[1], part="prop_guard", no_outline=True)


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
    """Vertical bow held at the grip hand; the string bends on windup."""
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 6, ground_y - 1, cx, ground_y - 2, colors, part="prop_bow")
        _shaft(buf, cx, ground_y - 2, cx + 6, ground_y - 1, colors, part="prop_bow")
        return
    hx, hy = hand
    top = (hx + 1, hy - 7)
    bot = (hx + 1, hy + 7)
    _shaft(buf, top[0], top[1], hx + 3, hy, colors, part="prop_bow")
    _shaft(buf, hx + 3, hy, bot[0], bot[1], colors, part="prop_bow")
    string_x = {"windup": hx - 3, "windup2": hx - 4}.get(pose)
    e = colors["eye"]  # ink -- dark 1 px string
    if string_x is not None:
        buf.line(top[0], top[1], string_x, hy, e[0], e[1], part="prop_string", no_outline=True)
        buf.line(string_x, hy, bot[0], bot[1], e[0], e[1], part="prop_string", no_outline=True)
        _shaft(buf, string_x, hy, hx + 4, hy, colors, part="prop_arrow")
        _accent_diamond(buf, hx + 5, hy, colors)
    else:
        buf.line(top[0], top[1], bot[0], bot[1], e[0], e[1], part="prop_string", no_outline=True)


def draw_staff(buf, pose, hand, colors, ground_y, canvas):
    if pose == "dropped":
        cx = canvas[0] // 2
        _shaft(buf, cx - 5, ground_y - 1, cx + 5, ground_y - 1, colors)
        _accent_diamond(buf, cx + 6, ground_y - 2, colors)
        return
    hx, hy = hand
    tilt = {
        "windup": (-2, -9), "windup2": (-3, -9), "contact": (3, -8),
    }.get(pose, (0, -10))
    tx, ty = hx + tilt[0], hy + tilt[1]
    _shaft(buf, hx, hy + 2, tx, ty, colors)
    a = colors["accent"]
    if pose == "contact":  # gem flares on the hit frame
        buf.fill_rect(tx - 1, ty - 2, tx + 1, ty, a[0], a[1], part="prop_gem", no_outline=True)
    else:
        buf.fill_rect(tx - 1, ty - 1, tx, ty, a[0], a[1], part="prop_gem", no_outline=True)


_WEAPONS = {
    "spear": draw_spear,
    "sword": draw_sword,
    "sling": draw_sling,
    "bow": draw_bow,
    "staff": draw_staff,
}


def draw_weapon(buf, pal, name, pose, hand, colors, ground_y, canvas):
    try:
        fn = _WEAPONS[name]
    except KeyError:
        raise KeyError(f"unknown weapon prop '{name}'; available: {sorted(_WEAPONS)}") from None
    fn(buf, pose, hand, colors, ground_y, canvas)


# ---------------------------------------------------------------- off-hand

def draw_offhand(buf, pal, name, anchor, colors):
    """Small round shield carried proud of the torso front-low edge so it
    breaks the silhouette (the far hand brought across the body)."""
    if name not in ("shield", "small_shield"):
        raise KeyError(f"unknown off-hand prop '{name}'")
    ax, ay = anchor
    w = colors["wood"]
    m = colors["metal"]
    face = min(w[1] + 2, pal.ramp_len(w[0]) - 1)
    buf.fill_round_rect(ax - 2, ay - 3, ax + 3, ay + 3, w[0], face, part="prop_shield")
    buf.fill_rect(ax, ay - 1, ax + 1, ay, m[0], min(m[1] + 1, pal.ramp_len(m[0]) - 1),
                  part="prop_boss")


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
