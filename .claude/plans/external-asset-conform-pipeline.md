# Plan — External-Asset Conform Pipeline (Part D)

**Roadmap:** `C:\Users\jmund\OneDrive\Documents\ObsidianVault\DevProjects\DraconicWars\Claude\BrainstormingDesigns\2026-06-11-game-foundation\roadmap.md` — Part **External-Asset Conform Pipeline** (design doc `art-pipeline-uplift.md` §Track 2, "Part D")

**Mode:** `/part_drive` one-shot. **Domain:** Art pipeline (Python sidecar) — pure Logic, strict TDD. **No `.cs` changed.**

## Goal

Drop generously-licensed CC0 external sprites into the palette-locked, lint-validated, manifest-driven pipeline for silhouette-distinct heroes. Three deliverables: `conform_external.py`, a `source:external` relaxed lint profile (TDD-first), and a checked-in CREDITS/license ledger. Prove end-to-end by conforming a source sheet to a palette-passing, ground-anchored, manifest-valid DW sheet.

## Brief & Drift Classification (verified against code)

- **No macro drift.** Roadmap row `EAC["External-Asset Conform Pipeline"]:::plan` (plan-pending); dep `Art Pipeline Toolkit` is `complete` (roadmap line 277) → actionable. Siblings: `Silhouette Shape-Language` (also plan-pending — **shares `lint.py`**, the coordination warning) and `Hero A/B/C Bake-off` (waits on this Part).
- **Manifest contract verified** from `generate_unit.py` + real manifests (`cinder_acolyte`, `storm_gryphon`): keys `name, typeclass, element, size_class, frame_w, frame_h, animations[], foreswing_ticks, backswing_ticks, ramps_used, pivot:"bottom_center", ground_y, facing:"right", body_size{w,h[,wingspan]}, lint{whitelist_colors, prop_colors, airborne_animations}`. Animation set = idle / walk|fly / attack / death; attack carries `contact_frame`.
- **Lint contract verified** (`lint.py`): `lint_sheet()` returns `{passed, checks{...}}`; overall `passed = all(c["passed"])` (flat AND, line 335). Checks: on_palette, no_aa, orphan_pixels, outline_1px, frames, banding, manifest_contract, body_size, whitelist_cap.
- **Reuse seams verified** (`skeletons.py`): `ground_row(H)=H-3` (line 34), `parse_canvas` (1415); (`generate_unit.py`): `contact_frame_index(fore,back,frames)` (51) already implements "contact maps to foreswing/backswing share, not source timing"; (`palette.py`): `get_palette()`, `pal.get/hex_of(ramp,idx)`, `pal.colors_rgb`, `pal.ramps`.
- **Environment verified:** Python 3.14.2, Pillow 12.2.0, **no numpy, no sklearn** → k-means is hand-rolled (1-D Lloyd's on luminance), pure-Python, deterministic. **No existing Python tests** (`art_pipeline/test_*.py` absent) → introduce the `unittest` (stdlib) convention.
- **PixelRefiner** (design-flagged "verify repo before integration") — **dropped from scope.** It is an unconfirmed third-party tool; the hand-rolled posterize+ramp-map fully satisfies the palette contract by construction, which is what PixelRefiner was meant to provide. Recorded in Decision Record.

## Resolved Fork (halt-valve f) — real-asset E2E proof

Acceptance says "conform ONE *real* Luiz Melo CC0 sheet." User chose **"I attempt the download."** Outcome:
- **License VERIFIED** at `https://luizmelo.itch.io/wizard-pack` (2026-06-13): *Creative Commons Zero v1.0 Universal* + "No generative AI was used." File: `Wizard Pack.zip` (68 kB).
- **Binary download BLOCKED** (authorized fallback): itch.io free downloads are JS-session-gated — static HTML exposes the file name/size but no `upload_id`/signed URL; a download-session POST returns itch's generic error page. `curl` reads the page (license) but not the binary.
- **Resolution:** build + prove the pipeline on a **deterministic synthetic source fixture** (reproducible in the test suite — the TDD-correct proof), wire it so a real sheet drops in at a known path, populate the ledger with the verified CC0 entry (local-file + download-date marked pending the user's manual grab), and leave drop-in instructions. **No third-party binary committed** (none could be obtained).

## Spec-Doc Coverage (design mechanic → plan section)

| Design mechanic (`art-pipeline-uplift.md` §Track 2) | Covered by |
|---|---|
| posterize to N value levels (k-means on luminance) | `conform_external.py` §A (hand-rolled 1-D k-means) |
| map each level → ONE ramp step per material (palette-compliant by construction) | §B (material hue-assignment + level→ramp-index map) |
| hard-threshold alpha to 0/255 | §C |
| re-slice into one-anim-per-row (idle/walk-or-fly/attack/death) | §D (mapping `rows[]`) |
| re-canvas to 32x32 or 48x64 | §E (fit-scale nearest-neighbor) |
| re-anchor lowest opaque row to ground line (`H-3` grounded, above for airborne) | §F (reuse `ground_row`) |
| emit compliant manifest; `contact_frame` maps to external attack anim, not source timing | §G (reuse `contact_frame_index`) |
| relaxed lint via manifest `source:external` flag; HARD={on_palette,frames,manifest_contract,body_size}; warnings={no_aa,outline_1px,banding} | `lint.py` severity layer (TDD-first) |
| CREDITS/license ledger: URL, artist, license name, copy of license text, download date | `art_pipeline/external/CREDITS.md` |

## Architecture Decisions

1. **Relaxed lint = severity layer, not a fork.** Keep every check computing exactly as today. After building `checks` (a **dict**), tag every check with `severity` (default "error"; "warning" only for the demoted three AND only iff `manifest.get("source")=="external"`), then recompute `passed = all(c["passed"] for c in checks.values() if c.get("severity") != "warning")` — **iterate `.values()`, not `checks`** (iterating the dict yields key strings → `TypeError`). Add `report["profile"] = "external"|"strict"`. Demoted checks still report findings (warnings stay visible). Demote **exactly** `no_aa, outline_1px, banding` (verbatim per spec + user args) — orphan_pixels & whitelist_cap stay HARD. **Regression invariant:** for a manifest with no `source` key (the 24 existing units) OR any non-`external` value, every check stays `severity=="error"` so `passed` is byte-identical to today's flat AND.
2. **k-means hand-rolled** (no numpy/sklearn). 1-D Lloyd's on luminance, fixed deterministic init (evenly-spaced percentiles), fixed iteration cap → identical input ⇒ identical output. Honors the pipeline's "identical spec ⇒ identical bytes" invariant.
3. **Palette-compliance by construction.** Output pixel = `pal.get(material.ramp, level_to_index(level, ramp_len))`. No computed shades — satisfies the palette contract trivially (the design's stated reason for the posterize→ramp approach).
4. **Material assignment by nearest source-hue** (mapping declares each material's ramp + optional hue anchor). Degrades to single-material (all opaque pixels → one ramp by luminance) when one material is declared — the core proof path.
5. **`body_size` measured on opaque pixels, not declared** — re-implemented locally (NOT an import: `generate_unit._body_bbox_size` is coupled to `PixelBuffer.cells`/`part`, which conformed RGBA images don't have). Bbox of opaque conformed pixels, max over idle+move anims; aerial wingspan = max width over fly frames. **The fit-scale (§E) targets the typeclass body-height band, not raw canvas-fit** — scale so the conformed silhouette height lands in `[min_h, ground_y]` for the typeclass (e.g. melee_biped → 24–28 px on a 32 px cell; over-ceiling is a warning, under-floor is a HARD fail even under the external profile). A short/wide source that canvas-fits to <min_h would HARD-fail `body_size`; the scale must prevent that.
6. **Reuse, don't duplicate (where importable):** `from skeletons import ground_row, parse_canvas`; `from generate_unit import contact_frame_index`. Single source of truth for ground line / canvas / contact timing. **Manifest-dict assembly is NOT extracted** (see DR4).
7. **Output dir** `art_pipeline/output/external/` (not `output/units/` — keeps conformed externals out of `RosterArtContractTest`'s `FullRoster` sweep). Conformed externals also intentionally bypass roster-level distinctness/IoU lint (Part B, not yet built) — **hero distinctness is Part E's concern (Hero A/B/C)**, not Part D's. Tests write to a tmp dir.
8. **Deterministic k-means** — fixed percentile init; on assignment ties, assign to the **lower-index centroid**; on an empty cluster, keep its previous centroid (no random re-seed). Sorted/stable float ops only. Pinned by a re-run-identical-bytes test (AD2 invariant).

## TDD Plan (RED first)

**Fixture geometry (pinned to avoid false-RED, per plan_check C2/F2/F3):** cells are **32×32** (`frame_w=frame_h=32`, `ground_y=29`); 4 single-frame rows → sheet **32×128**. Body bars ~16w×26h, lowest opaque row at exactly 29 per cell (passes `frames`+`body_size` min_h 24/min_w 14). Body colors are **mid-ramp (index ≥1, asserted ∉ `pal.darkest_step_hexes()`)** so `outline_1px` deterministically fails (boundary-dark-coverage <0.85). All color regions **≥3 connected px** (passes `orphan_pixels`); bars **≥2px thick** (no 1px ramp-successor `banding` run). No whitelist in the manifest (`whitelist_cap` n/a).

**`art_pipeline/test_lint.py`** (unittest, stdlib) — write FIRST, prove RED:
- `test_relaxable_strict_fails`: fixture (above) — all HARD checks pass, `outline_1px` fails. No `source` → `passed is False`, `outline_1px["severity"]=="error"`, `profile=="strict"`.
- `test_non_external_source_stays_hard`: same fixture + `source:"generated"` (a non-`external` value) → `passed is False`, demotable checks still `severity=="error"`, `profile=="strict"` (pins the 24-unit regression invariant: only `external` relaxes).
- `test_relaxable_external_passes`: same fixture + `source:"external"` → `passed is True`, `outline_1px["passed"] is False` but `severity=="warning"`, `profile=="external"`.
- `test_hard_violation_external_still_fails`: off-palette pixel + `source:"external"` → `passed is False` (on_palette is HARD).
- `test_demoted_set_exact`: under external, exactly {no_aa,outline_1px,banding} are warning-severity; all others error. (This test catches the dict-iteration bug C1 if present.)

**`art_pipeline/test_conform_external.py`** — the end-to-end proof:
- Build a deterministic synthetic source sheet (4 rows; **wide luminance blocks ≥3px each × 1–2 distinct hues**; opaque source body sized so the conformed body lands in the melee_biped band).
- `conform_external(...)` with a mapping (`source:external`, typeclass melee_biped, levels=3).
- Assert: output PNG + manifest exist; `lint_sheet(out_sheet)` → `passed is True` under the external profile. Crucially the **non-demoted** HARD checks must pass: `on_palette` (by construction), `frames` (anchored to 29), `manifest_contract`, `body_size` (in band), **`orphan_pixels`** (regions ≥3px — the fixture invariant guards this), `whitelist_cap` (no whitelist). `outline_1px`/`banding` may appear as warnings. This proves palette-passing + ground-anchored + manifest-valid in one deterministic run.
- `test_conform_is_deterministic`: run `conform_external` twice on the same source → identical PNG bytes + identical manifest (pins AD2/AD8).

## File-by-File Plan

| File | Action | Notes |
|---|---|---|
| `art_pipeline/test_lint.py` | **new** | RED-first relaxed-profile tests |
| `art_pipeline/lint.py` | **edit** | severity layer (~15 lines): `RELAXED_WARNING_CHECKS` const, read `manifest.source`, tag `severity`, recompute `passed`, add `profile`; CLI shows `[ww]` for warnings |
| `art_pipeline/conform_external.py` | **new** | the conform pipeline + CLI |
| `art_pipeline/test_conform_external.py` | **new** | synthetic E2E proof |
| `art_pipeline/external/CREDITS.md` | **new** | license ledger + full CC0 1.0 legalcode + Luiz Melo entry |
| `art_pipeline/external/README.md` | **new** (small) | drop-in instructions + example mapping for a real Wizard Pack conform |

## Decision Record

- **DR1:** PixelRefiner dropped (unconfirmed tool; hand-rolled posterize+ramp-map supersedes its purpose). Reversible — a future Part can add it as a pre-pass.
- **DR2:** Relaxed profile demotes exactly the three named checks; orphan_pixels/whitelist_cap stay HARD (spec is silent on them → no relaxation, faithful to "demote no_aa/outline_1px/banding").
- **DR3 (SUPERSEDED — real asset downloaded + conformed):** The earlier "itch download is JS-gated, defer to manual drop-in" was **wrong**. The Wizard Pack binary was fetched non-interactively via the itch CSRF flow (page→`download_url`→XHR interstitial→`upload_id`→`file/{id}`→R2 CDN; see `external/CREDITS.md` + `gotcha_itch_io_free_download_scriptable.md`) and **conformed end-to-end to `output/external/luiz_wizard_sheet.png` — `PASS [external]`**. Source strips + zip committed under `external/sources/luiz_wizard/` (CC0 permits redistribution). The DoD's "prove on ONE real Luiz Melo sheet" is now met for real, not by synthetic proxy.
- **DR5 (despeckle pass added — the synthetic proof was insufficient):** The real-asset conform exposed a gap the synthetic fixture (clean ≥3px bands) structurally could not: nearest-neighbor downscaling of detailed art (231→32px) fragments thin features (staff, glints, magic motes) into sub-3px specks → HARD `orphan_pixels` fails (228 orphans; even monochrome 2-level → 81). Parameter tuning cannot fix it. Added `_denoise_orphans()` (TDD: `test_downscale_specks_are_denoised` RED→GREEN) — a deterministic, palette-safe despeckle run on the scaled crop **before** ground-anchoring (so dropping a foot speck can't lift the sprite off the line), absorbing sub-`MIN_CLUSTER` regions into their dominant 8-neighbor or deleting isolated motes. Subtlety fixed: live-update the `opaque` map on merge so two mutually-adjacent specks collapse together instead of swapping colors forever. Lesson memorialized: [[gotcha-synthetic-fixture-hides-real-input-failure]].
- **DR4 (manifest-dict duplication, plan_check pattern-fit F2):** `conform_external.py` re-builds the manifest dict rather than extracting a shared `build_manifest()` from `generate_unit.py`. Rationale: (a) the two callers have different inputs (template roles/buffers vs external pixels/materials) so a shared builder would be heavily conditional; (b) extracting would EDIT a working generator and risk byte-drift across the 24 shipped manifests, demanding a parity capture for a DRY win; (c) **the lint IS the contract guardian** — `manifest_contract`/`frames`/`body_size` checks fail if conform's manifest shape diverges, and the conform E2E test runs `lint_sheet` on the output, so divergence is caught by construction; (d) rule-of-three not met (two callers). Revisit if a third manifest producer appears.

## Closing Steps (Definition of Done)

1. `python art_pipeline/test_lint.py` + `python art_pipeline/test_conform_external.py` green; manual `lint.py` CLI on the conformed synthetic output shows PASS (external profile).
2. `/regression_gate` single-flight (shared GdUnit4 pipe — stagger vs other sessions). No `.cs` changed, so the C# suite must remain green (esp. `RosterArtContractTest`) and the build clean.
3. Categorical commits (feat: conform pipeline + lint profile; chore/docs: CREDITS ledger). No push. Concurrent-session index hygiene: stage by explicit path, verify committed file list.
4. `/update_roadmap` External-Asset Conform Pipeline → `complete`.

## Unresolved Questions

- **Q1 (RESOLVED):** Wizard Pack downloaded, conformed, committed (source strips + zip + conformed DW sheet). See DR3.
- **Q2 (RESOLVED):** Multi-material hue anchors tuned against the real pixels — robe `shadow` (h≈280, was wrongly `storm` h≈64 olive), trim `rose`, skin `peach`, staff `radiant`, magic `frost_teal`. Ramp identity is a taste knob the user can re-map; palette-compliance holds for any choice.
