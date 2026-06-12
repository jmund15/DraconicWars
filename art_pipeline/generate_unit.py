"""generate_unit.py -- unit spec -> <name>_sheet.png + <name>.manifest.json.

A unit is a template INSTANCE: TypeClass skeleton + element ramp selection +
prop layer + spec parameters. No bespoke per-unit drawing.

Spec dict / JSON:
    {
      "name": "kobold_spearman",
      "typeclass": "melee_biped",          # see skeletons.TEMPLATE_NAMES
      "element": "fire",                    # palette.json elements
      "size_class": "small",                # informational; canvas comes from the template
      "props": ["spear", "small_shield"],
      "palette_overrides": {"cloth": ["leather", 1]},   # role -> ramp | [ramp, idx]
      "foreswing_ticks": 4,
      "backswing_ticks": 8
    }

Sheet layout: one animation per row (idle / walk-or-fly / attack / death),
frames left-to-right; transparent padding in short rows. The manifest records
frame size, per-animation row/frames/fps/loop, the attack contact frame, the
sim tick inputs, and the ramps used.

Timing ownership (art-direction.md section 10): the deterministic sim owns
frame timings. ``foreswing_ticks``/``backswing_ticks`` arrive as inputs and the
contact frame is placed so the wind-up share of the attack visually matches the
foreswing share of the swing; the Godot ``@tool`` importer validates
manifest-vs-stat-sheet agreement.

Determinism: no randomness anywhere -- identical spec => identical bytes.

CLI:
    python art_pipeline/generate_unit.py <spec.json> [--outdir DIR]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

import skeletons
from palette import Palette, get_palette

DEFAULT_OUTDIR = Path(__file__).resolve().parent / "output" / "units"
ATTACK_FRAMES = 4  # first-playable budget


def contact_frame_index(foreswing: int, backswing: int, frames: int = ATTACK_FRAMES) -> int:
    """Place the contact frame so anticipation share ~= foreswing share.

    Clamped to [1, frames-2]: at least one anticipation and one recovery frame
    (FP budget structure). The authoritative frame-to-tick rounding rule lives
    in the sim; the importer fails the import on drift.
    """
    total = max(foreswing + backswing, 1)
    return max(1, min(frames - 2, round(foreswing / total * (frames - 1))))


def resolve_colors(template, element: str, overrides: dict, pal: Palette) -> dict:
    """ROLE_DEFAULTS ('@primary'/'@secondary'/'@accent' placeholders) ->
    concrete (ramp, index) pairs, then apply spec palette_overrides."""
    e = pal.element(element)
    out: dict[str, tuple[str, int]] = {}
    for role, (ramp, idx) in template.ROLE_DEFAULTS.items():
        if ramp == "@primary":
            ramp = e["primary"]
        elif ramp == "@secondary":
            ramp = e["secondary"]
        if idx == "@accent":
            idx = e["accent_index"]
        out[role] = (ramp, pal.clamp_index(ramp, idx))
    for role, ov in (overrides or {}).items():
        if isinstance(ov, str):
            ramp, idx = ov, out.get(role, (ov, 2))[1]
        else:
            ramp, idx = ov[0], ov[1]
        pal.get(ramp, pal.clamp_index(ramp, idx))  # validate ramp exists
        out[role] = (ramp, pal.clamp_index(ramp, idx))
    # The ink ramp is reserved for sel-out outlines and eyes: a body part on
    # ink would get lit edges == #2e222f and read as a thick outline blob
    # (the shadow element's secondary is ink). Remap to the mauve_grey neutral.
    for role, (ramp, idx) in out.items():
        if ramp == "ink" and role != "eye":
            out[role] = ("mauve_grey", pal.clamp_index("mauve_grey", idx))
    return out


def _body_bbox_size(buf) -> tuple[int, int]:
    """(w, h) of the non-prop silhouette (parts prefixed ``prop_`` excluded)."""
    xs, ys = [], []
    for (x, y), cell in buf.cells.items():
        if not cell.part.startswith("prop_"):
            xs.append(x)
            ys.append(y)
    if not xs:
        return (0, 0)
    return (max(xs) - min(xs) + 1, max(ys) - min(ys) + 1)


def generate_unit(spec: dict, outdir: str | Path | None = None,
                  pal: Palette | None = None) -> dict:
    """Generate sheet + manifest for one unit spec. Returns paths + manifest."""
    pal = pal or get_palette()
    outdir = Path(outdir) if outdir else DEFAULT_OUTDIR
    outdir.mkdir(parents=True, exist_ok=True)

    name = spec["name"]
    template = skeletons.make_template(spec["typeclass"], spec)
    if spec.get("canvas") and skeletons.parse_canvas(spec["canvas"]) != tuple(template.canvas):
        raise ValueError(
            f"{name}: spec canvas {spec['canvas']} != template canvas {template.canvas}")
    colors = resolve_colors(template, spec["element"], spec.get("palette_overrides"), pal)
    fore = int(spec.get("foreswing_ticks", 4))
    back = int(spec.get("backswing_ticks", 8))
    attack_frames = next(a.frames for a in template.animations() if a.name == "attack")
    contact = contact_frame_index(fore, back, attack_frames)

    # whitelist = small deliberate details (eyes, element accents), capped by
    # lint at 6 px/frame. prop_colors = mid-tone 2 px prop bodies (shafts,
    # stocks, prongs) -- outline-coverage exempt but NOT capped.
    wl_roles = getattr(template, "WHITELIST_ROLES", ("accent", "eye"))
    protected = {pal.hex_of(*colors[r]) for r in wl_roles if r in colors}
    whitelist = sorted(protected)
    prop_colors = sorted({pal.hex_of(*colors[r]) for r in ("wood", "metal")
                          if r in colors})

    unit = {
        "colors": colors,
        "props": list(spec.get("props", [])),
        "eye_offset": tuple(spec.get("eye_offset", (0, 0))),
    }

    W, H = template.canvas
    anims = template.animations()
    airborne = isinstance(template, skeletons.AerialFlyerTemplate)

    rows: list[tuple[skeletons.AnimDef, list]] = []
    ramps_used: set[str] = set()
    body_sizes: dict[str, list[tuple[int, int]]] = {}
    for anim in anims:
        bufs = []
        for pose in template.poses(anim.name, contact):
            buf = skeletons.PixelBuffer(W, H)
            template.draw_pose(buf, pose, unit, pal)
            skeletons.finalize(buf, pal, protected)
            ramps_used |= buf.ramps_used()
            body_sizes.setdefault(anim.name, []).append(_body_bbox_size(buf))
            bufs.append(buf)
        if len(bufs) != anim.frames:
            raise RuntimeError(
                f"{name}/{anim.name}: template produced {len(bufs)} frames, "
                f"manifest declares {anim.frames}")
        rows.append((anim, bufs))

    # body mass (props excluded): max over the idle + move animations.
    move = "fly" if airborne else "walk"
    sizes = body_sizes.get("idle", []) + body_sizes.get(move, [])
    body_size = {"width": max(s[0] for s in sizes), "height": max(s[1] for s in sizes)}
    if airborne:
        body_size["wingspan"] = max(s[0] for s in body_sizes.get(move, sizes))

    max_frames = max(a.frames for a, _ in rows)
    sheet = Image.new("RGBA", (W * max_frames, H * len(rows)), (0, 0, 0, 0))
    for row, (anim, bufs) in enumerate(rows):
        for col, buf in enumerate(bufs):
            sheet.alpha_composite(skeletons.render(buf, pal), (col * W, row * H))

    sheet_path = outdir / f"{name}_sheet.png"
    sheet.save(sheet_path)

    manifest = {
        "name": name,
        "typeclass": spec["typeclass"],
        "element": spec["element"],
        "size_class": spec.get("size_class", "small"),
        "frame_w": W,
        "frame_h": H,
        "animations": [
            {
                "name": anim.name,
                "row": row,
                "frames": anim.frames,
                "fps": anim.fps,
                "loop": anim.loop,
                **({"contact_frame": contact} if anim.name == "attack" else {}),
            }
            for row, (anim, _) in enumerate(rows)
        ],
        "foreswing_ticks": fore,
        "backswing_ticks": back,
        "ramps_used": sorted(ramps_used),
        "pivot": "bottom_center",
        "ground_y": skeletons.ground_row(H),
        "facing": "right",
        "body_size": body_size,
        "lint": {
            "whitelist_colors": whitelist,
            "prop_colors": prop_colors,
            "airborne_animations": [a.name for a, _ in rows] if airborne else [],
        },
    }
    manifest_path = outdir / f"{name}.manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)

    return {"sheet": str(sheet_path), "manifest": str(manifest_path), "data": manifest}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Generate a unit sprite sheet from a spec JSON.")
    ap.add_argument("spec", help="path to a unit spec .json file")
    ap.add_argument("--outdir", default=None, help=f"output directory (default {DEFAULT_OUTDIR})")
    args = ap.parse_args(argv)
    with open(args.spec, "r", encoding="utf-8") as f:
        spec = json.load(f)
    result = generate_unit(spec, args.outdir)
    print(f"sheet:    {result['sheet']}")
    print(f"manifest: {result['manifest']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
