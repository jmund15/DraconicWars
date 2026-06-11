"""Round-3 verifier helper: attack-row zooms + color-distribution audit."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(r"C:\Users\jmund\Game_Dev\Godot_Projects\DraconicWars\art_pipeline")
OUT = ROOT / "output" / "reports"
pal = json.loads((ROOT / "palette.json").read_text())
hex_to_ramp = {}
for ramp, steps in pal["ramps"].items():
    for i, hx in enumerate(steps):
        hex_to_ramp.setdefault(hx, f"{ramp}[{i}]")

def attack_zoom(unit, frame_w, frame_h, row=2, scale=8):
    img = Image.open(ROOT / "output" / "units" / f"{unit}_sheet.png").convert("RGBA")
    strip = img.crop((0, row * frame_h, img.width, (row + 1) * frame_h))
    big = strip.resize((strip.width * scale, strip.height * scale), Image.NEAREST)
    p = OUT / f"verify3_{unit}_attack_zoom{scale}x.png"
    big.save(p)
    print(f"saved {p.name}  ({big.width}x{big.height})")

def color_audit(unit, frame_w, frame_h, rows=4):
    img = Image.open(ROOT / "output" / "units" / f"{unit}_sheet.png").convert("RGBA")
    px = img.load()
    counts = {}
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 255:
                hx = f"{r:02x}{g:02x}{b:02x}"
                counts[hx] = counts.get(hx, 0) + 1
    total = sum(counts.values())
    print(f"--- {unit}: {total} opaque px ---")
    for hx, n in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"  {hx}  {hex_to_ramp.get(hx, '???'):16s} {n:5d}  {100*n/total:5.1f}%")

def frame_colors(unit, frame_w, frame_h, row, col):
    img = Image.open(ROOT / "output" / "units" / f"{unit}_sheet.png").convert("RGBA")
    px = img.load()
    counts = {}
    for y in range(row * frame_h, (row + 1) * frame_h):
        for x in range(col * frame_w, (col + 1) * frame_w):
            r, g, b, a = px[x, y]
            if a == 255:
                counts[f"{r:02x}{g:02x}{b:02x}"] = counts.get(f"{r:02x}{g:02x}{b:02x}", 0) + 1
    print(f"--- {unit} attack frame {col} ---")
    for hx, n in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"  {hx}  {hex_to_ramp.get(hx, '???'):16s} {n:4d}")

attack_zoom("forest_archer", 32, 32)
attack_zoom("dune_marksman", 48, 64, scale=6)
attack_zoom("stone_ram", 40, 32)
color_audit("stone_ram", 40, 32)
for c in range(4):
    frame_colors("forest_archer", 32, 32, 2, c)
