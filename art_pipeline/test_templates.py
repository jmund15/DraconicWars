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


def main():
    print("test_templates.py")
    for fn in (test_make_template_resolves_all_typeclasses, test_contract_conformance,
               test_internal_volume, test_eye_shape, test_squash_breathe_anchors_feet,
               test_secondary_lag):
        print(f"{fn.__name__}:")
        fn()
    print(f"\n{'PASS' if not check.failed else f'FAIL ({check.failed})'}")
    return 1 if check.failed else 0


if __name__ == "__main__":
    sys.exit(main())
