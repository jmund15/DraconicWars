# Creature Body-Plan Template Family

**Roadmap:** `Claude/BrainstormingDesigns/2026-06-11-game-foundation/roadmap.md` — Part **Creature Body-Plan Template Family** (NEW foundational Part; deps Art Pipeline Toolkit; feeds Hero A/B/C Bake-off → Extract Parameterized Methodology). Driven via `/part_drive`.

## Why (Brief)

24 units render from **3 body-plan templates** (`BipedTemplate` serves 4 humanoid typeclasses ≈17 units; `AerialFlyerTemplate` ≈6; `SiegeMachineTemplate` 2 — instantiated with NO spec, so `stone_ram` ≡ `plague_bell`). Part B's IoU lint made the 17 humanoids *proportionally* distinct (passing) but they remain one skeleton. User verdict (valve c): units must read as **different kinds of creatures**, not recolored humanoids. `design.md` defers form-assignment ("authored in a content design Part"); `art-pipeline-uplift.md` already diagnosed "a 'colossus','ogre','revenant','kobold' are the same body recolored." This Part builds the expandable form vocabulary the names always implied.

Architecture stance: **bespoke templates sharing one formal contract**, not a speculative generic morphology engine (extends the existing 3-sibling family; novel-morphology hand-tuning beats a graph engine at 32px). "Determined upfront, expanded as needed" = the formal `BodyPlanTemplate` contract + conformance test is the upfront deliverable; forms are added incrementally against it.

## The contract (formalize what's implicit)

`generate_unit` requires of every template: `canvas` (prop→(w,h)), `ROLE_DEFAULTS` (dict), `animations()`→[AnimDef(.name,.frames)], `poses(anim, contact_idx)`→[pose dict], `draw_pose(buf, pose, unit, pal)`. Shared infra (`PixelBuffer` primitives + `render`/`finalize`/`apply_*` pipeline) is form-agnostic — reused as-is.

## Form → unit mapping

| Form | Class strategy | Units | Slice |
|---|---|---|---|
| Humanoid (keep) | `BipedTemplate` (unchanged) | monks, archers, marksmen, chanters, courier, slinger, acolyte, mage, warden, spearman | — |
| **Ogre** | `OgreTemplate(BipedTemplate)` override | pyre_ogre | S1 |
| **Slime** | `SlimeTemplate` (new, novel morph) | plague_bell → plague-slime (re-theme, art-only) | S1 |
| **Golem/Construct** | `ConstructTemplate(BipedTemplate)` size/material-scaled | boreal_colossus, deepway_bulwark, rime_sentry | S2 |
| **Revenant** | `RevenantTemplate(BipedTemplate)` override | ash_revenant | S2 |
| **Spider** | `SpiderTemplate` (new, 8-leg novel morph) | bog_stalker | S3 |

Cheap reproportions ride free in any slice: cinder_acolyte → tall+slender; kobold_spearman → snout/tail; bird-vs-dragon flyer split (gale_harrier/storm_gryphon feathered vs membranous drakes).

## TDD (RED → GREEN; Python self-test + C# gate, Part B pattern)

