---
name: gotcha-art-whitelist-hex-collision
description: Procedural-art whitelist cap counts ALL pixels of the whitelisted hex; an element whose accent/glow resolves to a body color trips it.
metadata:
  type: project
---

The per-frame whitelist cap (flyer family: `accent`+`eye` ≤6px) counts EVERY pixel matching the whitelisted hex — not just deliberate detail. When an element/palette resolves a whitelisted role (`accent`/`eye`) OR a large glow role (`hot`) to the **same hex** as a major body fill (`skin`), the whole fill counts against the cap → `whitelist_cap` fails.

**Why:** the lint dedupes by resolved hex, not by draw intent — two roles sharing a hex are indistinguishable to it.

**How to apply:** before choosing an element for a wingless / glow-heavy body, check `pal.hex_of(*colors[role])` for skin/accent/hot collisions; pick a non-colliding element, or route the large fill through a non-whitelisted role (`belly`).

**Concrete:** venom `skin==accent` (1ebc73, wyrm body); frost `hot==accent` (8fd3ff, seraph core) — bit twice. Also in vault `creature-archetype-methodology` §5.1. See [[gotcha-sprite-sharp-tip-2x2-ink]].
