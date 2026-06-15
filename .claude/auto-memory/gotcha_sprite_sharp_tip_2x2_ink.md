---
name: gotcha-sprite-sharp-tip-2x2-ink
description: A thin/acute sprite tip at or near the canvas edge yields a 2x2 outline-ink lint block; blunt the apex or clamp inside the canvas.
metadata:
  type: project
---

A thin triangle/spike with an acute apex — especially one reaching the canvas edge in a lunge frame — produces a 2×2 outline-ink block (`outline_1px` fail): the 1px outline can't fit beside/beyond the tip, so two ink pixels land adjacent.

**Why:** `finalize()` outlines the silhouette; a 1px apex at the boundary has no room for a single-pixel outline.

**How to apply:** blunt the apex (move the first scallop point outward so the tip triangle isn't a needle), OR clamp the tip x ≥2px inside the canvas. Clamp ONLY the offending tip — clamping several at once spawned new 2×2s elsewhere.

**Concrete:** elder_drake far-wing tip (blunt fix); boreal_colossus crystal shoulder-spike, lunge frame at the right edge (single-tip clamp). See [[gotcha-art-whitelist-hex-collision]].
