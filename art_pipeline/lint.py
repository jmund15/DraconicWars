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

Profiles: a manifest ``"source": "external"`` selects the relaxed profile, which
demotes {no_aa, outline_1px, banding} to non-gating warnings (curated CC0 art
conformed via conform_external.py). All other checks stay hard. Absent / any
other source value = the strict profile (identical to the historical behavior).
The report carries ``profile`` and a per-check ``severity`` ("error"|"warning").

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

# Relaxed profile: a manifest with ``"source": "external"`` (curated CC0 art
# conformed via conform_external.py) demotes these three checks to warnings --
# external pixel art has no sel-out ink outline (outline_1px), legitimately uses
# gradient shading (banding), and is alpha-thresholded but may carry residual
# soft edges (no_aa). The load-bearing checks (on_palette, frames,
# manifest_contract, body_size) AND orphan_pixels / whitelist_cap stay hard for
# every profile -- only the exact string "external" relaxes anything.
RELAXED_WARNING_CHECKS = ("no_aa", "outline_1px", "banding")

# --- cross-unit silhouette-distinctness constants (roster_distinctness) -------
# A body-mask pair AT OR ABOVE this IoU reads as "the same rig recolored".
# Calibrated in the GAP between the two regimes (NOT inflated to pass): the
# pre-Part-B near-identical bipeds measured 0.85-1.0 (all caught); the reworked
# shape-language spread tops out at 0.70 (all clear). Two genuinely-distinct
# humanoids share ~0.65-0.70 (same head/torso/2-leg body plan) -- that is
# different shape-language, not a duplicate rig; 0.77 sits between with margin.
IOU_MAX = 0.77
# A dragon-tier silhouette must outweigh the heaviest infantry by this area
# factor (art-direction.md section 4 -- the premium read).
SCALE_GAP_MIN = 3.0
# head_h / body_height band per archetype: the shape-language head-proportion
# lever (agile=rounder/larger head, sturdy=small low head, dangerous=wedge).
HEAD_BANDS = {
    "melee_biped": (0.20, 0.46),
    "ranged_biped": (0.20, 0.46),
    "support_robed": (0.16, 0.42),
    "sniper_biped": (0.14, 0.36),
    "aerial_flyer": (0.10, 0.42),
    "ogre": (0.10, 0.34),   # small head sunk low in a big body
}
NO_HEAD_CLASSES = {"siege_machine", "slime"}  # no head mass -> head_to_body skips it (typed)

# A cross-FORM body-mask pair AT OR ABOVE this IoU reads as the same creature
# shape wearing two names -- the defect this Part exists to kill. Set BELOW the
# same-form 0.77 bar: two genuinely-different creature forms (a humanoid vs an
# ogre/slime) must overlap notably LESS than two humanoids do. Calibrated from
# the observed S1 forms (disclosed in the commit), never inflated to pass.
CROSS_FORM_IOU_MAX = 0.55
# typeclass -> creature FORM class. Same-form pairs are gated by IOU_MAX (rig
# recolor); cross-form pairs by CROSS_FORM_IOU_MAX (different creatures). The 4
# humanoid typeclasses share one form by design (people DO share a body plan).
FORM_CLASS = {
    "melee_biped": "humanoid", "ranged_biped": "humanoid",
    "sniper_biped": "humanoid", "support_robed": "humanoid",
    "ogre": "ogre", "construct": "construct", "revenant": "revenant",
    "slime": "slime", "spider": "spider",
    "aerial_flyer": "aerial", "siege_machine": "siege",
}

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
    # a hulking ogre: broader + bulkier than infantry, on the 32px canvas
    "ogre": {"min_h": 18, "max_h": 30, "min_w": 16, "max_w": 26},
    # a spread blob: wide + squat, on the 40px canvas
    "slime": {"min_h": 12, "max_h": 24, "min_w": 18, "max_w": 36},
}

