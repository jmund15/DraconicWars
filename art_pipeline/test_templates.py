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

from skeletons import BodyPlanTemplate, make_template


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


def main():
    print("test_templates.py")
    for fn in (test_make_template_resolves_all_typeclasses, test_contract_conformance):
        print(f"{fn.__name__}:")
        fn()
    print(f"\n{'PASS' if not check.failed else f'FAIL ({check.failed})'}")
    return 1 if check.failed else 0


if __name__ == "__main__":
    sys.exit(main())
