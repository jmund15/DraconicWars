"""lint.py -- automated sprite-sheet checks per art-direction.md section 9.

Checks:
  a. on_palette      every opaque pixel's RGB is in palette.json's color set.
  b. orphan_pixels   every color region (8-connected, exact color) spans >= 3 px.
                     Whitelist mechanism: the sheet's sibling manifest may list
                     ``lint.whitelist_colors`` (deliberate sub-3px details:
                     eyes, element accents, 1 px prop shafts).
  c. no_aa           no anti-aliasing against transparency: alpha is 0 or 255.
  d. outline_1px     sel-out heuristic: silhouette-boundary pixels are darkest-
                     step colors (>= 85% coverage, whitelist exempt) and no
                     2x2 block of pure ink #2e222f (thicker-than-1px outline).
  e. frames          per manifest frame cell: silhouette nonempty; grounded
                     animations put the lowest opaque row exactly on the ground
                     line (frame_h - 3); airborne animations stay above it.

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
MAX_SAMPLES = 12  # offender coordinates kept per check


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
    if manifest:
        whitelist = {c.lower() for c in manifest.get("lint", {}).get("whitelist_colors", [])}

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
    eligible = pal.darkest_step_hexes() | whitelist
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
