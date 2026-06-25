# Plan — Part A: Per-Element Signature VFX Layer

**Roadmap:** `OneDrive/.../BrainstormingDesigns/2026-06-11-game-foundation/roadmap.md` — Part **Signature VFX Layer (per-element)**
**Design surface:** `art-pipeline-uplift.md` §Track 3 (sibling of the roadmap).
**Mode:** `/part_drive` one-shot (brief → plan → plan_check → TDD → gate → commits → roadmap-complete).

## Goal

Every unit carries an element + tier-scaled signature look so it reads as *alive and powerful*; dragons (top tier) get the loudest version and finally "pop". Additive — no roster regen, no sim change.

## Brief findings (verified against the codebase)

- **Hook:** `Game/Battle/UnitView.cs` → `Bind(SimUnit unit, SpriteFrames frames)` creates the `AnimatedSprite2D` (`_sprite`), sets the element tint via `_sprite.Modulate` (line 39-40), `AddChild(_sprite)`. Units parent under `UnitLayer` (Node2D) in `battle_scene.tscn`.
- **Data available at bind:** `unit.Def.Element` (`Element` enum: Fire/Frost/Storm/Venom/Stone), `unit.Def.Tier` (int), `unit.Def.NativeElement`.
- **Element palette anchors:** `Game/Battle/ElementColors.cs` (static `Dictionary<Element,Color>`, palette-anchored) — the precedent to mirror.
- **Raw Godot nodes, NOT Jmodot VFX:** zero `VisualEffectController`/`VisualComposer` usage in `Game/`; the shipped `BreathBeamView` uses raw nodes. Build on raw nodes.
- **DRIFT 1 (absorbed):** `viewport/hdr_2d=true` is set, but **no `WorldEnvironment`/glow pass exists anywhere** (scenes or code). The design's "shipped WorldEnvironment glow" is false. → Part A **adds** the `WorldEnvironment`+`Environment` glow pass to `battle_scene.tscn`. Bounded, high-leverage (all VFX bloom for free). Glow *tuning* → playtest flag (valve c).
- **DRIFT 2 (absorbed):** the shipped VFX (`BreathBeamView`) uses `Line2D`/`Polygon2D` ONLY — there are **no existing `.gdshader`, `GPUParticles2D`, `PointLight2D`, or `ShaderMaterial`** in the project. Part A introduces all of these for the first time (net-new VFX foundation, not "reuse shipped infra"). Bounded + additive; no light pool exists, so lights are gated to high tier (introduce sparingly) rather than drawn from a pool.

## Decisions

1. **Per-element `.tres` profile, convention-path load** (honors the design doc; avoids the typed-Dictionary export gotcha + a registry resource). `SignatureVfxProfile : Resource [GlobalClass, Tool]`, files at `assets/vfx/signature_{element}.tres`, loaded in `UnitView` via `GD.Load`. Missing file → log warning + skip (graceful).
2. **Tier scaling is pure + continuous** (`SignatureVfxResolver`, plain scalars — the strict-TDD piece). No "is-dragon" magnitude-discrimination (`feedback_no_magnitude_as_type_discriminator`): VFX intensity scales with `Tier`; the dragon, being top tier, is loudest automatically.
3. **Emissive via shader, NOT Modulate** (compose-not-stomp — `feedback_modulate_dual_tracking`): element tint stays on `_sprite.Modulate`; emissive bloom rides `_sprite.Material` (ShaderMaterial). Orthogonal lanes.
4. **PointLight2D gated by a tuning threshold** (`MinTierForLight`) to keep on-screen lights few (no light pool exists yet) — a budget decision, not type discrimination. Low tiers get emissive + aura only.
5. **Palette-contract-compliant:** emissive colors are palette picks (brightest 1-2 steps of the element ramp from `palette.json`), HDR-boosted only via the shader multiply (`VFX may exceed palette only via HDR multiplication`).

## New / changed files

