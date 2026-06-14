# External-Asset Conform — Usage

`conform_external.py` projects a curated external sprite sheet onto the
DraconicWars contract (Resurrect-64 palette, feet-on-ground-line framing, a
tick-faithful manifest carrying `source: external` for the relaxed lint
profile). It is the companion to `generate_unit.py`: same output contract,
different input (external pixels instead of procedural templates).

## Drop-in workflow (real asset)
1. Download a CC0 pack (e.g. Luiz Melo Wizard Pack —
   https://luizmelo.itch.io/wizard-pack). Record its license in `CREDITS.md`
   and copy the verbatim text into `licenses/`.
2. Extract the sheets into `art_pipeline/external/sources/`.
3. Write a mapping JSON (full schema in the `conform_external.py` docstring).
   You decide: the source cell grid; which source rows map to
   idle / walk-or-fly / attack / death; material→ramp assignments (one ramp =
   monochrome conform; add a `hue` per material for nearest-hue multi-material
   segmentation); and `foreswing_ticks` / `backswing_ticks` (from the unit's
   catalog stats — the importer rejects manifest-vs-stat-sheet drift).
4. Conform:
   ```
   python art_pipeline/conform_external.py external/sources/<mapping>.json
   ```
   Output lands in `art_pipeline/output/external/`.
5. Verify:
   ```
   python art_pipeline/lint.py art_pipeline/output/external/<name>_sheet.png
   ```
   Expect `PASS [external]`. `no_aa` / `outline_1px` / `banding` may show as
   `[ww]` warnings — that is the relaxed profile working as designed (external
   art has no sel-out ink outline and legitimately uses gradients).

## Example mapping
```json
{
  "name": "luiz_wizard",
  "typeclass": "melee_biped",
  "element": "fire",
  "canvas": "32x32",
  "levels": 4,
  "materials": [{ "ramp": "fire" }],
  "source_sheet": "sources/wizard_idle.png",
  "source_grid": { "frame_w": 64, "frame_h": 64 },
  "rows": [
    { "animation": "idle",   "src_row": 0, "frames": 8, "fps": 7,  "loop": true },
    { "animation": "walk",   "src_row": 1, "frames": 8, "fps": 10, "loop": true },
    { "animation": "attack", "src_row": 2, "frames": 8, "fps": 12, "loop": false },
    { "animation": "death",  "src_row": 4, "frames": 7, "fps": 10, "loop": false }
  ],
  "foreswing_ticks": 8,
  "backswing_ticks": 12
}
```

## Notes
- **`element` is manifest metadata only** — conform picks colors via `materials`,
  not via the element→ramp mapping that `generate_unit.py` uses.
- **Downscaling caveat:** a large source (e.g. 64px → 32px) downscaled
  nearest-neighbor can drop thin features and create sub-3px color regions that
  trip the (still hard) `orphan_pixels` check. Tune `levels` and the source crop,
  or pick a `canvas` close to the source cell size.
- **Determinism:** identical source + mapping ⇒ identical output bytes
  (k-means uses a fixed init; no RNG). Pinned by `test_conform_external.py`.
- The conform pipeline + relaxed lint profile are proven end-to-end on a
  synthetic source in `test_conform_external.py` — no external binary needed for
  CI; the real-asset conform is the manual drop-in above.
