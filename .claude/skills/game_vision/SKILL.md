---
name: Game Vision
description: >-
  DraconicWars design bible — 2D lane auto-battler / tower-defense hybrid where rival
  Dragonlords field fantasy warbands in ~10-minute battles that crescendo into dragon
  warfare; Broker-parley pact drafting (TFT-inspired replayability, original identity) over Battle Cats-style lane combat, premium meta
  progression (no IAP). Use for design-loaded decisions: gameplay/scope/audience framing,
  what 'fits' the game, where a proposed feature lands on shipped/planned/design-pending.
  SKIP for mechanical refactors, bug fixes with known root cause, tooling/test work, or
  code-state lookups (use `project_subsystems`).
---

# DraconicWars — Game Vision

Deep source: vault `Claude/BrainstormingDesigns/2026-06-11-game-foundation/` —
`design.md` (battle systems), `design-meta.md` (meta/modes), `art-direction.md` (art spec).

## Elevator Pitch
A 2D lane auto-battler / tower-defense hybrid: rival Dragonlords field fantasy warbands
in ~10-minute battles. Battle Cats-style deploy-and-march lane combat meets Age of
War-style in-battle Ascension that climaxes in unleashing your dragon, with hard-pause
pact parleys making every battle a fresh build puzzle (draft-inspired, priced originals). Premium, no IAP; campaign +
local PvP; all opponents draw from the player's own unit pool (mirrored rosters are
diegetic — every enemy is a rival Dragonlord).

## Design Pillars (feature-acceptance tests)
1. **Every battle builds to a dragon.** The in-battle arc crescendos at Dragon Tier; the
   meta arc crescendos at collecting/awakening dragons. Features that don't serve the
   crescendo get cut.
2. **One legible verb-set, deep matchup math.** Player verbs are exactly: deploy units,
   build/swap Conduits, aim Dragon's Breath, seal pacts, fire the ultimate. Depth
   lives in stats/counters/timing — never unit micro.
3. **Randomness excites, never robs.** Every RNG surface is bounded, published in UI,
   and symmetric in PvP (shared pact tier paths, parity rules, pity).
4. **Grind is investment, never toll.** Level N+1 clearable with first-clear rewards of
   1..N; no energy, no wallclock mechanics, free 100% respecs, no IAP.

## Core Loop
- **Moment:** spend the mana drip — deploy a unit, build/upgrade/swap a Conduit, or bank
  — while manually aiming Dragon's Breath at aerial/clump threats.
- **Battle (~10 min, 3 acts):** Tier-I skirmish + economy investment → hard-pause
  pact parleys shape the build → Ascension thresholds unlock Tiers II/III → Dragon
  Tier + Summoning escrow decide it. Win = destroy the enemy Dragonspire (sudden death
  at 10:00, hard end 12:00 on spire HP%). Teaching levels L1-4 run compressed 3-4 min arcs.
- **Meta:** Gold levels units/conduit options; Dragon Sigils buy unlocks (units, dragon
  eggs, sockets); Dragon Rank raises caps; Trophies/Awakening/Heat-ladder bounties give
  long-horizon goals.

## Target Audience & Scope
Premium PC players who enjoy Battle Cats/Cartoon Wars-style lane battlers and TFT/
roguelite drafting. Solo-agent-built scope, v1: ONE campaign region (15 levels, 1 boss
dragon), ~22 units + 5 dragons, 5 elements (Fire/Frost/Storm/Venom/Stone), local PvP
only. **Non-goals:** online multiplayer, servers of any kind, gacha/IAP, 3D, mobile
ports, Shadow/Radiant elements (reserved).

## Systems at a Glance
| System | Status | One-liner |
|---|---|---|
| Deterministic battle sim (30 tps) | Shipped | Pure C# Logic-domain sim; frame-quantized combat (foreswing/backswing, KB counts, i-frames); seeded JmoRng |
| Units & type classes | Shipped (FP roster 11) | Melee/Ranged/Sniper/Aerial/Siege/Support; Tier I-III + Dragon; stat-shape-driven roles |
| Elements & synergies | Shipped (thresholds; counters pending) | 5 elements; per-unit counter abilities (Strong 1.5x / Massive 3x / Resistant 0.25x); fielded-type thresholds 2/4 with dragon capstones |
| In-battle economy | Shipped | 12 mana/s drip, 300 cap, kill bounties, final-2:00 double drip |
| Conduits | Shipped (6 types, 3 sockets) | 3→5 Dragonspire sockets, ONE per conduit type; 6 buildable/sellable structures; the resource-management identity |
| Ascension | Shipped | Trickle + lane-control meter (kills capped at 30%/threshold); Tier II/III/Dragon; +25% drip per threshold; dragons via visible Summoning escrow |
| Draconic Pacts (the Broker's Parley) | Shipped (core) | 1-of-3 hard-pause parleys (~2:00/5:00/8:00, per-level authorable); Ember/Drake/Wyrm tier paths published at battle start; Wyrm terms carry a Price (spire blood / drip tithe); reroll = mana-costed Tithe; lore on every card; PvP split blind draft |
| Dragon's Breath + Wrath | Shipped | Manually aimed elemental base attack (anti-air verb) + 60s comeback ultimate |
| Campaign (Cinderfell Marches) | Shipped (FP: 5 levels, scripted waves; personas core) | 15 levels → boss dragon Pyraxis; mirrored-pool AI personas (Rusher/Powerhouse/Streamer); magnification scaling |
| Local PvP | Design-Pending | KB+mouse vs gamepad; War Standard level clamp; shared augment tier path |
| Meta progression | Shipped (gold levels, rank, bonding; sigils pending) | Gold levels / Sigil unlocks; Dragon Rank caps; Trophies; Awakening; Heat ladder; RNG-control unlocks |
| Daily Skirmish | Design-Pending | Date-seeded, first-attempt-scores, local leaderboard |
| Art pipeline | Shipped (FP batch accepted) | 640x360 integer-scaled pixel art, Resurrect 64 palette, 6 parametric TypeClass templates (Python/PIL) + manifest-driven SpriteFrames import; sim owns combat timing |

**Release phases** (`design-meta.md` §14): **First Playable** = sim + 12 units + Elder
Drake/Pyraxis + 3 conduits + Ascension/escrow + Ember breath/Wrath + 5 scripted-wave
levels + gold levels. **v0.2** = pacts + local PvP + synergies. **v1.0** = full
roster/campaign/personas/trophies/Heat/skirmish. **Post-v1** = Awakening, Draconomicon,
ghosts, draft mode, possession, Confluence.

## Story / Theme Arc
The player is a Dragonlord reclaiming the Cinderfell Marches from rival Dragonlords.
Each region boss is a rival's dragon; defeating it bonds the dragon to the player
(beat the dragon = unlock the dragon). Before the first boss falls, Dragon Tier fields
a rental "Elder Drake" so the crescendo exists from battle one.

## Economy & Progression
Two currencies, hard split: **Gold** (every battle) buys levels on one shared x2.2 cost
table (rarity = entry offset L1/L3/L6/L9, equal ceiling — no dead commons); **Dragon
Sigils** (bosses, heat bounties, milestones) buy unlocks only. Dragon Rank accumulates
all spending into cap raises (5/8/10/13 at ranks 100/300/600/1000) and socket unlocks.
Trophies (region sets → permanent buffs, clamped off in War Standard PvP), Awakening
(level cap + boss relic → evolved dragon), Draconic Pact heat ladder (modifier-built
difficulty × per-element bounty matrix). Anti-dark-pattern charter in `design-meta.md` §12.
