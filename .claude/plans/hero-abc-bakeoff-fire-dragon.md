# Hero A/B/C Bake-off (fire dragon) — Plan

**Roadmap:** `C:\Users\jmund\OneDrive\Documents\ObsidianVault\DevProjects\DraconicWars\Claude\BrainstormingDesigns\2026-06-11-game-foundation\roadmap.md` — Part **Hero A/B/C Bake-off (fire dragon)** (plan-pending; deps Signature VFX Layer + External-Asset Conform Pipeline complete)

Design surface: `art-pipeline-uplift.md` §"The Hero A/B/C Experiment & Evaluation Rubric" (l.93-113), §"Dragon Differentiation Fix" (l.115-118), §"Acceptance & Review Method" (l.158-162).

---

## ⏸ EXECUTION RESULT — AWAITING YOUR VERDICT (recorded 2026-06-17)

**Status:** Slices A–D shipped + committed (3 commits, **NOT pushed**). Slice E halts here — the rubric verdict is yours (it IS the Part's DoD, reserved by art-direction §162). Roadmap still `plan-pending`; the `/update_roadmap` flip waits on your call.

### The three variants — all 96px, idle/fly/attack/death, lint-PASS, ticks 10/14
| | Variant | Sheet | In-context render (VFX on) |
|---|---|---|---|
| **A** | procedural-maxed pyraxis | `art_pipeline/output/units/pyraxis_sheet.png` | `art_pipeline/output/previews/pyraxis_preview.png` |
| **B** | hand-authored kit-bash | `art_pipeline/output/external/pyraxis_kitbash_sheet.png` | `.../previews/pyraxis_kitbash_preview.png` |
| **C** | curated-conform (Cethiel CC0) | `art_pipeline/output/external/pyraxis_conform_sheet.png` | `.../previews/pyraxis_conform_preview.png` |

**3-way in-context comparison image:** `art_pipeline/.review/bakeoff_incontext_3way.png`. All three rendered IN-ENGINE over the battle backdrop with the live signature_fire VFX (emissive bloom + ember aura + boss PointLight2D halo) via the new reusable harness `scenes/battle/unit_preview.tscn` + `Game/Battle/UnitPreviewController.cs`. Re-capture any time: `dotnet build`, then run that scene with `PREVIEW_DEF/DIR/SHEET/OUT` env vars (see the controller).

### Rubric (mechanizable axes filled; ⟨obs⟩ = Claude's observation, NOT the verdict)
| Criterion | A procedural | B kit-bash | C conform (CC0) |
|---|---|---|---|
| Palette cohesion (lint) | ✅ PASS (full) | ✅ PASS (ext) | ✅ PASS (ext) |
| License | 🟢 fully original | 🟢 original | 🟡 CC0 — clean but a recognizable shared asset |
| Production cost | 🟡 moderate (4 TDD gaps) | 🔴 high (parts+compositor, weak result) | 🟢 low (assemble+map+conform) |
| Silhouette ⟨obs⟩ | clear wyvern | crude blob | detailed, busy-but-readable |
| "Wow" ⟨obs⟩ | solid | low | **highest** (bloom → molten fire) |
| Liveness ⟨obs⟩ | **best** (hand-tuned squash/lag/breathe) | minimal | smooth (but DR6 scaling-pulse) |
| Uniqueness ⟨obs⟩ | **high** (bespoke) | high (original) | **low** (curated, "seen elsewhere") |

Headline: the bake-off reproduced the design's core tension — **C buys the most "wow" for the least effort but spends uniqueness**; the live VFX amplified both ends (bloom ignited C's detail, washed out B's flat fill).

### ❓ YOUR VERDICT — what to decide when back at your PC
1. **Which strategy wins the HERO tier?** (+ your scoring of the subjective axes above)
2. **Per-style guidance** — which sourcing strategy for which use (e.g. "procedural for roster, conform for boss spectacle, hand-author never")? This is the recorded output that feeds the [[GeneralGameDev]] methodology extraction (Part F).

### Next steps the verdict unblocks
- `/update_roadmap` flip Part → `complete` (deferred to post-verdict by design).
- **IF C (conform) wins a hero tier** → the gated **DR6 per-animation scaling-pulse fix** becomes in-scope. Site pinpointed: `conform_external.py` `_conform_cell`, per-frame `scale = th/sh` (each frame scales to its own bbox → attack frames pulse). Fix = one consistent scale per animation.
- **Unit Back-Light Shader** Part stays queued separately (arch-pending; the deferred rim).
- Raw Cethiel pack (132 MB) is git-excluded + re-fetchable from the CREDITS ledger URL; only the assembled source + conformed output are committed.

### Commits (banked, NOT pushed)
- `27b672b` feat(art-pipeline): three fire-dragon variants (A/B/C)
- `a69353d` feat(battle): in-context preview/capture harness + renders
- `0d9e9dc` docs(plan): this plan

---

## Decision Record

- **DR1 — Forks resolved (user, this session):** Full **A/B/C** (all three variants); **full-Godot in-context render with live Signature VFX** (rubric-faithful, line 112); strategy-C source = **Cethiel "Dragon — Fully Animated"** (CC0, OpenGameArt) — downloaded, ledgered.
- **DR2 — Premise reframe (VERIFIED first-party, skeletons.py:1584):** pyraxis renders through the dedicated `_draw_dragon` (1866-2048), NOT `_draw_scaled`. The design's "scaled aerial-flyer template" framing was stale. `_draw_dragon` already ships most Track-1 anatomy (two-mass torso, 3-seg S-neck, multi-panel fan wings + finger spars, multi-seg ember-tipped whip tail, dorsal spikes, lagged wing motion). Strategy A = **close 4 gaps**, not rebuild.
- **DR3 — Isolation (VERIFIED, run_fp_batch.py:168-169):** `crowned` head_style + `boss` flag are **pyraxis-exclusive** across all batches/demos. All strategy-A edits land on guarded crowned/boss seams + a new default-off `FlyerConfig.seed` → `elder_drake` and every other unit stay byte-identical. **No new body_plan / no `_draw_fire_boss` fork** (extend the existing path through guards).
- **DR4 — Binding gate is silhouette IoU < 0.77 vs elder_drake** (lint.py roster_distinctness; C# RosterArtContractTest reads the JSON verdict). The 3:1 `scale_gap` is DORMANT (pyraxis manifest typeclass = `aerial_flyer`, not `dragon`). So strategy A diverges the *shape* (real horns, accent, asymmetry), not just scale. The wingspan HARD floor at 96px is **72px; pyraxis sits at 74 (2px margin)** — any wing change must hold tip-to-snout ≥72.
- **DR5 — Frame budget:** BOSS_AERIAL_ANIMS = 22 (idle4/fly6/attack6/death6); design target 26-30. Bump is a **double source of truth** (BOSS_AERIAL_ANIMS l.454-455 AND boss branches of `poses()` l.1537-1572). Target **27** (idle 4→6, attack 6→7 [the 7th key `settle2` already exists], death 6→8) — keep foreswing/backswing **ticks 10/14 unchanged** (UnitSpriteLibrary timing gate is `==`, no tolerance) and verify `contact_frame ∈ [0,frames)`.
- **DR6 — Conform (strategy C):** `conform_external.py` needs a **single gridded source sheet**; Cethiel ships separate per-anim frames → a deterministic **assembler pre-step** (subsample ~6 frames/folder → uniform cell → one-anim-per-row). The known per-animation **scaling pulse** lives at `_conform_cell` (`scale = th/sh`, per-frame bbox; l.281-286). Per the GATING clause it is **noted as an artifact for scoring, fixed only if conform wins a hero tier**.
- **DR7 — Phase 4 is from-scratch (VERIFIED):** NO preview/review scene or screenshot code exists. Must author `dragon_preview.tscn` (WorldEnvironment glow + cinderfell bg + Camera2D + UnitLayer + controller) + a capture (`GetViewport().GetTexture().GetImage().SavePng` after `frame_post_draw`, **real driver** — never `--headless`). Wiring: `UnitSpriteLibrary.Load(def, sheetName)` → `UnitView.Bind` → `ApplySignatureVfx(element, tier)`. **pyraxis.Tier must be ≥3** for the boss PointLight2D halo. Emissive accents in **#F79617 bloom for free** (== signature_fire EmissiveColors[0] == manifest whitelist accent).
- **DR8 — DoD is a user verdict:** the Part's Definition-of-Done is the rubric scoring + recommendation, which the design reserves for the user (line 162). This drive produces ALL artifacts (3 variants + in-context renders + mechanizable rubric data) and **HALTS (valve c/f) for the verdict**. The `/update_roadmap` flip to `complete` is **deferred to post-verdict** — it cannot happen this session.

---

## Scope boundary (this Part vs siblings)

- **IN:** 3 fire-dragon variants (procedural-max / hand-kit-bash / curated-conform) under one house treatment; in-context Godot render with live VFX; mechanizable rubric data; verdict HALT.
- **OUT (sibling Parts):** Unit Back-Light Shader (the deferred rim — separate arch-pending Part); Extract Parameterized Methodology (Part F, idea-pending downstream); the conform per-animation scaling fix UNLESS strategy C wins a tier (GATING, then cross-ref Conform DR6).

No file deletions → no parity ledger needed.

---

## Plan-check resolution (3 lenses, verdict APPROVE WITH NOTES)

No critical findings (no Logic change without a test-first). Verified-CLEAN spine: finalize ordering, the `==` manifest-timing gate (ticks come from spec, A4 frame bump is safe), `crowned`+`boss` pyraxis-isolation, real-driver capture. Folded refinements (above): A1 sharp-tip/2×2-ink guard + test access; A2 hex-collision check + presence-asserting RED; A3 `seed=0`/`_asym` reuse + broadened byte-identity; A4 boss-template test access; A1/A3/A4 `main()`-tuple registration; B1 conform-helper reuse; D2 private-`ApplySignatureVfx` simplification; D1 reusable `unit_preview.tscn`; D3 script-only capture. One execution-time verification carried forward: confirm `#F79617` is the `accent` hex and not the `hot`/fill hex (A2).

---

## Slices

### Slice A — Strategy A: procedural-maxed pyraxis (Dragon Differentiation Fix)
Pure-Python pipeline (`skeletons.py`, `run_fp_batch.py`). Logic-domain pure functions get a failing test FIRST; sprite output judged via the render→review loop.

- **A1 — Real backward-swept horns.** Rewrite the crowned branch (skeletons.py:2009-2016, pyraxis-isolated) into a SYMMETRIC backswept paired horn: drop the up-forward `horn3` tine; both horns rake AWAY from the snout (apex `|dx| > |dy|`, raking back over the neck), each a thick base + curled tip as ≥3px outlined clusters (mauve_grey horn role). **Keep horn tips ≥2px inside the canvas + blunt acute apexes** — an acute tip near the edge mints a 2×2 outline-ink block (`gotcha_sprite_sharp_tip_2x2_ink`, fails outline_1px); compounds with the 74px wingspan (2px margin, DR4). RED (`test_templates.py`): a geometry test asserting the horn apex vector is backswept (`|dx| > |dy|`, dx negative). **Test access:** reach the crowned branch via a `make_template` spec carrying `flyer:{head:'crowned',boss:True,dragon:True}`, then filter rendered cells by `part='horn'/'horn2'` to compute the apex vector. GREEN: the rewrite **+ append the new test fn to the `test_templates.py` `main()` tuple** (plain-assert script — no auto-discovery; an unregistered test silently never runs). Visual review.
- **A2 — Palette-locked identity accent.** **FIRST verify #F79617 is collision-free** — confirm it is the `accent` hex and NOT also the `hot`/fill hex (whitelist_cap counts by HEX, so if `hot`==accent the large crest/maw/tail-flame pixels all count and blow the cap; `gotcha_art_whitelist_hex_collision`). If it collides, route the accent through a distinct palette index. Then stamp a `no_outline` ember accent (#F79617 = the emissive-bloom color) at the brow/horn-base, gated on crowned/boss, ≤2px. RED (true fail-first, lands in `test_lint.py`, unittest → auto-discovered): assert a rendered crowned/boss frame has **≥1px of the accent hex at the brow/horn-base AND** whitelist_cap (eye+accent) ≤6/frame — so it fails BEFORE the stamp exists. GREEN: the stamp. Blooms in-engine (Slice D payoff).
- **A3 — Per-seed asymmetry.** Add `FlyerConfig.seed: int = 0` (**match `BipedConfig.seed`: 0 = off-sentinel**, NOT -1); read it in `flyer_config_from_spec` (skeletons.py:2581-2598); gate `seed != 0` and **reuse the existing `_asym(seed)` helper** (skeletons.py:~600) for the deterministic offset — do NOT fork a bespoke jitter (`feedback_inspect_existing_abstractions_first`). Apply to ONE element (horn-tip / head tilt / one dorsal-spike height). Unit RED (`test_templates.py`, **append fn to `main()` tuple**): two distinct seeds render geometrically different. **Byte-identity** (the "unchanged" half) is the A-exit md5 sweep, NOT an in-test baseline — and `flyer_config_from_spec` is a SHARED read path for all 6 flyers, so the A-exit assert must cover **elder_drake + the other 4 flyers**, not just pyraxis.
- **A4 — Frame budget 22→27.** Bump BOSS_AERIAL_ANIMS (l.454-455, pyraxis-exclusive) idle 4→6 / attack 6→7 / death 6→8, with matching boss-branch pose data in `poses()` (idle +2 dicts l.1540-41; attack inline 6→7 reusing `settle2`; death +2 dicts l.1559-66). RED (`test_templates.py`, **append fn to `main()` tuple**): build a boss template (`make_template` spec `flyer:{boss:True}`), assert `animations()` total ∈ [26,30]; `contact_frame ∈ [0,attack_frames)`; foreswing/backswing ticks unchanged. GREEN: the bumps. (Timing gate verified safe — ticks come from the spec not the frame count; `contact_frame` auto-clamps; the AnimDef↔`poses()` desync guard at generate_unit catches a mismatch.)
- **A-exit:** regenerate pyraxis (`run_fp_batch.py`), lint PASS, distinctness IoU < 0.77 vs elder_drake (confirm headroom in roster_distinctness.json), **byte-identity for every non-pyraxis sheet** (md5 diff). Rung 1/2 visual review (filmstrip + over battle bg).

### Slice B — Strategy B: hand-authored kit-bash fire dragon
- **B1 — Compositing path.** Resolve from code: reuse `conform_external`'s palettize+anchor on hand-authored part PNGs, OR a small new `kitbash_assemble.py` (place hand-authored parts at large canvas → palettize via ramp-map → animate via per-frame part transforms → emit manifest). **If a new module, it MUST import conform's helpers (`_material_for`/`_level_to_index`/`_denoise_orphans`/`_conform_cell`) and the shared manifest shape — do NOT add a third manifest emitter** (conform already mirrors generate_unit's; `feedback_inspect_existing_abstractions_first`). Decide at B1 (read conform internals already mapped).
- **B2 — Author the parts.** Pixel-author dragon parts (head/body/wing/tail/legs) at ~96px per the sprite_authoring skill (decompose → grid coords → render→review per part). Quality is the experiment's stated unknown — **if it can't reach the prototype bar, that IS a rubric data point** (record production-cost/quality honestly; don't force it).
- **B-exit:** a kit-bash fire-dragon sheet + manifest; lint (full or external-relaxed as appropriate); Rung 1/2 review.

### Slice C — Strategy C: conform the Cethiel dragon
- **C1 — Frame assembler.** `assemble_external_frames.py` (or inline): subsample ~6 evenly-spaced frames per chosen folder (Idle→idle, Walking→walk, Attack 1→attack, Death→death), normalize to a uniform cell (= max selected-frame bbox), pack one-animation-per-row → `cethiel_dragon_source.png`. Deterministic. RED: a small test pinning frame-count/grid-alignment of the assembled sheet.
- **C2 — Mapping.** `cethiel_dragon.mapping.json`: materials = fire (primary) + a neutral (mauve_grey horns/claws) + hot accent; `levels` ~5; `canvas` 96x96; `body_fill` ~0.8; `airborne: false` (Cethiel walks — grounded). typeclass chosen so body_size floors fit a 96px boss.
- **C3 — Conform run.** `conform_external.py` → conformed sheet + manifest (`source: external`). Note the DR6 scaling pulse as a documented artifact (no fix unless C wins). Commit-scope: raw 132MB pack NOT committed; only the subsampled frames + assembled sheet + mapping + conformed output.
- **C-exit:** external-relaxed lint PASS; Rung 1/2 review.

### Slice D — Phase 4: in-context Godot render harness (live VFX)
Gameplay-domain integration — deterministic capture where possible (ISceneRunner / scripted), the visual itself flagged for the user.
- **D1 — `unit_preview.tscn` (REUSABLE, parameterized by sheet+element+tier — NOT creature-scoped; the sprite_authoring skill prescribes one review scene per project, so don't seed a `faerie_preview.tscn` next):** WorldEnvironment(Environment_glow, mirroring battle_scene l.14-19,33-34) + the cinderfell parallax bg + Camera2D + a `UnitLayer` Node2D + a preview controller node. (A new lightweight scene IS justified — battle_scene drags in BattleRunner/HUD/full sim.)
- **D2 — Preview controller (C#):** load a named sheet via `UnitSpriteLibrary.Load(def, sheetName)` → `new UnitView()` → `Bind(unit, frames)`. **`ApplySignatureVfx` is PRIVATE and auto-invoked inside `Bind` — do NOT call it externally.** Just bind a `SimUnit` whose `Def` carries Element=Fire & **Tier≥3** and the emissive shader + ember aura + boss PointLight2D halo come FREE from `Bind`. Instantiate a standalone pyraxis `SimUnit` OR drive a Deploy `SimCommand` until it spawns (verify `Sim/Units` constructor visibility). **Confirm pyraxis.Tier ≥ 3** (UnitCatalog).
- **D3 — Capture (SCRIPT only):** `await RenderingServer.frame_post_draw` → `GetViewport().GetTexture().GetImage().SavePng`. **Do NOT save via the editor** — the A-exit byte-identity md5 covers PNG sheets only; an editor resave can silently null-strip `.tres` value-type exports (`gotcha_editor_reserialize_value_export_null_strip`). Real driver (local d3d12; cloud `xvfb-run --auto-servernum … --rendering-driver opengl3`). One idle + one fly/attack capture per variant, all over the real battle bg with VFX on.
- **D-exit:** 3 variants rendered side-by-side in-context (idle + motion frame), VFX + glow visible.

### Slice E — Phase 5: rubric + HALT for verdict
- **E1 — Mechanizable axes (Claude fills):** palette cohesion (lint PASS/FAIL per variant), license cleanliness (procedural=none / CC0 / etc.), production cost (effort per variant), silhouette readability proxy (IoU/distinctness).
- **E2 — HALT (valve c/f):** present the 3 in-context renders + the rubric table with mechanizable axes filled; the subjective axes (wow/charisma, uniqueness) + the final verdict/recommendation are **user-owned** (design line 162). Record the verdict when the user renders it; it feeds the Extract Methodology Part.

---

## Closing steps (Definition of Done)

1. **`/regression_gate`** (MANDATORY — Slice D touches `.cs`). Pure-Python pipeline changes (A/B/C) are covered by the Python suites (`test_lint.py`, `test_templates.py`, `test_distinctness.py`, `test_conform_external.py`) + the C# `RosterArtContractTest`.
2. **Commits (categorical, do NOT push):** feat(art-pipeline) strategy-A pyraxis Dragon Differentiation Fix; feat(art-pipeline) external frame assembler + cethiel conform; feat(art-pipeline) hand-kit-bash dragon; feat(battle) Godot dragon preview/capture harness; chore(external) Cethiel CC0 asset + ledger. Concurrent-session index hygiene: stage by explicit path, verify the committed list.
3. **`/update_roadmap` Part → `complete`: DEFERRED to post-verdict** (DR8 — the DoD is the user's verdict; this session HALTS at Slice E). Surface the pending flip in the final report.

---

## Open questions

- **Q-typeclass:** make the 3:1 `scale_gap` gate-enforced by setting pyraxis manifest typeclass=`dragon`? Currently dormant (visually honored at 96px). Deferred — needs FORM_CLASS/HEAD_BANDS/CLASS_BODY_RULES `dragon` entries or other per-typeclass lints silently skip, and risks other units. Flag, don't block strategy A.
- **Q-frames:** exact target 27 vs 29 (add fly 6→8)? Default 27; revisit if fly motion reads thin at the bake-off.
- **Q-kitbash:** reuse conform palettize vs a new assembler (resolve at B1 from code).
- **Q-driver:** local d3d12 vs cloud xvfb+opengl3 — bloom parity for the screenshots (default local).
