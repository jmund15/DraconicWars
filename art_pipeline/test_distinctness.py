"""test_distinctness.py -- self-test for the silhouette-distinctness lint math.

Plain-assert script (no pytest dependency): exit 0 = pass, 1 = fail. Run:
    python art_pipeline/test_distinctness.py

This pins the PURE math of the distinctness lint with SYNTHETIC masks --
especially the >=3:1 dragon-to-infantry area gap, which the live 13-unit
expansion roster CANNOT exercise (it has no dragon-tier unit). The actual-roster
RED->GREEN (pairwise body IoU on the real rigs) lives at the C# gate
(RosterArtContractTest) reading roster_distinctness.json; this script proves the
math underneath it is correct, including the axis the roster can't reach
(pin coverage axes separately).
"""

from __future__ import annotations

import sys

from PIL import Image

from lint import (align_bottom_center, iou, roster_distinctness,
                  silhouette_mask)


def _rect_mask(x0, y0, x1, y1):
    return {(x, y) for x in range(x0, x1) for y in range(y0, y1)}


def _unit(name, typeclass, mask, head_w, head_h):
    xs = [p[0] for p in mask]
    ys = [p[1] for p in mask]
    body = {
        "width": max(xs) - min(xs) + 1,
        "height": max(ys) - min(ys) + 1,
        "head_w": head_w,
        "head_h": head_h,
    }
    return {"name": name, "typeclass": typeclass, "mask": mask, "body_size": body}


def check(label, cond):
    mark = "ok" if cond else "XX"
    print(f"  [{mark}] {label}")
    if not cond:
        check.failed += 1


check.failed = 0


def test_iou():
    a = _rect_mask(0, 0, 10, 10)        # 100 px
    check("iou(identical) == 1.0", iou(a, a) == 1.0)
    disjoint = _rect_mask(100, 100, 110, 110)
    check("iou(disjoint) == 0.0", iou(a, disjoint) == 0.0)
    half = _rect_mask(5, 0, 15, 10)     # overlaps a in x[5,10) -> 50 px shared
    # intersection 50, union 150 -> 1/3
    check("iou(half-overlap) ~= 1/3", abs(iou(a, half) - 1 / 3) < 1e-9)
    check("iou(empty,empty) == 0.0 (no div-by-zero)", iou(set(), set()) == 0.0)


def test_align_bottom_center():
    # two identical shapes at different canvas offsets must align to identical
    # foot-centered masks (IoU 1) -- the lint compares silhouettes, not placement
    shape = _rect_mask(0, 0, 6, 12)
    shifted = {(x + 20, y + 7) for (x, y) in shape}
    check("align_bottom_center makes translated copies identical",
          align_bottom_center(shape) == align_bottom_center(shifted))


def test_distinctness_iou_pairwise():
    # two byte-identical bodies -> IoU 1.0 >= IOU_MAX -> distinctness FAILS
    body = _rect_mask(0, 0, 14, 24)
    same = [_unit("a", "melee_biped", body, 9, 9),
            _unit("b", "melee_biped", set(body), 9, 9)]
    rep = roster_distinctness(same, iou_max=0.70)
    check("identical bodies -> silhouette_distinctness FAILS",
          rep["checks"]["silhouette_distinctness"]["passed"] is False)
    check("identical bodies -> report passed False", rep["passed"] is False)
    check("offending pair reported",
          ("a", "b") in {tuple(p["pair"]) for p in
                         rep["checks"]["silhouette_distinctness"]["offenders"]})

    # genuinely different shapes (tall-thin vs short-wide) -> low IoU -> PASS
    tall = _unit("t", "melee_biped", _rect_mask(0, 0, 8, 26), 8, 8)
    wide = _unit("w", "melee_biped", _rect_mask(0, 0, 18, 12), 6, 5)
    rep2 = roster_distinctness([tall, wide], iou_max=0.70)
    check("distinct bodies -> silhouette_distinctness PASSES",
          rep2["checks"]["silhouette_distinctness"]["passed"] is True)


def test_scale_gap():
    infantry = _unit("grunt", "melee_biped", _rect_mask(0, 0, 14, 24), 9, 9)  # 336
    # dragon with >=3x area -> PASS
    big = _unit("wyrm", "dragon", _rect_mask(0, 0, 40, 60), 10, 10)           # 2400 (>3x)
    rep = roster_distinctness([infantry, big], iou_max=0.70)
    check("dragon >=3x infantry -> scale_gap PASSES",
          rep["checks"]["scale_gap"]["passed"] is True)
    # dragon under 3x -> FAIL
    small = _unit("runt", "dragon", _rect_mask(0, 0, 20, 30), 10, 10)         # 600 (<3x of 336)
    rep2 = roster_distinctness([infantry, small], iou_max=0.70)
    check("dragon <3x infantry -> scale_gap FAILS",
          rep2["checks"]["scale_gap"]["passed"] is False)
    # no dragon present -> scale_gap SKIPPED (neutral, does not fail the gate)
    rep3 = roster_distinctness([infantry,
                                _unit("grunt2", "melee_biped",
                                      _rect_mask(40, 0, 60, 26), 7, 7)],
                               iou_max=0.70)
    sg = rep3["checks"]["scale_gap"]
    check("no dragon -> scale_gap skipped", sg.get("skipped") is not None)
    check("skipped scale_gap does NOT hold the report RED (neutral)",
          sg["passed"] is True)


