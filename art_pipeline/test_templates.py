"""test_templates.py -- self-test for the body-plan template family contract.

Plain-assert script (no pytest): exit 0 = pass, 1 = fail. Run:
    python art_pipeline/test_templates.py

Pins the BodyPlanTemplate contract: every registered typeclass resolves through
make_template to an object satisfying the 5-member contract generate_unit relies
on (canvas / ROLE_DEFAULTS / animations / poses / draw_pose). A new creature form
(ogre, slime, ...) MUST register + conform here before generate_unit can build it
-- this is the "determined upfront" guarantee, mechanically enforced. The
contract is structural (typing.Protocol); the incumbents conform by duck-typing
with no base-class change (byte-identity preserved).
"""

from __future__ import annotations

import sys

import skeletons
from skeletons import BodyPlanTemplate, make_template
from palette import get_palette


def check(label, cond):
    mark = "ok" if cond else "XX"
    print(f"  [{mark}] {label}")
    if not cond:
        check.failed += 1


check.failed = 0

# (typeclass, spec) -- one per registered form. spec carries the canvas the
# aerial/siege/slime templates read; biped specs ignore it (config-driven).
CASES = [
    ("melee_biped", {"typeclass": "melee_biped", "canvas": "32x32"}),
    ("ranged_biped", {"typeclass": "ranged_biped", "canvas": "32x32"}),
    ("sniper_biped", {"typeclass": "sniper_biped", "canvas": "48x64"}),
    ("support_robed", {"typeclass": "support_robed", "canvas": "32x32"}),
    ("aerial_flyer", {"typeclass": "aerial_flyer", "canvas": "32x32"}),
    ("siege_machine", {"typeclass": "siege_machine", "canvas": "40x32"}),
    ("ogre", {"typeclass": "ogre", "canvas": "32x32"}),
    ("construct", {"typeclass": "construct", "canvas": "48x48", "construct_style": "segmented"}),
    ("construct", {"typeclass": "construct", "canvas": "64x64", "construct_style": "crystalline"}),
    ("slime", {"typeclass": "slime", "canvas": "40x32"}),
]


def test_make_template_resolves_all_typeclasses():
    for tc, spec in CASES:
        ok = True
        try:
            ok = make_template(tc, spec) is not None
        except Exception as e:  # noqa: BLE001 -- self-test reports any failure
            ok = False
            print(f"      make_template({tc!r}) raised {e!r}")
        check(f"make_template({tc!r}) resolves", ok)


