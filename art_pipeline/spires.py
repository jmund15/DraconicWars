"""spires.py -- the Dragonspire sheet (narrative centerpiece, art-direction §11).

One 96x160-frame sheet, 4 columns (Ascension tiers I-IV) x 3 rows (damage states:
pristine / cracked / last-stand). The same sheet serves both lane ends (the right
spire renders flip_h). Palette-contract compliant: every pixel is a palette.json
ramp lookup. Deterministic: fixed geometry, no RNG.

The spire reads the battle: ley veins brighten and the crown grows as the side
ascends; cracks, breaches, and embers accumulate as the spire HP falls.

CLI: python art_pipeline/spires.py
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image

from palette import get_palette

FRAME_W, FRAME_H = 96, 160
TIERS = 4
DAMAGE_STATES = 3
OUT_DIR = Path(__file__).resolve().parent / "output" / "spires"

CX = 48
GROUND = 156


def _px(img, x, y, color):
    if 0 <= x < FRAME_W and 0 <= y < FRAME_H:
        img.putpixel((int(x), int(y)), color)


def _rect(img, x0, y0, x1, y1, color):
    for y in range(int(y0), int(y1) + 1):
        for x in range(int(x0), int(x1) + 1):
            _px(img, x, y, color)


def _line(img, x0, y0, x1, y1, color):
    steps = max(abs(x1 - x0), abs(y1 - y0), 1)
    for i in range(int(steps) + 1):
        t = i / steps
        _px(img, round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t), color)


def _shaft_half_width(y):
    """Tapering tower silhouette: wide at the plinth, narrow at the crown."""
    t = (y - 24) / (132 - 24)
    return round(17 + 11 * t)


def _draw_frame(pal, tier: int, damage: int) -> Image.Image:
    img = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
    body = [pal.get("mauve_grey", i) for i in range(5)]
    stone = [pal.get("stone", i) for i in range(5)]
    ink = pal.get("ink", 0)
    vein = [pal.get("frost_teal", i) for i in range(5)]
    ember = pal.get("fire", 3)
    flame = [pal.get("fire", i) for i in range(5)]

    # Plinth: stepped base capping the leyline.
    _rect(img, CX - 34, 144, CX + 34, GROUND, stone[1])
    _rect(img, CX - 28, 136, CX + 28, 143, stone[2])
    _rect(img, CX - 34, 144, CX + 34, 146, stone[3])

    # Shaft with banded masonry.
    for y in range(24, 136):
        hw = _shaft_half_width(y)
        band = 2 if (y // 10) % 2 == 0 else 1
        for x in range(CX - hw, CX + hw + 1):
            edge = x <= CX - hw + 1 or x >= CX + hw - 1
            _px(img, x, y, body[0] if edge else body[band])
        if y % 10 == 0:
            for x in range(CX - hw + 2, CX + hw - 1, 3):
                _px(img, x, y, body[0])

    # Side buttresses.
    for sx in (-1, 1):
        _line(img, CX + sx * 30, 135, CX + sx * 16, 84, body[1])
        _line(img, CX + sx * 31, 135, CX + sx * 17, 84, body[0])

    # Gate at the base (ink arch with an ember keystone glow at higher tiers).
    _rect(img, CX - 6, 118, CX + 6, 135, ink)
    if tier >= 2:
        _px(img, CX, 117, vein[min(4, tier)])

    # Ley veins: one branch per tier, brighter steps as the spire drinks deeper.
    vein_paths = [
        [(CX - 10, 150), (CX - 12, 110), (CX - 8, 78), (CX - 10, 50)],
        [(CX + 11, 150), (CX + 13, 104), (CX + 9, 72), (CX + 11, 46)],
        [(CX - 3, 150), (CX - 5, 96), (CX - 1, 60), (CX - 3, 36)],
        [(CX + 4, 150), (CX + 6, 88), (CX + 2, 54), (CX + 4, 30)],
    ]
    glow = vein[min(4, 1 + tier)]
    for path in vein_paths[:tier]:
        for (x0, y0), (x1, y1) in zip(path, path[1:]):
            _line(img, x0, y0, x1, y1, glow)

    # Crown: grows with tier; tier IV blazes (the Toll can be paid).
    crown_y = 24
    hw_top = _shaft_half_width(24)
    _rect(img, CX - hw_top - 2, crown_y - 4, CX + hw_top + 2, crown_y, body[2])
    spike_count = {1: 2, 2: 4, 3: 6, 4: 6}[tier]
    spike_height = {1: 8, 2: 13, 3: 19, 4: 24}[tier]
    span = hw_top * 2
    for i in range(spike_count):
        sx = CX - hw_top + 3 + round(i * (span - 6) / max(1, spike_count - 1))
        center_boost = 5 if i in (2, 3) and spike_count >= 6 else 0
        tip = crown_y - 4 - spike_height - center_boost
        # Tapered horn: 3px base narrowing to the lit tip.
        for y in range(tip, crown_y - 3):
            t = (y - tip) / max(1, crown_y - 4 - tip)
            hw = 0 if t < 0.34 else 1
            _rect(img, sx - hw, y, sx + hw, y, body[3])
        _px(img, sx, tip, vein[min(4, tier)])
        _px(img, sx, tip + 1, vein[min(4, tier - 1) if tier > 1 else 1])
    if tier >= 4:
        # The crown fire: a layered blaze between the horns.
        for dx, h, idx in ((-6, 9, 2), (-3, 15, 3), (0, 21, 4), (3, 14, 3), (6, 8, 2)):
            for y in range(crown_y - 16 - h, crown_y - 14):
                t = (y - (crown_y - 16 - h)) / max(1, h)
                hw = 0 if t < 0.3 else 1
                _rect(img, CX + dx - hw, y, CX + dx + hw, y, flame[idx])
        _px(img, CX - 1, crown_y - 40, flame[4])
        _px(img, CX + 2, crown_y - 43, flame[3])

    # Damage state 1+: cracks crawl up from the plinth.
    if damage >= 1:
        for path in (
            [(CX - 20, 150), (CX - 16, 122), (CX - 22, 100), (CX - 17, 86)],
            [(CX + 18, 150), (CX + 22, 118), (CX + 16, 98)],
            [(CX - 2, 134), (CX + 3, 112), (CX - 1, 96)],
        ):
            for (x0, y0), (x1, y1) in zip(path, path[1:]):
                _line(img, x0, y0, x1, y1, ink)
        # Chipped silhouette notches.
        _rect(img, CX - _shaft_half_width(60) - 1, 58, CX - _shaft_half_width(60) + 2, 62,
              (0, 0, 0, 0))
        _rect(img, CX + _shaft_half_width(96) - 2, 94, CX + _shaft_half_width(96) + 1, 97,
              (0, 0, 0, 0))

    # Damage state 2 (last-stand): a breach, embers in the wounds, rubble below.
    if damage >= 2:
        for y in range(34, 58):
            hw = _shaft_half_width(y)
            bite = round((1 - abs(y - 46) / 12) * 9)
            _rect(img, CX + hw - bite, y, CX + hw, y, (0, 0, 0, 0))
        for x, y in ((CX + 10, 46), (CX + 13, 52), (CX - 19, 104), (CX + 19, 116),
                     (CX - 1, 110), (CX + 7, 40)):
            _px(img, x, y, ember)
        for x, y, w in ((CX - 30, 152, 5), (CX + 22, 153, 6), (CX - 8, 154, 4)):
            _rect(img, x, y, x + w, y + 1, body[1])
        for path in ([(CX + 8, 84), (CX + 4, 66), (CX + 9, 50)],
                     [(CX - 12, 70), (CX - 8, 52), (CX - 13, 40)]):
            for (x0, y0), (x1, y1) in zip(path, path[1:]):
                _line(img, x0, y0, x1, y1, ink)

    return img


def generate(out_dir: Path | None = None) -> Path:
    pal = get_palette()
    out_dir = out_dir or OUT_DIR
    out_dir.mkdir(parents=True, exist_ok=True)

    sheet = Image.new("RGBA", (FRAME_W * TIERS, FRAME_H * DAMAGE_STATES), (0, 0, 0, 0))
    for damage in range(DAMAGE_STATES):
        for tier in range(1, TIERS + 1):
            frame = _draw_frame(pal, tier, damage)
            sheet.paste(frame, ((tier - 1) * FRAME_W, damage * FRAME_H))

    sheet_path = out_dir / "spire_sheet.png"
    sheet.save(sheet_path)

    preview = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
    preview.save(out_dir / "_spire_preview_2x.png")
    return sheet_path


if __name__ == "__main__":
    print(f"spire sheet: {generate()}")
