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
| Source file | `Wizard Pack.zip` (68 kB) — **not yet downloaded** (see Status) |
| Download date | _pending manual download_ |
| Conformed DW asset | _pending — run `conform_external.py` once the source is dropped in (see `README.md`)_ |

**Status — binary download pending (manual step).** itch.io free downloads are
gated behind a JS download-session handshake a CLI cannot reliably complete: the
static page exposes the file name/size but withholds the signed CDN URL (a
download-session POST returns itch's generic error page). The **license above was
verified programmatically from the page**; the binary itself must be fetched by a
human. To finish: download `Wizard Pack.zip`, extract its sheets into `sources/`,
run the conform per `README.md`, then fill the *Source file* / *Download date* /
*Conformed DW asset* rows above.

## Adding a new external source
1. Verify the license **on the asset's page at download time**; copy the full
   license text verbatim into `licenses/<NAME>.txt`.
2. Add a ledger entry above (URL, artist, license, verified date, provenance,
   download date).
3. Author a conform mapping and run `conform_external.py` (see `README.md`).
4. Keep **one artist per coherence-group** (the cohesion-trap discipline — the
   Resurrect-64 conform pass is the unifying house treatment, not a license to
   free-mix artists).