def test_contract_conformance():
    for tc, spec in CASES:
        try:
            t = make_template(tc, spec)
        except Exception:  # noqa: BLE001
            check(f"{tc}: conforms to BodyPlanTemplate", False)
            continue
        # structural duck-check (the actual contract generate_unit consumes;
        # version-robust across Protocol isinstance semantics).
        members = (hasattr(t, "canvas") and hasattr(t, "ROLE_DEFAULTS")
                   and callable(getattr(t, "animations", None))
                   and callable(getattr(t, "poses", None))
                   and callable(getattr(t, "draw_pose", None)))
        check(f"{tc}: has the 5 contract members", members)
        check(f"{tc}: isinstance BodyPlanTemplate (runtime_checkable)",
              isinstance(t, BodyPlanTemplate))
        try:
            w, h = t.canvas
            cv = isinstance(w, int) and isinstance(h, int)
        except Exception:  # noqa: BLE001
            cv = False
        check(f"{tc}: canvas is an (int, int) pair", cv)
        anims = t.animations()
        check(f"{tc}: animations() non-empty", len(anims) > 0)
        attack = [a for a in anims if a.name == "attack"]
        check(f"{tc}: has exactly one attack animation", len(attack) == 1)
        if attack:
            poses = t.poses("attack", max(1, attack[0].frames // 2))
            check(f"{tc}: poses('attack', ...) non-empty", len(poses) > 0)


def test_internal_volume():
    """apply_internal_volume darkens the lower-ventral interior of a >=48px body
    mass one ramp step (a core shadow), flags those cells volume=True for the
    exempt harvest, leaves the upper body at the base fill, and no-ops < 48px."""
    pal = get_palette()
    buf = skeletons.PixelBuffer(20, 60)
    for y in range(10, 50):                  # torso rows 10..49 (h=40)
        for x in range(5, 15):
            buf.set_px(x, y, "fire", 2, part="body")
    skeletons.apply_internal_volume(buf, pal)
    lo = buf.cells[(10, 45)]
    check("internal volume darkens the ventral interior one ramp step", lo.idx == 1)
    check("volume cells are flagged for the exempt harvest", lo.volume is True)
    hi = buf.cells[(10, 15)]
    check("upper body keeps the base fill (no volume flag)",
          hi.idx == 2 and hi.volume is False)

    small = skeletons.PixelBuffer(20, 32)    # < 48 -> no volume
    for y in range(8, 28):
        for x in range(5, 15):
            small.set_px(x, y, "fire", 2, part="body")
    skeletons.apply_internal_volume(small, pal)
    check("internal volume no-ops below 48px", small.cells[(10, 25)].volume is False)


def test_eye_shape():
    """_flyer_eye 'slit' = a 2px VERTICAL pair (reptilian identity accent);
    'round' = the eye_px block. Default 'round' keeps the byte-identical shape."""
    pal = get_palette()
    colors = {"eye": ("mauve_grey", 4)}
    slit = skeletons.PixelBuffer(10, 10)
    skeletons._flyer_eye(slit, 5, 5, colors, 2, "slit")
    check("slit eye is a 2px vertical pair",
          (5, 5) in slit.cells and (5, 6) in slit.cells and (4, 5) not in slit.cells)
    check("slit eye stays within the 6px whitelist cap", len(slit.cells) == 2)
    rnd = skeletons.PixelBuffer(10, 10)
    skeletons._flyer_eye(rnd, 5, 5, colors, 2, "round")
    check("round eye is the eye_px block (horizontal pair, byte-identical default)",
          (5, 5) in rnd.cells and (4, 5) in rnd.cells and (5, 6) not in rnd.cells)


def test_squash_breathe_anchors_feet():
    """The idle 'squash' breathe compresses the torso TOP downward while the
    feet stay on the same ground row (the grounded-frame lint invariant)."""
    import generate_unit
    pal = get_palette()
    tmpl = skeletons.make_template("melee_biped", {"typeclass": "melee_biped",
                                                   "canvas": "32x32"})
    colors = generate_unit.resolve_colors(tmpl, "fire", None, pal)
    unit = {"colors": colors, "props": [], "eye_offset": (0, 0)}

    def render(pose):
        buf = skeletons.PixelBuffer(*tmpl.canvas)
        tmpl.draw_pose(buf, pose, unit, pal)
        return buf

    rest, breathe = (render(p) for p in tmpl.poses("idle", 1))
    bottom = lambda b: max(y for (_, y) in b.cells)
    torso_top = lambda b: min(y for (x, y), c in b.cells.items() if c.part == "torso")
    check("breathe keeps feet on the same ground row (squash anchors feet)",
          bottom(rest) == bottom(breathe))
    check("breathe compresses the torso top downward (squash applied)",
          torso_top(breathe) > torso_top(rest))


def test_secondary_lag():
    """apply_secondary_lag injects a frame-lagged 'wing' drive: WRAP for loop
    clips (frame 0 reads the last), CLAMP at 0 for one-shots; trails by `lag`."""
    base = [{"wing": 0}, {"wing": 3}, {"wing": 6}, {"wing": 9}]
    loop_p = [dict(p) for p in base]
    skeletons.apply_secondary_lag(loop_p, loop=True, lag=1, keys=("wing",))
    check("loop lag wraps: frame 0 reads the last frame's wing",
          loop_p[0]["wing_lag"] == 9)
    check("loop lag trails by 1: frame 2 reads frame 1's wing",
          loop_p[2]["wing_lag"] == 3)
    clamp_p = [dict(p) for p in base]
    skeletons.apply_secondary_lag(clamp_p, loop=False, lag=1, keys=("wing",))
    check("one-shot lag clamps: frame 0 reads itself", clamp_p[0]["wing_lag"] == 0)
    check("one-shot lag trails elsewhere: frame 2 reads frame 1", clamp_p[2]["wing_lag"] == 3)


def test_dragon_horns_backswept():
    """Crowned dragon horns must RAKE BACK (apex.dx < 0 AND |dx| >= |dy|) so they
    read as heavy dragon horns, not upward-splaying deer antlers. R = the boss
    scale (3.2). The antler tells this rejects: an apex pointing up (|dy| > |dx|)
    or forward (dx >= 0)."""
    R = lambda v: round(v * 3.2)
    hx, hy = 60, 30
    for p0, p1, p2, part in skeletons._dragon_horns(hx, hy, R):
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        check(f"crowned horn '{part}' apex rakes back (dx<0)", dx < 0)
        check(f"crowned horn '{part}' backsweep dominates rise (|dx|>=|dy|)",
              abs(dx) >= abs(dy))


def _pyraxis_template(pal, seed=None):
    """Build the crowned fire-boss dragon template + render context (the pyraxis
    spec) for tests. Optional seed exercises the per-seed asymmetry path."""
    import generate_unit
    flyer = {"scale": 3.2, "wing_mult": 0.84, "tail_mult": 1.3, "head": "crowned",
             "crest": "ridge", "fire_tail": True, "eye_px": 4, "eye_shape": "slit",
             "boss": True, "body_dx": -10, "dragon": True}
    if seed is not None:
        flyer["seed"] = seed
    spec = {"typeclass": "aerial_flyer", "canvas": "96x96", "flyer": flyer}
    tmpl = skeletons.make_template("aerial_flyer", spec)
    colors = generate_unit.resolve_colors(tmpl, "fire", None, pal)
    unit = {"colors": colors, "props": [], "eye_offset": (0, 0)}
    return tmpl, colors, unit


def test_crowned_fire_accent():
    """The crowned fire dragon carries a deliberate palette-locked ember accent
    (the 'accent' role hex, no_outline so it blooms) -- present on a rendered
    frame, with eye+accent staying within the 6px whitelist cap."""
    pal = get_palette()
    tmpl, colors, unit = _pyraxis_template(pal)
    acc_hex = pal.hex_of(*colors["accent"])
    eye_hex = pal.hex_of(*colors["eye"])
    buf = skeletons.PixelBuffer(96, 96)
    tmpl.draw_pose(buf, tmpl.poses("idle", 1)[0], unit, pal)
    n_acc = sum(1 for c in buf.cells.values() if pal.hex_of(c.ramp, c.idx) == acc_hex)
    n_eye = sum(1 for c in buf.cells.values() if pal.hex_of(c.ramp, c.idx) == eye_hex)
    check("crowned dragon has >=1 ember accent px (identity accent present)", n_acc >= 1)
    check("eye+accent whitelist within the 6px cap", n_acc + n_eye <= 6)


def test_boss_frame_budget():
    """A maxed boss dragon spends 26-30 animation frames (Dragon Differentiation
    Fix); poses() must return exactly the AnimDef frame count per animation (the
    two are a double source of truth that generate_unit cross-checks), and the
    attack contact_frame stays in range across the bumped count."""
    from generate_unit import contact_frame_index
    pal = get_palette()
    tmpl, colors, unit = _pyraxis_template(pal)
    anims = tmpl.animations()
    total = sum(a.frames for a in anims)
    check(f"boss frame budget in [26,30] (got {total})", 26 <= total <= 30)
    attack = next(a for a in anims if a.name == "attack")
    contact = contact_frame_index(10, 14, attack.frames)
    check("attack contact_frame in [0, attack.frames)", 0 <= contact < attack.frames)
    for a in anims:
        n = len(tmpl.poses(a.name, contact))
        check(f"poses('{a.name}') count == AnimDef.frames ({a.frames})", n == a.frames)


def test_flyer_seed_asymmetry():
    """A nonzero FlyerConfig.seed breaks symmetry deterministically (reusing the
    shared _asym helper): distinct seeds render distinct geometry, while seed 0
    is the byte-stable no-asymmetry default (other flyers keep their bytes)."""
    pal = get_palette()
    def render(seed):
        tmpl, colors, unit = _pyraxis_template(pal, seed=seed)
        buf = skeletons.PixelBuffer(96, 96)
        tmpl.draw_pose(buf, tmpl.poses("idle", 1)[0], unit, pal)
        return {(xy, c.ramp, c.idx) for xy, c in buf.cells.items()}
    a0, a5, a3 = render(0), render(5), render(3)
    check("seed 5 differs from seed 0 (asymmetry applied)", a5 != a0)
    check("seed 3 differs from seed 5 (distinct seeds -> distinct geometry)", a3 != a5)


def main():
    print("test_templates.py")
    for fn in (test_make_template_resolves_all_typeclasses, test_contract_conformance,
               test_internal_volume, test_eye_shape, test_squash_breathe_anchors_feet,
               test_secondary_lag, test_dragon_horns_backswept, test_crowned_fire_accent,
               test_boss_frame_budget, test_flyer_seed_asymmetry):
        print(f"{fn.__name__}:")
        fn()
    print(f"\n{'PASS' if not check.failed else f'FAIL ({check.failed})'}")
    return 1 if check.failed else 0


if __name__ == "__main__":
    sys.exit(main())
