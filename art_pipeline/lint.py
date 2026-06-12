"""lint.py -- automated sprite-sheet checks per art-direction.md section 9.

Checks:
  a. on_palette      every opaque pixel's RGB is in palette.json's color set.
  b. orphan_pixels   every color region (8-connected, exact color) spans >= 3 px.
                     Whitelist mechanism: the sheet's sibling manifest may list
                     ``lint.whitelist_colors`` (deliberate sub-3px details:
                     eyes, element accents).
  c. no_aa           no anti-aliasing against transparency: alpha is 0 or 255.
  d. outline_1px     sel-out heuristic: silhouette-boundary pixels are darkest-
                     step colors (>= 85% coverage; whitelist + manifest
                     ``lint.prop_colors`` mid-tone props exempt) and no
                     2x2 block of pure ink #2e222f (thicker-than-1px outline).
  e. frames          per manifest frame cell: silhouette nonempty; grounded
                     animations put the lowest opaque row exactly on the ground
                     line (frame_h - 3); airborne animations stay above it.
  f. banding         no two successive ramp steps forming parallel 1 px bands
                     longer than 6 px hugging each other (art rule: cluster
                     shading, not gradient banding).
  g. manifest_contract  the attack row carries contact_frame within
                     [0, frames); foreswing_ticks / backswing_ticks echoed.
  h. body_size       per-template-class body mass floors (manifest
                     ``body_size``, props excluded): small ground bipeds
                     24-28 px tall x 14-18 px wide; sniper 40-56 px tall on
                     the 48x64 canvas (art doc section 4 -- height = range
                     threat); aerial 14-20 px tall with wingspan >= 24 px;
                     siege >= 26 px wide x 20 px tall (outweighs infantry).
                     Fails under the floor; over-ceiling is a warning only.
  i. whitelist_cap   whitelisted colors cover at most 6 px per frame.

Output: JSON report (written next to the sheet as <stem>.lint.json unless
--report is given). Exit code 0 = pass, 1 = fail, 2 = usage error.

CLI:
    python art_pipeline/lint.py <sheet.png> [--manifest m.json] [--report out.json]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

from palette import get_palette, rgb_to_hex

MIN_CLUSTER = 3
OUTLINE_DARK_COVERAGE = 0.85
MAX_SAMPLES = 12   # offender coordinates kept per check
MAX_BAND_RUN = 6   # parallel 1px same-ramp-successor bands longer than this fail
WHITELIST_CAP = 6  # max whitelisted (deliberate-detail) pixels per frame

# body-mass floors per template class (art fix 4 / lint upgrade c).
# "min" keys fail the check; "max" keys only produce warnings.
CLASS_BODY_RULES = {
    "melee_biped": {"min_h": 24, "max_h": 28, "min_w": 14, "max_w": 18},
    "ranged_biped": {"min_h": 24, "max_h": 28, "min_w": 14, "max_w": 18},
    # round 3: art doc section 4 sniper row -- 48x64 canvas, 40-56 px body
    "sniper_biped": {"min_h": 40, "max_h": 56, "min_w": 16, "max_w": 24},
    "support_robed": {"min_h": 24, "max_h": 28, "min_w": 14, "max_w": 18},
    "aerial_flyer": {"min_h": 14, "max_h": 20, "min_wingspan": 24},
    # round 3: a siege engine must outweigh infantry
    "siege_machine": {"min_h": 20, "min_w": 26},
}


def _find_manifest(sheet_path: Path) -> Path | None:
    stem = sheet_path.stem
    if stem.endswith("_sheet"):
        stem = stem[: -len("_sheet")]
    cand = sheet_path.parent / f"{stem}.manifest.json"
    return cand if cand.exists() else None


def lint_sheet(sheet_path: str | Path, manifest_path: str | Path | None = None) -> dict:
    sheet_path = Path(sheet_path)
    pal = get_palette()
    img = Image.open(sheet_path).convert("RGBA")
    w, h = img.size
    px = img.load()

    manifest = None
    mp = Path(manifest_path) if manifest_path else _find_manifest(sheet_path)
    if mp and mp.exists():
        with open(mp, "r", encoding="utf-8") as f:
            manifest = json.load(f)
    whitelist = set()
    prop_colors = set()
    if manifest:
        whitelist = {c.lower() for c in manifest.get("lint", {}).get("whitelist_colors", [])}
        prop_colors = {c.lower() for c in manifest.get("lint", {}).get("prop_colors", [])}

    opaque: dict[tuple[int, int], tuple[int, int, int]] = {}
    aa_offenders = []
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if a != 255:
                if len(aa_offenders) < MAX_SAMPLES:
                    aa_offenders.append([x, y, a])
                continue
            opaque[(x, y)] = (r, g, b)

    checks: dict[str, dict] = {}

    # (a) on-palette ------------------------------------------------------
    bad_colors: dict[str, list] = {}
    for (x, y), rgb in opaque.items():
        if rgb not in pal.colors_rgb:
            hx = rgb_to_hex(rgb)
            bad_colors.setdefault(hx, [])
            if len(bad_colors[hx]) < MAX_SAMPLES:
                bad_colors[hx].append([x, y])
    checks["on_palette"] = {"passed": not bad_colors, "off_palette_colors": bad_colors}

    # (c) no AA against transparency --------------------------------------
    aa_total = sum(1 for y in range(h) for x in range(w) if px[x, y][3] not in (0, 255))
    checks["no_aa"] = {"passed": aa_total == 0, "semi_transparent_pixels": aa_total,
                       "samples": aa_offenders}

    # (b) orphan pixels (8-connected exact-color regions < MIN_CLUSTER) ----
    # Exemptions: whitelisted colors (deliberate eyes / accents / 1px shafts)
    # and darkest-step colors touching the silhouette boundary -- sel-out
    # outlines (rule 5) legitimately fragment into 1-2 px segments at corners;
    # the cluster rule (rule 3) targets interior shading noise.
    eligible = pal.darkest_step_hexes()
    orphans = []
    seen: set[tuple[int, int]] = set()
    for start, rgb in opaque.items():
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
                    if p not in seen and opaque.get(p) == rgb:
                        seen.add(p)
                        region.append(p)
        if len(region) >= MIN_CLUSTER:
            continue
        hx = rgb_to_hex(rgb)
        if hx in whitelist:
            continue
        touches_edge = any(
            (cx + dx, cy + dy) not in opaque
            for (cx, cy) in region for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)))
        if hx in eligible and touches_edge:
            continue
        orphans.append({"color": hx, "size": len(region), "at": list(region[0])})
    checks["orphan_pixels"] = {"passed": not orphans,
                               "count": len(orphans),
                               "offenders": orphans[:MAX_SAMPLES],
                               "whitelist": sorted(whitelist)}

    # (d) outline: 1px sel-out heuristic -----------------------------------
    # prop_colors: mid-tone 2px prop bodies legitimately sit on the
    # silhouette boundary un-inked (art fix 1 -- they must survive 1x).
    eligible = pal.darkest_step_hexes() | whitelist | prop_colors
    boundary = dark = 0
    light_samples = []
    for (x, y), rgb in opaque.items():
        if all((x + dx, y + dy) in opaque
               for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0))):
            continue
        boundary += 1
        if rgb_to_hex(rgb) in eligible:
            dark += 1
        elif len(light_samples) < MAX_SAMPLES:
            light_samples.append([x, y, rgb_to_hex(rgb)])
    coverage = dark / boundary if boundary else 1.0
    ink = pal.outline_rgb
    fat_blocks = []
    for (x, y), rgb in opaque.items():
        if rgb == ink and opaque.get((x + 1, y)) == ink \
                and opaque.get((x, y + 1)) == ink and opaque.get((x + 1, y + 1)) == ink:
            if len(fat_blocks) < MAX_SAMPLES:
                fat_blocks.append([x, y])
    checks["outline_1px"] = {
        "passed": coverage >= OUTLINE_DARK_COVERAGE and not fat_blocks,
        "boundary_dark_coverage": round(coverage, 3),
        "light_boundary_samples": light_samples,
        "ink_2x2_blocks": fat_blocks,
    }

    # (e) per-frame silhouette + feet-on-groundline ------------------------
    frame_results = []
    frames_pass = True
    if manifest:
        fw, fh = manifest["frame_w"], manifest["frame_h"]
        gy = manifest.get("ground_y", fh - 3)
        airborne = set(manifest.get("lint", {}).get("airborne_animations", []))
        for anim in manifest["animations"]:
            row = anim["row"]
            for col in range(anim["frames"]):
                ys = [y - row * fh for (x, y) in opaque
                      if col * fw <= x < (col + 1) * fw and row * fh <= y < (row + 1) * fh]
                entry = {"animation": anim["name"], "frame": col}
                if not ys:
                    entry["error"] = "empty silhouette"
                    frames_pass = False
                else:
                    bottom = max(ys)
                    if anim["name"] in airborne:
                        if bottom >= gy:
                            entry["error"] = f"airborne frame touches ground line (bottom row {bottom} >= {gy})"
                            frames_pass = False
                    elif bottom != gy:
                        entry["error"] = f"feet not on ground line (bottom row {bottom}, expected {gy})"
                        frames_pass = False
                if "error" in entry:
                    frame_results.append(entry)
        checks["frames"] = {"passed": frames_pass, "failures": frame_results}
    else:
        checks["frames"] = {"passed": True, "skipped": "no manifest found; frame checks not run"}

    # (f) banding: two successive ramp steps as hugging parallel 1px bands ---
    hexes = {p: rgb_to_hex(rgb) for p, rgb in opaque.items()}
    adjacent_steps = set()
    for steps in pal.ramps.values():
        for i in range(len(steps) - 1):
            adjacent_steps.add(frozenset((steps[i], steps[i + 1])))

    def _band_runs(primary_axis_len, cross_axis_len, at):
        """Yield (start, cross, length) runs of 1px/1px successive-step bands."""
        for c in range(cross_axis_len - 1):
            run = 0
            start = None
            for p in range(primary_axis_len):
                h1 = hexes.get(at(p, c))
                h2 = hexes.get(at(p, c + 1))
                ok = (h1 is not None and h2 is not None and h1 != h2
                      and frozenset((h1, h2)) in adjacent_steps
                      and hexes.get(at(p, c - 1)) != h1
                      and hexes.get(at(p, c + 2)) != h2)
                if ok:
                    if run == 0:
                        start = p
                    run += 1
                else:
                    if run > MAX_BAND_RUN:
                        yield (start, c, run)
                    run = 0
            if run > MAX_BAND_RUN:
                yield (start, c, run)

    band_offenders = []
    for start, yy, length in _band_runs(w, h, lambda p, c: (p, c)):
        band_offenders.append({"orientation": "horizontal", "at": [start, yy], "length": length})
    for start, xx, length in _band_runs(h, w, lambda p, c: (c, p)):
        band_offenders.append({"orientation": "vertical", "at": [xx, start], "length": length})
    checks["banding"] = {"passed": not band_offenders,
                         "max_run": MAX_BAND_RUN,
                         "offenders": band_offenders[:MAX_SAMPLES]}

    # (g) manifest contract: contact frame + sim tick echo -------------------
    contract_errors = []
    if not manifest:
        contract_errors.append("manifest missing")
    else:
        attacks = [a for a in manifest.get("animations", []) if a.get("name") == "attack"]
        if not attacks:
            contract_errors.append("no attack animation row")
        else:
            cf = attacks[0].get("contact_frame")
            frames_n = attacks[0].get("frames", 0)
            if not isinstance(cf, int) or not 0 <= cf < frames_n:
                contract_errors.append(
                    f"attack contact_frame {cf!r} not an int within [0, {frames_n})")
        for key in ("foreswing_ticks", "backswing_ticks"):
            if not isinstance(manifest.get(key), int):
                contract_errors.append(f"{key} missing or not an int")
    checks["manifest_contract"] = {"passed": not contract_errors, "errors": contract_errors}

    # (h) body size per template class (manifest body_size, props excluded) --
    size_errors = []
    size_warnings = []
    body = (manifest or {}).get("body_size")
    rules = CLASS_BODY_RULES.get((manifest or {}).get("typeclass", ""))
    # aerial floors scale with the canvas (FP batch: 48/64/96 flyers scale
    # body-part proportions, never pixel-double) -- base rules are per-32px.
    if rules and manifest and manifest.get("typeclass") == "aerial_flyer":
        k = max(1.0, manifest.get("frame_h", 32) / 32)
        if k > 1.0:
            rules = {key: round(v * k) for key, v in rules.items()}
    if not manifest:
        size_errors.append("manifest missing")
    elif body is None:
        size_errors.append("manifest has no body_size block")
    elif rules:
        bw, bh = body.get("width", 0), body.get("height", 0)
        ws = body.get("wingspan", 0)
        if "min_h" in rules and bh < rules["min_h"]:
            size_errors.append(f"body height {bh} under floor {rules['min_h']}")
        if "min_w" in rules and bw < rules["min_w"]:
            size_errors.append(f"body width {bw} under floor {rules['min_w']}")
        if "min_wingspan" in rules and ws < rules["min_wingspan"]:
            size_errors.append(f"wingspan {ws} under floor {rules['min_wingspan']}")
        if "max_h" in rules and bh > rules["max_h"]:
            size_warnings.append(f"body height {bh} over target {rules['max_h']}")
        if "max_w" in rules and bw > rules["max_w"]:
            size_warnings.append(f"body width {bw} over target {rules['max_w']}")
    checks["body_size"] = {"passed": not size_errors, "errors": size_errors,
                           "warnings": size_warnings, "measured": body,
                           "rules": rules}

    # (i) whitelist cap: deliberate-detail colors <= 6 px per frame ----------
    cap_offenders = []
    if manifest and whitelist:
        fw, fh = manifest["frame_w"], manifest["frame_h"]
        counts: dict[tuple[str, int], int] = {}
        anims_by_row = {a["row"]: a for a in manifest["animations"]}
        for (x, y), hx in hexes.items():
            if hx not in whitelist:
                continue
            anim = anims_by_row.get(y // fh)
            if anim is None or x // fw >= anim["frames"]:
                continue
            key = (anim["name"], x // fw)
            counts[key] = counts.get(key, 0) + 1
        for (anim_name, col), n in sorted(counts.items()):
            if n > WHITELIST_CAP:
                cap_offenders.append({"animation": anim_name, "frame": col, "pixels": n})
    checks["whitelist_cap"] = {"passed": not cap_offenders, "cap": WHITELIST_CAP,
                               "offenders": cap_offenders[:MAX_SAMPLES]}

    passed = all(c["passed"] for c in checks.values())
    return {
        "sheet": str(sheet_path),
        "manifest": str(mp) if mp else None,
        "passed": passed,
        "checks": checks,
    }


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Lint a generated sprite sheet.")
    ap.add_argument("sheet", help="path to <name>_sheet.png")
    ap.add_argument("--manifest", default=None, help="manifest path (default: sibling <name>.manifest.json)")
    ap.add_argument("--report", default=None, help="report JSON path (default: <sheet stem>.lint.json)")
    args = ap.parse_args(argv)

    sheet = Path(args.sheet)
    if not sheet.exists():
        print(f"error: no such file: {sheet}", file=sys.stderr)
        return 2
    report = lint_sheet(sheet, args.manifest)
    out = Path(args.report) if args.report else sheet.with_name(sheet.stem + ".lint.json")
    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2)

    print(f"{'PASS' if report['passed'] else 'FAIL'}  {sheet.name}")
    for name, c in report["checks"].items():
        print(f"  [{'ok' if c['passed'] else 'XX'}] {name}")
    print(f"report: {out}")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