# The canvas height each class's body rules are calibrated for. A unit rendered
# on a taller frame (size tier) scales its floors by frame_h / base_h so the
# body-mass gate tracks the tier instead of a fixed 32px floor. sniper_biped is
# natively 64px (rules already tuned to it -> never double-counts); siege_machine
# is deliberately absent (fixed-size engines are never floor-scaled).
CLASS_BASE_H = {
    "melee_biped": 32, "ranged_biped": 32, "support_robed": 32,
    "sniper_biped": 64, "aerial_flyer": 32, "ogre": 32, "slime": 32,
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

    # Band tolerance scales with the canvas: a clean larger fill carries
    # proportionally longer flat runs, so the 32px-tuned 6px floor would
    # false-fail big-tier units. The internal-form detail (rim-light / shading)
    # that breaks these runs on large units is owned by the Sprite Detail Passes
    # Part, not this size pass.
    band_limit = round(MAX_BAND_RUN * (manifest.get("frame_h", 32) / 32)) if manifest else MAX_BAND_RUN

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
                    if run > band_limit:
                        yield (start, c, run)
                    run = 0
            if run > band_limit:
                yield (start, c, run)

    band_offenders = []
    for start, yy, length in _band_runs(w, h, lambda p, c: (p, c)):
        band_offenders.append({"orientation": "horizontal", "at": [start, yy], "length": length})
    for start, xx, length in _band_runs(h, w, lambda p, c: (c, p)):
        band_offenders.append({"orientation": "vertical", "at": [xx, start], "length": length})
    checks["banding"] = {"passed": not band_offenders,
                         "max_run": band_limit,
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
    # Body floors scale with the size tier (taller canvas -> bigger body), per
    # the class's calibrated base height. Flyers/ogres/scaled bipeds scale up;
    # sniper (base 64) and siege (absent) never double-count.
    base_h = CLASS_BASE_H.get((manifest or {}).get("typeclass", ""))
    if rules and manifest and base_h:
        k = manifest.get("frame_h", base_h) / base_h
        if k != 1.0:
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

    # Profile / severity layer: under the relaxed external profile the demoted
    # checks become warnings (still reported, but not gating). Every check is
    # tagged so callers/tests can read the full partition; ``passed`` is then
    # the AND over non-warning checks only. A missing/non-"external" source
    # leaves every severity "error" -> byte-identical to the strict flat AND.
    relaxed = bool(manifest) and manifest.get("source") == "external"
    demoted = RELAXED_WARNING_CHECKS if relaxed else ()
    for name, check in checks.items():
        check["severity"] = "warning" if name in demoted else "error"
    passed = all(c["passed"] for c in checks.values() if c["severity"] != "warning")
    return {
        "sheet": str(sheet_path),
        "manifest": str(mp) if mp else None,
        "profile": "external" if relaxed else "strict",
        "passed": passed,
        "checks": checks,
    }


# ===========================================================================
# Cross-unit silhouette distinctness (roster-level, not per-sheet)
# ===========================================================================
# These operate on solid black-fill BODY masks (props excluded -- the body is
# what "recolored one rig" duplicates). generate_unit.py writes the body-only
# mask as <name>.silhouette.png; run_roster_batch.py collects them and calls
# roster_distinctness, writing roster_distinctness.json for the C# art-contract
# gate. The math is pinned by test_distinctness.py (incl. the dragon area gap,
# which the live roster can't exercise -- no dragon-tier unit).


def silhouette_mask(img) -> set[tuple[int, int]]:
    """Opaque (alpha==255) pixel coords of an RGBA image -- a black-fill mask.
    The pipeline renders binary alpha (no AA against transparency, rule 9)."""
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    return {(x, y) for y in range(h) for x in range(w) if px[x, y][3] == 255}


def iou(a: set, b: set) -> float:
    """Intersection-over-union of two coord sets. Empty/empty -> 0 (no div0)."""
    union = len(a | b)
    return len(a & b) / union if union else 0.0


def align_bottom_center(mask: set) -> frozenset:
    """Translate so the mask's bbox bottom-center sits at the origin (feet y=0,
    horizontal center x=0). Lets silhouettes be compared foot- and center-
    aligned regardless of canvas size or placement on the canvas."""
    if not mask:
        return frozenset()
    xs = [p[0] for p in mask]
    ys = [p[1] for p in mask]
    cx = round((min(xs) + max(xs)) / 2)
    by = max(ys)
    return frozenset((x - cx, y - by) for (x, y) in mask)


def roster_distinctness(units: list[dict], iou_max: float = IOU_MAX,
                        scale_gap_min: float = SCALE_GAP_MIN,
                        head_bands: dict | None = None,
                        cross_form_iou_max: float = CROSS_FORM_IOU_MAX) -> dict:
    """Roster-level distinctness report. ``units`` = list of dicts with keys
    ``name``, ``typeclass``, ``mask`` (set of body-pixel coords), ``body_size``
    ({width, height, head_w, head_h}). ``passed`` = every NON-skipped check
    passes; a skipped check (e.g. scale_gap with no dragon, or cross_form with a
    single form present) is NEUTRAL."""
    head_bands = HEAD_BANDS if head_bands is None else head_bands
    checks: dict[str, dict] = {}

    # (j) pairwise body-silhouette IoU, foot/center aligned --------------------
    aligned = [(u["name"], align_bottom_center(u["mask"])) for u in units]
    matrix: dict[str, float] = {}
    iou_offenders = []
    for i in range(len(aligned)):
        for k in range(i + 1, len(aligned)):
            ni, mi = aligned[i]
            nk, mk = aligned[k]
            v = round(iou(set(mi), set(mk)), 4)
            matrix[f"{ni}|{nk}"] = v
            if v >= iou_max:
                iou_offenders.append({"pair": [ni, nk], "iou": v})
    iou_offenders.sort(key=lambda o: -o["iou"])
    checks["silhouette_distinctness"] = {
        "passed": not iou_offenders, "iou_max": iou_max,
        "offenders": iou_offenders, "matrix": matrix,
    }

    # (m) cross-form distinctness: different creature FORMS must not share a
    # silhouette (an 'ogre'/'slime' reading as a recolored humanoid is the
    # defect). Reuses the pairwise IoU matrix; only cross-form pairs are judged,
    # against the stricter cross_form_iou_max. <2 forms present -> NEUTRAL skip.
    forms = [FORM_CLASS.get(u.get("typeclass", "")) for u in units]
    cross_offenders = []
    cross_pairs = 0
    for i in range(len(aligned)):
        for k in range(i + 1, len(aligned)):
            fi, fk = forms[i], forms[k]
            if fi is None or fk is None or fi == fk:
                continue
            cross_pairs += 1
            v = matrix[f"{aligned[i][0]}|{aligned[k][0]}"]
            if v >= cross_form_iou_max:
                cross_offenders.append({"pair": [aligned[i][0], aligned[k][0]],
                                        "forms": [fi, fk], "iou": v})
    if cross_pairs == 0:
        checks["cross_form_distinctness"] = {
            "passed": True, "skipped": "fewer than two creature forms present"}
    else:
        cross_offenders.sort(key=lambda o: -o["iou"])
        checks["cross_form_distinctness"] = {
            "passed": not cross_offenders,
            "cross_form_iou_max": cross_form_iou_max,
            "offenders": cross_offenders,
        }

    # (k) dragon-to-infantry area gap (typed dragon signal ONLY) ---------------
    areas = {u["name"]: len(u["mask"]) for u in units}
    dragons = [u for u in units if u.get("typeclass") == "dragon"]
    if not dragons:
        checks["scale_gap"] = {"passed": True,
                               "skipped": "no dragon-tier unit present"}
    else:
        infantry = [u for u in units if u.get("typeclass") != "dragon"]
        base = max((areas[u["name"]] for u in infantry), default=0)
        dragon_area = min(areas[u["name"]] for u in dragons)
        ratio = (dragon_area / base) if base else float("inf")
        checks["scale_gap"] = {
            "passed": ratio >= scale_gap_min, "min": scale_gap_min,
            "ratio": round(ratio, 3), "dragon_area": dragon_area,
            "infantry_baseline": base,
        }

    # (l) per-archetype head-to-body ratio band -------------------------------
    head_offenders = []
    measured = []
    for u in units:
        tc = u.get("typeclass", "")
        if tc in NO_HEAD_CLASSES:
            continue
        band = head_bands.get(tc)
        if band is None:
            continue
        bs = u.get("body_size", {})
        bh = bs.get("height", 0)
        ratio = (bs.get("head_h", 0) / bh) if bh else 0.0
        entry = {"name": u["name"], "ratio": round(ratio, 3), "band": list(band)}
        measured.append(entry)
        if not (band[0] <= ratio <= band[1]):
            head_offenders.append(entry)
    checks["head_to_body"] = {"passed": not head_offenders,
                              "offenders": head_offenders, "measured": measured}

    return {"passed": all(c["passed"] for c in checks.values()),
            "unit_count": len(units), "checks": checks}


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

    print(f"{'PASS' if report['passed'] else 'FAIL'}  {sheet.name}  [{report['profile']}]")
    for name, c in report["checks"].items():
        mark = "ok" if c["passed"] else ("ww" if c.get("severity") == "warning" else "XX")
        print(f"  [{mark}] {name}")
    print(f"report: {out}")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
