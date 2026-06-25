# Plan — Part B: Silhouette Shape-Language + Distinctness Lint

**Roadmap:** `OneDrive/.../BrainstormingDesigns/2026-06-11-game-foundation/roadmap.md` — Part **Silhouette Shape-Language + Distinctness Lint**
**Design surface:** `art-pipeline-uplift.md` §Track 1 + §"Implementation Parts" Part B (sibling of the roadmap).
**Mode:** `/part_drive` one-shot (brief → plan → plan_check → TDD → gate → commits → roadmap-complete).

## Goal

Stop every biped being the same body recolored. Make per-unit silhouette distinctness **machine-checkable** (extend the lint), then rework the rigs toward a distinct shape-language (circles=agile, squares=sturdy, triangles=dangerous) + per-seed asymmetry, and fix the `quarry_slinger` T-pose. Acceptance: lint **RED** on the current near-identical bipeds → **GREEN** after differentiation; roster strip visibly distinct.

## Brief findings (verified against the codebase)

- **The gate is file-based.** `RosterArtContractTest.cs` reads per-unit `*.manifest.json` from `art_pipeline/output/units/` and asserts (currently: sheet+manifest exist; foreswing/backswing match `UnitCatalog.FullRoster`). It never invokes Python. So the distinctness seam is: Python writes a `roster_distinctness.json` report → C# reads it → asserts `passed`.
- **`FullRoster = FirstPlayable + RosterExpansion`** (`UnitCatalog.cs:256`). `run_roster_batch.py` processes only the **13 expansion units** (`BATCH`, lines 33–179). Distinctness gate is scoped to those 13 (see Decision 1).
- **The sameness is body-level, confirmed visually** (roster strip 4x). The 7 melee/ranged bipeds (ash_revenant, pyre_ogre, rime_sentry, boreal_colossus, bog_stalker, deepway_bulwark, spark_courier) share ONE rig: same blocky torso + round head + two stick legs, differing only by arm/weapon pose. `BIPED_CONFIGS["melee_biped"]` is a single config (`skeletons.py:426`). Props (sword/spear/shield) already differ per unit but are excluded from `body_size` (`generate_unit._body_bbox_size`, `prop_`-excluded) because the *body* is what is identical → the distinctness mask must also be **body-only** (Decision 3).
- **Shape-language must be per-UNIT, not per-typeclass.** colossus/ogre/revenant are all `melee_biped` (one archetype). Per-archetype shape-language alone cannot separate them. → add a per-unit `build` dimension to the biped rig (Decision 4).
- **T-pose root cause (confirmed in `props.py:157` + render).** `draw_crossbow` draws a LEVEL stock spanning `back = canvas[0]*14//32 = 21` px behind the hand + `front = 12` px past it = a 33px horizontal bar at chest height on the 48px canvas. Combined with `head_style="plain"` (no neck → floating head) and the flat `crossbow` arm pose (`ARM_POSES["crossbow"]`, all near-horizontal), it reads as a scarecrow T.
- **`make_silhouette_strip` already exists** (`contact_sheet.py:118`) — extracts a 1-bit mask from the sheet **alpha** (props included) for idle+move frame 0. Good for the visual review (step 4); NOT body-only, so the lint needs its own body-only mask (Decision 3).
- **No Python test harness exists** (`art_pipeline/**/test*.py` → none). TDD surface = the C# gate (design-named) + a lightweight synthetic-mask Python self-test for the pure math the roster can't exercise (Decision 5).

## Decisions (records)