def test_head_band():
    bands = {"melee_biped": (0.20, 0.45)}
    body = _rect_mask(0, 0, 14, 24)  # height 24
    in_band = _unit("ok", "melee_biped", body, 9, 8)    # 8/24 = 0.33 in band
    out_band = _unit("floaty", "melee_biped", set(body), 4, 2)  # 2/24 = 0.083 < 0.20
    rep = roster_distinctness([in_band], iou_max=0.70, head_bands=bands)
    check("in-band head -> head_to_body PASSES",
          rep["checks"]["head_to_body"]["passed"] is True)
    rep2 = roster_distinctness([out_band], iou_max=0.70, head_bands=bands)
    check("tiny floating head -> head_to_body FAILS",
          rep2["checks"]["head_to_body"]["passed"] is False)
    # siege has no head -> skipped by typed typeclass, not measured mass
    siege = _unit("ram", "siege_machine", _rect_mask(0, 0, 26, 20), 0, 0)
    rep3 = roster_distinctness([siege], iou_max=0.70, head_bands=bands)
    check("siege typeclass -> head_to_body skips it (no spurious fail)",
          rep3["checks"]["head_to_body"]["passed"] is True)


def test_cross_form_distinctness():
    # different creature FORMS that share a silhouette must FAIL (the whole point
    # of the Part: an 'ogre' that's just a recolored humanoid is the defect).
    humanoid = _unit("h", "melee_biped", _rect_mask(0, 0, 14, 24), 9, 9)
    fake_ogre = _unit("o", "ogre", set(_rect_mask(0, 0, 14, 24)), 9, 9)  # same body
    rep = roster_distinctness([humanoid, fake_ogre], iou_max=0.70,
                              cross_form_iou_max=0.55)
    check("same-body different-form pair -> cross_form FAILS",
          rep["checks"]["cross_form_distinctness"]["passed"] is False)
    check("cross_form offender names the cross-form pair",
          any(set(o["pair"]) == {"h", "o"} for o in
              rep["checks"]["cross_form_distinctness"]["offenders"]))

    # genuinely different forms (humanoid vs wide low blob) -> low IoU -> PASS
    blob = _unit("s", "slime", _rect_mask(0, 12, 28, 24), 0, 0)  # wide, low, no overlap up top
    rep2 = roster_distinctness([humanoid, blob], iou_max=0.70,
                               cross_form_iou_max=0.55)
    check("distinct forms (humanoid vs slime) -> cross_form PASSES",
          rep2["checks"]["cross_form_distinctness"]["passed"] is True)

    # a single form present -> no cross-form pairs -> SKIPPED (neutral, not a crash)
    a = _unit("a", "melee_biped", _rect_mask(0, 0, 8, 26), 8, 8)
    b = _unit("b", "melee_biped", _rect_mask(0, 0, 18, 12), 6, 5)
    rep3 = roster_distinctness([a, b], iou_max=0.70, cross_form_iou_max=0.55)
    cf = rep3["checks"]["cross_form_distinctness"]
    check("single form -> cross_form skipped", cf.get("skipped") is not None)
    check("skipped cross_form is neutral (passed True)", cf["passed"] is True)


def test_silhouette_mask():
    img = Image.new("RGBA", (4, 4), (0, 0, 0, 0))
    img.putpixel((1, 2), (0, 0, 0, 255))
    img.putpixel((3, 3), (10, 20, 30, 255))
    img.putpixel((0, 0), (0, 0, 0, 128))  # semi-transparent -> NOT opaque
    mask = silhouette_mask(img)
    check("silhouette_mask collects only alpha==255 pixels",
          mask == {(1, 2), (3, 3)})


def main():
    print("test_distinctness.py")
    for fn in (test_iou, test_align_bottom_center, test_distinctness_iou_pairwise,
               test_scale_gap, test_head_band, test_cross_form_distinctness,
               test_silhouette_mask):
        print(f"{fn.__name__}:")
        fn()
    print(f"\n{'PASS' if not check.failed else f'FAIL ({check.failed})'}")
    return 1 if check.failed else 0


if __name__ == "__main__":
    sys.exit(main())
