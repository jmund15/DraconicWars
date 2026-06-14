"""test_forms.py -- self-test for the element attack-form generator.

Plain-assert script (no pytest): exit 0 = pass, 1 = fail. Run:
    python art_pipeline/test_forms.py

Pins the FormTemplate contract + generate_form output: every shape conforms to
the canvas / frames() / draw_frame() contract, and each canonical (element, shape)
bakes a correctly-shaped sheet + manifest using only palette ramps. Forms are the
second template family (parallel to skeletons.BodyPlanTemplate) -- this is the
"register + conform before the view consumes it" guarantee for forms.
"""

from __future__ import annotations

import sys
import tempfile
from pathlib import Path

import forms
from palette import get_palette


def check(label, cond):
    mark = "ok" if cond else "XX"
    print(f"  [{mark}] {label}")
    if not cond:
        check.failed += 1


check.failed = 0


def test_template_contract():
    for shape in forms.FORM_SHAPES:
        t = forms.FormTemplate(shape)
        check(f"{shape}: canvas is an (int,int) pair",
              isinstance(t.canvas, tuple) and len(t.canvas) == 2
              and all(isinstance(v, int) for v in t.canvas))
        check(f"{shape}: frames() == FRAME_KINDS", t.frames() == list(forms.FRAME_KINDS))
        check(f"{shape}: draw_frame is callable", callable(getattr(t, "draw_frame", None)))


def test_bad_shape_raises():
    raised = False
    try:
        forms.FormTemplate("not_a_shape")
    except KeyError:
        raised = True
    check("unknown shape raises KeyError", raised)


def test_generate_canonical():
    pal = get_palette()
    with tempfile.TemporaryDirectory() as tmp:
        for element, shape in forms.CANONICAL:
            res = forms.generate_form(element, shape, outdir=tmp, pal=pal)
            m = res["data"]
            check(f"{element}_{shape}: sheet exists", Path(res["sheet"]).exists())
            check(f"{element}_{shape}: manifest kind=form", m["kind"] == "form")
            check(f"{element}_{shape}: frames == spawn/travel/impact",
                  m["frames"] == list(forms.FRAME_KINDS))
            check(f"{element}_{shape}: loop_frame is travel",
                  m["frames"][m["loop_frame"]] == "travel")
            check(f"{element}_{shape}: palette-only (lint clean)",
                  forms.lint_form(res, pal) == [])


def main():
    print("test_forms.py")
    for fn in (test_template_contract, test_bad_shape_raises, test_generate_canonical):
        print(f"{fn.__name__}:")
        fn()
    print(f"\n{'PASS' if not check.failed else f'FAIL ({check.failed})'}")
    return 1 if check.failed else 0


if __name__ == "__main__":
    sys.exit(main())