| File | Action | Domain |
|---|---|---|
| `Game/Battle/Vfx/SignatureVfxResolver.cs` | NEW — pure scalar tier-scaling (`Resolve(tier, cfg) → ResolvedVfx`) | Logic (strict TDD) |
| `Game/Battle/Vfx/AuraKind.cs` | NEW — enum {Embers, FrostMotes, StormSparks, VenomSpores, StoneDust} | — |
| `Game/Battle/Vfx/SignatureVfxProfile.cs` | NEW — `Resource [GlobalClass, Tool]`: emissive `Color[]`, `AuraKind`, base aura density, light `Color`, base light energy, per-tier gain, min-tier-for-light | — |
| `assets/vfx/signature_{fire,frost,storm,venom,stone}.tres` | NEW — per-element data | — |
| `assets/shaders/emissive_bloom.gdshader` | NEW — canvas_item; uniforms `emissive_colors[]`, `emissive_count`, `boost`, `tolerance`; matched pixels ×boost (HDR) | Gameplay (playtest) |
| `Game/Battle/UnitView.cs` | EDIT — `Bind()` calls `ApplySignatureVfx(element, tier)`: shader material + GPUParticles2D aura + tier-gated PointLight2D, all children of UnitView | Gameplay (ISceneRunner) |
| `scenes/battle/battle_scene.tscn` | EDIT — add `WorldEnvironment` (glow Environment); bump `load_steps` | Gameplay (playtest) |

## TDD slices (RED → GREEN per slice)

1. **Resolver (Logic, strict).** `Tests/Logic/Battle/Vfx/SignatureVfxResolverTest`: higher tier → strictly higher lightEnergy/auraDensity/emissiveBoost; `spawnLight == (tier >= minTierForLight)`; tier-1 baseline sane. RED first (no resolver), then implement. Pure scalars — no `[RequireGodotRuntime]`.
2. **Profiles present (Sanity, `[RequireGodotRuntime]`).** Every `Element` resolves to a loadable `signature_{element}.tres` with non-empty emissive colors + a valid `AuraKind`. (Runtime — `.tres`/Color construction needs the engine.)
3. **UnitView integration (Integration, ISceneRunner `[RequireGodotRuntime]`).** Bind a synthetic `SimUnit` (Fire, dragon tier) → assert `_sprite.Material is ShaderMaterial`, a `GPUParticles2D` aura child exists, a `PointLight2D` exists for high tier (and is ABSENT for tier 1), and `_sprite.Modulate` is still the element tint (compose-not-stomp). Assert no sim mutation.
4. **Shader + glow (Gameplay, playtest).** `emissive_bloom.gdshader` + the `WorldEnvironment` glow. **Verify the exact Godot-4 2D-glow Environment params via the Godot docs before authoring** (`background_mode=Canvas`, `glow_enabled`, `glow_hdr_threshold≈1.0`, blend mode Softlight). Render the fire dragon over `cinderfell` bg with glow on (Rung-2 screenshot) → flag the *tuning* for the user.

## Halt valves live (part_drive)
- (a) factual-basis drift — glow-absence already resolved (absorb). Any further plan-referenced type missing → halt.
- (c) the glow tuning + emissive boost feel are subjective → implement mechanism, screenshot, flag for playtest, continue.
- (f) no load-bearing taste fork open (the .tres-vs-static choice resolved by precedent + design-doc fidelity).

## Parity ledger
N/A — additive feature. No deletions, no refactor, no behavior removed. (No transient parity capture needed.)

## Closing steps (Definition of Done)
1. Full `/regression_gate` (single-flight; stagger vs any parallel art session — shared GdUnit4 pipe).
2. Commits (categorical, by explicit path — concurrent-session index hygiene): `feat` for the VFX layer (resolver + profile + .tres + shader + UnitView), separate `feat`/`chore` for the `battle_scene.tscn` glow node if cleaner. No push.
3. `/update_roadmap mark complete "Signature VFX Layer (per-element)"`. Surface the diff. **MVP-3 membership stays the user's call** (not auto-added).
4. Report: the glow drift + its remediation, the playtest-flagged tuning, the commit list.
