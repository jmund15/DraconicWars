---
name: gotcha-synthetic-fixture-hides-real-input-failure
description: A test fixture engineered to satisfy the checks can pass every stage yet hide a failure mode only real input triggers
metadata:
  type: feedback
---

A synthetic fixture built to *pass* the assertions exercises the happy path of every pipeline stage but can structurally exclude the failure mode that real input guarantees — so "proven end-to-end on synthetic" overstates coverage.

**Concrete case:** the external-art conform pipeline's synthetic source was clean 8px-tall solid color bands. It passed all 9 lint checks, "proving" the pipeline. But conforming a *real* CC0 sprite (Luiz Melo wizard, 231×190 downscaled to 32px) failed HARD `orphan_pixels` immediately — nearest-neighbor downscaling of detailed art always fragments thin features (staffs, glints, VFX motes) into sub-3px specks the clean bands never produced. The pipeline was missing a despeckle pass entirely; the fixture couldn't reveal it.

**Why:** the fixture was reverse-engineered from the checks, so it can't test what the checks-plus-real-data interaction surfaces.

**How to apply:** for any pipeline that transforms *external/arbitrary* input (downscale, parse, import, decode), the acceptance proof needs at least one *adversarial-by-nature* input — real data, or a fixture deliberately built to violate the invariant (specks, ragged alpha, off-grid frames), not one built to satisfy it. If real data isn't available in CI, encode its hostile shape into the fixture. Related: [[gotcha-itch-io-free-download-scriptable]] (how the real asset was obtained).