1. **Distinctness gate scope = the 13 expansion units** (`run_roster_batch.BATCH`). This is what the batch owns and where the RED→GREEN is demonstrable. FP-unit differentiation (they share rigs too) is NOT required by Part B and is left as a follow-up (the `build` mechanism is general; only the spec assignments would be added). The existing FullRoster sheet/manifest/ticks case is unchanged.
2. **Backward-compatible `build` knob.** New per-unit `build` spec field; absent → a `"neutral"` default that reproduces today's `melee_biped`/`support_robed` rig **byte-identically**, so FP units (no `build`) and any non-reworked unit are untouched. Honors the byte-identity constraints in `skeletons.py` headers.
3. **Body-only black-fill mask is the distinctness instrument.** `generate_unit.py` renders an extra artifact `<name>.silhouette.png` (idle frame 0, **`prop_`-excluded**, opaque→black on transparent). The lint reads these for IoU + area. Body-only is design-faithful ("stop recoloring one biped rig" = the BODY must differ; props must not let a unit game the check). The existing alpha-based strip stays for visual review.
4. **Shape-language = a `build` enum reshaping the biped rig**, assigned per unit from the design vocabulary + unit semantics:
   | build | shape | silhouette levers | units |
   |---|---|---|---|
   | `agile` | circle | hunched/forward-lean, narrow round torso, round head, longer legs | bog_stalker, spark_courier |
   | `sturdy` | square | top-heavy block: broad shoulders/pauldrons, short thick legs, small low head | pyre_ogre, boreal_colossus, rime_sentry, deepway_bulwark |
   | `dangerous` | triangle | wedge head, shoulder/dorsal spikes, wide-shoulder→narrow-waist taper, upright | ash_revenant |
   Per-unit **proportion variation** (body W/H within the class floors) + **per-seed asymmetry** spread same-build units (4 `sturdy`) below the IoU threshold — e.g. boreal_colossus = max body, pyre_ogre = hunched-heavy, rime_sentry = tall upright + helmet crest, deepway_bulwark = squat + large shield. *This unit→build mapping is design-derivable, not a load-bearing taste fork; the contact-sheet review is the taste gate (valve c). It is surfaced for the user in the final report.*
   **Threading (plan_check fold):** `build` is **per-UNIT**, NOT a `BipedConfig` typeclass field (`BIPED_CONFIGS` has one entry per typeclass — a field there can't separate the 4 `sturdy` melee units). Mirror the flyer precedent: add `biped_config_from_spec(typeclass, spec)` (sibling to `flyer_config_from_spec`, `skeletons.py:1341`) that starts from `BIPED_CONFIGS[typeclass]` and overlays per-unit `build` / proportion / `seed` from the spec. `make_template(typeclass, spec)` already receives `spec` but discards it for bipeds (`skeletons.py:1366`) — route it through the new builder. Absent overlay keys → unmodified base config (byte-identical, Decision 2).
5. **TDD order (RED proven twice):**
   - **(a) Logic RED** — `art_pipeline/test_distinctness.py` (plain-`assert` script, no pytest dep, exit 0/1) builds *synthetic* masks and asserts `iou()` (identical→1, disjoint→0), the **≥3:1 dragon-area-gap** (the axis the roster can't exercise — no dragon unit), and the head-ratio band. Run → fails (functions absent).
   - **(b) Logic GREEN** — implement the functions in `lint.py`; self-test passes (math pinned, incl. dragon-gap).
   - **(c) Gate RED** — wire `run_roster_batch.py` to write `roster_distinctness.json`; extend `RosterArtContractTest` with the distinctness case. **Order (plan_check fold): generate the report over the CURRENT rigs FIRST, THEN run the gate** — so the RED is witnessed as a clean `passed=false` assertion, not a missing-file/parse exception masquerading as RED (`feedback_test_name_must_match_exercised_path`). High body IoU among the 7 melee → `passed=false` → C# RED. Proves "current near-identical bipeds fail."
   - **(d) Gate GREEN** — rework rigs + assign builds + fix T-pose; regenerate; report `passed=true` → C# GREEN.
6. **Per-seed asymmetry** (art-direction rule 10 "Break symmetry", currently unimplemented). New per-unit `seed` spec field drives small *deterministic* offsets (head tilt ±1, horn/spike side offset, one shoulder higher, stance lean). Deterministic = no RNG (sim/pipeline purity: identical spec → identical bytes). **plan_check fold — NEVER `hash(name)`:** Python's built-in `hash()` on `str` is per-process salted (`PYTHONHASHSEED`) → non-reproducible across runs, breaking the byte-identity contract and able to flip `roster_distinctness.json` on CI regen. Use an explicit per-unit `seed: int` in the spec (precedent: `backgrounds.py` passes explicit-int seeds), or a stable digest `int.from_bytes(hashlib.sha1(name.encode()).digest()[:4], "big")`. The asymmetry offsets are a fixed function of that integer.
7. **`props.py` `draw_crossbow` IS in scope for the T-pose fix** (a justified deviation from the brief's anticipated file list, which omits it). The horizontal cross-bar is intrinsic to the crossbow draw; the minimal correct fix shoulders the stock (small back-reach so the butt sits at the shoulder, muzzle forward → reads as a leveled rifle, not a chest-spanning bar) and the sniper rig gets a braced stance + real neck/head. Flagged prominently.

## Lint checks added (extend `lint.py`; thresholds = tuning constants, calibrated empirically)

- **Shared primitives (plan_check fold):** `silhouette_mask(image)` (alpha→1-bit) + `iou(a, b)` live in `lint.py` (single home for mask-extraction + math). `contact_sheet.make_silhouette_strip` calls the shared `silhouette_mask` instead of its inline `.point()` (lines 141-143). The **body-only** filter (a `prop_`-exclusion over `PixelBuffer.cells`) stays in `generate_unit` because it operates on cells, not pixels — the genuine reason for the second render source (Decision 3).
- **`silhouette_distinctness`** (NEW, cross-unit, in a new `roster_distinctness(units, thresholds)` — top-level sibling of `lint_sheet`, NOT a branch inside it): pairwise body-mask IoU over the 13, bottom-center aligned on a common (max W, max H) canvas. Fail if any pair IoU ≥ `IOU_MAX` (start 0.70; calibrate so current melee pairs ~0.9 FAIL and reworked < 0.70 PASS with margin; **threshold is set before the rig rework, never inflated to pass**). Report the full IoU matrix + offending pairs.
- **`scale_gap`** (NEW, in the same function): dragon-tier body area ÷ infantry-baseline body area ≥ 3.0. **Dragon-tier = typed signal ONLY: manifest typeclass `== "dragon"`** (plan_check fold — dropped the `canvas height ≥ 96` magnitude disjunct per `feedback_no_magnitude_as_type_discriminator`; a 96px siege/colossus would misclassify). No dragon unit in the 13 → report `skipped: "no dragon-tier unit present"` (the math is pinned by the synthetic self-test).
- **`head_to_body`** (extends `body_size`): emit `head_w`/`head_h` into manifest `body_size`, measured from the **idle frame 0** buffer via `PixelBuffer.part_bboxes()` over the **verified head part-names** `{"head", "face", "back_ear"}` (the biped `draw_pose` labels the snout/head/hood-face/ear cells with these — confirmed in `skeletons.py` `_draw_head`; NOT max-over-anims like width/height, so the band calibration is stable). Per-archetype band on `head_h / body_height`. Calibrated so the current floating-head `quarry_slinger` is out-of-band (RED contribution) and the fixed one + others are in-band. Skipped for `siege_machine` by **typed typeclass** (not a measured-mass threshold).
- **`passed` semantics (plan_check fold):** the report's `passed` = every **non-skipped** check passes. `skipped` checks (e.g. `scale_gap` with no dragon) are NEUTRAL — they must NOT hold the gate RED, or GREEN is unreachable after a perfect rework.

## New / changed files

| File | Action | Domain |
|---|---|---|
| `art_pipeline/lint.py` | add `silhouette_mask`, `iou`, `roster_distinctness` (IoU + scale-gap, `passed`=non-skipped pass), head-ratio band in `body_size`; new check constants | Logic |
| `art_pipeline/test_distinctness.py` | NEW — synthetic-mask self-test (RED-first for the pure math + dragon-gap) | Logic (test) |
| `art_pipeline/skeletons.py` | `biped_config_from_spec(typeclass, spec)` (mirrors `flyer_config_from_spec`); `build`-driven reshape in `BipedTemplate` (torso/head/legs/stance + spikes/pauldrons/hunch); per-seed asymmetry; sniper braced stance + neck/head (T-pose); route `make_template` biped path through the new builder | Gameplay (art) |
| `art_pipeline/props.py` | `draw_crossbow` — shoulder the stock (T-pose) | Gameplay (art) |
| `art_pipeline/generate_unit.py` | render `<name>.silhouette.png` (body-only idle mask, `prop_`-excluded over cells); `_head_bbox_size` → emit `head_w`/`head_h` in `body_size`; thread `build`/`seed` from spec | Logic/art |
| `art_pipeline/contact_sheet.py` | `make_silhouette_strip` calls shared `lint.silhouette_mask` (de-dup) | Gameplay (art) |
| `art_pipeline/run_roster_batch.py` | collect masks; call `lint.roster_distinctness`; write `roster_distinctness.json` to `output/reports/`; assign `build`/`seed` per unit spec | Gameplay (art) |
| `Tests/Logic/Campaign/RosterArtContractTest.cs` | NEW case `RosterSilhouettesAreDistinct`: read `output/reports/roster_distinctness.json` (add `ReportsDir()` helper — report is NOT in `output/units/`), assert `passed==true` AND offending-pairs empty | Logic (gate) |

## Closing steps (Definition of Done)

1. `art_pipeline/test_distinctness.py` passes (math + dragon-gap pinned).
2. `roster_distinctness.json` `passed=true`; new contact sheets + roster silhouette strip regenerated; the 7 melee bodies visibly distinct in the strip; quarry_slinger no longer a T.
3. Full `/regression_gate` GREEN (single-flight; stagger vs other sessions — shared GdUnit4 pipe). New test count accounted (the existing FullRoster case + 1 new distinctness case).
4. Categorical commits (feat: lint + gate; feat: rig shape-language + T-pose fix; chore: regenerated art artifacts). Submodule order N/A (no Jmodot change). Concurrent-session index hygiene per `gotcha_concurrent_session_shared_index_collision`. Do not push.
5. `/update_roadmap` Part → `complete` (diff surfaced in the final report).
6. Final report: build→unit mapping + the props.py deviation + thresholds, with the contact sheets, flagged for the user's subjective "wow/feel" verdict (valve c).

## Coordination / risks

- **Shared `lint.py` with the "External-Asset Conform Pipeline" Part** — do NOT run both in this window (brief). Single session here; noted.
- **Subjective visual quality** (does the shape-language *look* good) is valve (c): implement the mechanism + machine-gate distinctness + flag the contact sheets for playtest. Not a halt.
- **Iteration risk:** getting all 7 melee bodies below `IOU_MAX` is real art-iteration (render→strip→measure→tune). Budgeted as the GREEN loop; if a pair stubbornly exceeds threshold, add proportion/feature divergence, not threshold inflation.

### plan_check convergence — recorded guards (no plan-shape change)

- **Review without opening the Godot editor.** Visual review is via the Python `contact_sheet.py` PNGs + the silhouette strip over the battle bg — NOT a Godot-editor open. This sidesteps `gotcha_editor_reserialize_value_export_null_strip` (a resave trigger) entirely; this Part adds no C# `[Export]` so the direct trigger is also absent.
- **Verify outputs are tracked, not gitignored** (`gotcha_gitignore_build_glob_swallows_production_dirs`): `art_pipeline/output/` is committed (sheets already are); confirm the new `roster_distinctness.json` + `*.silhouette.png` land tracked.
- **gdunit narrowing** during TDD filters on the test-class FQN `~RosterArtContractTest.RosterSilhouettesAreDistinct` (`gotcha_gdunit4_filter_uses_test_class_name`), not a production name.
- **Baseline count:** the new case shifts the Logic test count by +1 — `/regression_gate` updates `Tests/regression_baseline.json` on the green run; the shift is exactly +1, accounted.
- **`build` closed-set guard:** the typed `build` enum is correct (each case = genuinely distinct levers, not `arch_rule_closed_set_switch`). GREEN-loop watch: if two builds converge to the same lever set, delete the redundant build rather than keep a duplicate case.

## Open questions (resolved at plan-time; none blocking)

- *Should distinctness cover FP units too?* — No (Decision 1); follow-up if desired.
- *IoU threshold exact value?* — Empirical; calibrated during GREEN so current FAILS / reworked PASSES with margin.
- *Build→unit mapping taste?* — Design-derivable (Decision 4); the contact-sheet review is the taste gate, surfaced in the final report.
