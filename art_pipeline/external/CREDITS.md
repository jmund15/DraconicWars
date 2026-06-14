# External Asset Credits & License Ledger

Checked-in provenance + license record for every external (curated CC0) sprite
sheet conformed into the DraconicWars pipeline via `conform_external.py`. Per the
art-pipeline-uplift Track-2 license-hygiene rule, every source is recorded here
**even when CC0** — itch.io license text is creator-editable and can change, so a
copy is pinned at download time. Full license texts are stored verbatim under
`licenses/`.

## Ledger

### Luiz Melo — Wizard Pack
| Field | Value |
|---|---|
| Source URL | https://luizmelo.itch.io/wizard-pack |
| Artist | Luiz Melo (https://luizmelo.itch.io) |
| License | Creative Commons Zero v1.0 Universal (CC0 1.0) — verbatim copy: `licenses/CC0-1.0.txt` |
| License verified | 2026-06-13 — page states *"Asset license: Creative Commons Zero v1.0 Universal"* |
| Provenance | page tagged *"No generative AI was used"* |
| Source file | `Wizard Pack.zip` (69,998 bytes) — `sources/luiz_wizard/` (8 anim strips, 231×190 frames) |
| Download date | 2026-06-13 |
| Conformed DW asset | `output/external/luiz_wizard_sheet.png` + `luiz_wizard.manifest.json` — `PASS [external]` (idle/walk/attack/death, `shadow`/`rose`/`peach`/`radiant`/`frost_teal` ramps) |

**Status — downloaded + conformed.** The binary was fetched non-interactively
with curl; itch.io free downloads are scriptable (no human-in-the-loop needed):

1. GET the asset page → scrape its `csrf_token`.
2. `POST /<game>/download_url` with the token → JSON `{url}` (a signed
   `/download/{token}` interstitial).
3. GET that interstitial as XHR → real download page exposing `upload_id`.
4. `POST /<game>/file/{upload_id}?source=game_download` with the token → JSON
   `{url}` (a time-limited Cloudflare-R2 CDN link). GET it for the zip.

The earlier "JS-gated, fetch manually" note was wrong — it failed only because it
never sent the CSRF token on the `download_url` POST. The license was *also*
verified programmatically from the page (CC0 1.0, "No generative AI was used").

## Adding a new external source
1. Verify the license **on the asset's page at download time**; copy the full
   license text verbatim into `licenses/<NAME>.txt`.
2. Add a ledger entry above (URL, artist, license, verified date, provenance,
   download date).
3. Author a conform mapping and run `conform_external.py` (see `README.md`).
4. Keep **one artist per coherence-group** (the cohesion-trap discipline — the
   Resurrect-64 conform pass is the unifying house treatment, not a license to
   free-mix artists).