1. **Contract-conformance test** (new `test_templates.py`): every `make_template(tc)` for each registered typeclass returns an object satisfying the `BodyPlanTemplate` protocol (all 5 members; `poses`/`animations` non-empty; `draw_pose` callable). **This carries the genuine RED** — new typeclasses (`ogre`,`slime`,…) unresolved → fail. GREEN: Protocol + dispatch.
2. **Cross-form distinctness** (extend `roster_distinctness` in `lint.py`): map typeclass→form-class; assert MIN cross-form-pair IoU < `CROSS_FORM_IOU_MAX`. **Threshold is calibrated from S1 observed data, NOT guessed** — render Ogre-vs-humanoid + Slime-vs-humanoid, read their actual cross-form IoU, set the constant in the gap below it (mirrors how IOU_MAX=0.77 was derived; disclose the arithmetic). Within-form per-unit distinctness keeps IOU_MAX=0.77. **Empty cross-form-pair set → skip-NEUTRAL** (like `scale_gap`'s no-dragon case — no empty-min crash). This is a calibrated *guard/ratchet* (catches a future "two forms too similar"), not a from-scratch RED. New check writes its own `cross_form_distinctness` key in `roster_distinctness.json`; top-level `passed` stays the aggregate.
   - **C# surfacing (critical, C2):** `RosterArtContractTest.RosterSilhouettesAreDistinct` currently enumerates only `silhouette_distinctness.offenders` — extend it to enumerate offenders from **every** failing check generically, so a cross-form failure is diagnosable (not a red `passed` with an empty offender list). Additive `.cs` edit → covered by `/regression_gate`.
3. **Parity capture** straddling the *actual* edited code (dispatch + any subclass additions in S1), not just the Protocol declaration: hash the full `<name>_sheet.png` **and `.manifest.json`** bytes for all 24 existing units to a scratch baseline; after S1's changes, assert byte-identical. **The silhouette mask is a lossy proxy** (alpha-thresholded — recolor/layering drift passes a mask diff) — hash full sheets. **Failure path:** any byte difference on an *existing* unit ⇒ the change was NOT additive ⇒ STOP and investigate (do not rebaseline). Delete scratch post-verify.

## Slices (ship + checkpoint per slice)

- **S1 — Foundation + 2 proofs:** `BodyPlanTemplate` Protocol (`typing.Protocol`, `@runtime_checkable`) + conformance test + full-sheet parity capture; cross-form lint (calibrated threshold) + C# gate offender-surfacing edit + test; `OgreTemplate(BipedTemplate)` (helper-override seam) + `SlimeTemplate` (novel-morph; `canvas=(40,32)` to match the plague_bell spec); remap pyre_ogre→`ogre`, plague_bell→`slime`; register both new typeclasses in `CLASS_BODY_RULES`/`HEAD_BANDS` (slime→`NO_HEAD_CLASSES`); `make_template` dispatch; regen those 2; gate; commit. **→ render contact sheets + silhouette strip; HALT for aesthetic read (valve c — last batch proved lint-green ≠ looks-right).**
- **S2 — Heavy cluster:** `ConstructTemplate` (3 units, size/material-scaled) + `RevenantTemplate`; reproportions (acolyte/kobold); regen; gate; commit; aesthetic read.
- **S3 — Spider:** `SpiderTemplate` (8-leg); regen bog_stalker; flyer bird/dragon split; gate; commit; aesthetic read.

Part is `complete` only when all forms ship. This drive targets **S1**; S2/S3 are same-Part follow-ons (Part stays active with progress noted) unless scope holds to continue.

## Decision records

- **DR1 — Slime = re-theme `plague_bell`, not a new unit.** Art-only (typeclass/template swap); binding unit catalog (stats/ticks) unchanged. New slime unit would pull in content-design scope → out.
- **DR2 — Bespoke templates over generic morphology engine.** Extends 3-sibling family; YAGNI on a body-part graph until form count is unmaintainable. (`feedback_inspect_existing_abstractions_first`.)
- **DR3 — Biped-variants subclass `BipedTemplate` via the helper-override seam.** `draw_pose` computes geometry inline (skeletons.py:566-594), but per-part helpers (`_draw_torso`/`_draw_head`/`_draw_legs`/`_draw_danger_features`) are cleanly overridable. "Zero risk to the 17 shipped humanoids" holds ONLY for the helper-override path: per-part reshape → override the helper; proportion change → drive via `BipedConfig.proportions`/`build` (already supported); layering/geometry change → explicit `draw_pose` override + parity check. Avoids a risky `HumanoidTemplate` extraction.
- **DR4 — New foundational Part, not folded into Part F.** Part F = universal cross-project extraction, gated on Hero bake-off, post-v1; this is its upstream input. (User-confirmed.)
- **DR5 — `BodyPlanTemplate` is `typing.Protocol`, not an ABC.** Structural + runtime-inert (`@runtime_checkable` for the conformance test, else hasattr duck-check). The 3 incumbents conform by duck-typing and need NOT list it as a base — *not* inheriting is the strictly-safer byte-identity path. An ABC would force a base on all incumbents (MRO/`__init_subclass__` change) — rejected.
- **DR6 — New typeclasses register in the lint's class maps.** `lint.py` keys body-floor + head-band checks by typeclass (`CLASS_BODY_RULES`/`HEAD_BANDS`/`NO_HEAD_CLASSES`); an unregistered typeclass *silently skips* those checks (lint.py:324, 482). Each slice adds its forms' entries so new shapes are gated, not ungated.
- **DR7 — Cross-form threshold calibrated from observed data.** Set in the gap below S1's measured Ogre/Slime-vs-humanoid IoU, disclosed — never a hardcoded guess.

## Plan-check folds (2026-06-14)

3-agent audit (memory-alignment / pattern-fit / test-readiness), all findings cited to `file:line` from first-party reads → round-2 re-dispatch skipped (evidence basis met). Verdict APPROVE WITH NOTES. Critical folds: C1 SlimeTemplate canvas=(40,32) (else `generate_unit:143` hard-raises); C2 C# gate enumerates all failing checks' offenders; C3 parity-capture hashes full sheets+manifests, not silhouette masks. Non-critical: DR5 Protocol-not-ABC, DR3 helper-override seam, DR6 lint-map registration, DR7 threshold calibration + empty-pairs neutral-skip.

## Closing steps (Definition of Done)

1. `/regression_gate` (full, single-flight) — green; baseline arithmetic disclosed if it moves.
2. Commits: `feat` (template family + lint + specs) + `chore` (regenerated art). Submodule N/A. Explicit-path staging; foreign staged-file check (`gotcha_concurrent_session_shared_index_collision`). No push.
3. `/update_roadmap` — ADD the new Part (deps Art Pipeline Toolkit; feeds Hero A/B/C Bake-off). State reflects S1-shipped vs full-Part-complete. Mermaid + derived views + revision log.
