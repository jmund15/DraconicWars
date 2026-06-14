---
name: gotcha-itch-io-free-download-scriptable
description: itch.io free/CC0 asset downloads ARE scriptable with curl — a 4-hop CSRF flow, not a JS wall
metadata:
  type: reference
---

itch.io free (incl. CC0) downloads are fully scriptable from the CLI — no browser/JS needed. The flow, all carrying the page's `csrf_token`:

1. `GET https://<user>.itch.io/<slug>` → scrape `<meta name="csrf_token" value="...">` (and `cookies`).
2. `POST /<slug>/download_url` with `csrf_token` (form-encoded, `X-Requested-With: XMLHttpRequest`) → JSON `{"url": "<signed /download/{token} interstitial>"}` (JSON-escaped `\/` — strip backslashes).
3. `GET` that interstitial **as XHR** → the real download page (title "Download <name>") exposing the upload widget `id="upload_list_<dom_id>"` and the true `upload_id="<NNNNNNN>"` (NOT the widget DOM id — that returns `{"errors":["invalid upload"]}`).
4. `POST /<slug>/file/<upload_id>?source=game_download` with `csrf_token` → JSON `{"url": "<time-limited Cloudflare-R2 CDN link>"}`. `GET` it for the binary.

**Why this matters:** an earlier pass concluded "itch downloads are JS-gated, fetch manually" and deferred the real-asset proof. That was wrong — it failed only at hop 2 (never sent the CSRF on the `download_url` POST). Don't pre-declare a download unautomatable; trace the actual handshake first. Verify the license on the page at download time regardless (itch license text is creator-editable). Proven on luizmelo.itch.io/wizard-pack (CC0). Related: [[gotcha-synthetic-fixture-hides-real-input-failure]] (the conform this unblocked).
