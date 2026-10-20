"""Deterministic derivatives of the approved artwork. Requires Pillow 12.3 / NumPy 2.3.
No drawing/reconstruction of the icon: only connected exterior removal, crop and resize.
Run from any directory: python build/CreateBrandingAssets.py [--preview PATH].
"""
from collections import deque
from hashlib import sha256
from pathlib import Path
import argparse
import io
import math
import struct
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/branding/workbench-icon-source.png"
APPROVED_SHA256 = "A47EFCAD4AC2C7E19CB296A280DFA02B2AABFB11DFE07DCFF9EB4951B3651095"
SIZES = (16, 24, 32, 48, 64, 128, 256)


def prepare():
    source_bytes = SOURCE.read_bytes()
    if sha256(source_bytes).hexdigest().upper() != APPROVED_SHA256:
        raise ValueError("Artwork differs from the approved source; review before regenerating.")
    original = Image.open(io.BytesIO(source_bytes)).convert("RGBA")
    pixels = np.array(original)
    height, width = pixels.shape[:2]
    # The closed blue/cyan outline separates the dark exterior from the equally
    # dark interior. Flood only edge-connected backdrop pixels; never color-key
    # the whole image, which would erase the approved navy interior.
    backdrop = (pixels[:, :, 1] < 145) & (pixels[:, :, 2] < 120)
    exterior = np.zeros((height, width), dtype=bool)
    pending = deque([(x, 0) for x in range(width)] + [(x, height - 1) for x in range(width)] +
                    [(0, y) for y in range(height)] + [(width - 1, y) for y in range(height)])
    while pending:
        x, y = pending.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or exterior[y, x] or not backdrop[y, x]:
            continue
        exterior[y, x] = True
        pending.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
    assert not exterior[height // 2, width // 2], "Outline was not closed; interior must remain intact."
    pixels[exterior, 3] = 0
    cutout = Image.fromarray(pixels)
    box = cutout.getbbox()
    assert box is not None
    # Square crop around actual shape, with 1.5% padding per side. Its dimensions
    # are never independently scaled, so the original geometry is not stretched.
    extent = max(box[2] - box[0], box[3] - box[1])
    side = math.ceil(extent * 1.03)
    left = math.floor((box[0] + box[2] - side) / 2)
    top = math.floor((box[1] + box[3] - side) / 2)
    crop = (left, top, left + side, top + side)
    assert 0 <= left and 0 <= top and crop[2] <= width and crop[3] <= height
    assert left <= box[0] and top <= box[1] and crop[2] >= box[2] and crop[3] >= box[3]
    master = cutout.crop(crop).resize((512, 512), Image.Resampling.LANCZOS)
    assert master.getpixel((0, 0))[3] == 0 and master.getpixel((256, 256))[3] == 255
    return master, crop, box


def ico_bytes(frames):
    # Explicit PNG-backed ICO entries ensure every requested size is retained;
    # no platform-dependent ICO encoder or frame selection is involved.
    payloads = []
    for size in SIZES:
        buffer = io.BytesIO()
        frames[size].save(buffer, format="PNG", optimize=True, compress_level=9)
        payloads.append(buffer.getvalue())
    result = bytearray(struct.pack("<HHH", 0, 1, len(SIZES)))
    offset = 6 + 16 * len(SIZES)
    for size, payload in zip(SIZES, payloads):
        result += struct.pack("<BBBBHHII", size if size < 256 else 0, size if size < 256 else 0, 0, 0, 1, 32, len(payload), offset)
        offset += len(payload)
    return bytes(result) + b"".join(payloads)


def small_variant(master, size):
    """Authorized small-size derivative, using the approved monogram pixels.
    Tight framing, stronger silhouette, simplified bars; larger frames are untouched.
    """
    # The small navy field uses an interior source color. Keep the original curved
    # outline and its cyan/blue palette; remove the old interior before enlarging.
    canvas = master.crop((8, 8, 504, 504)).resize((512, 512), Image.Resampling.LANCZOS)
    ImageDraw.Draw(canvas).rounded_rectangle((6, 6, 505, 505), radius=87, fill=(3, 19, 42, 255))
    foreground = np.array(master)
    color = ((foreground[:, :, 2] > 130) & (foreground[:, :, 1] > 50)) | ((foreground[:, :, 0] > 150) & (foreground[:, :, 1] > 60))
    foreground[:, :, 3] = np.where(color, foreground[:, :, 3], 0)
    # At 16/24 px the blue bars compete with the monogram. Retain the recognizable
    # orange message accent and the full letter strokes, omitting only those bars.
    if size <= 24:
        foreground[190:231, 320:451, 3] = 0
        foreground[277:315, 320:451, 3] = 0
    layer = Image.fromarray(foreground).crop((95, 80, 447, 432)).resize((456, 456), Image.Resampling.LANCZOS)
    canvas.alpha_composite(layer, (28, 28))
    reduced = np.array(canvas.resize((size, size), Image.Resampling.LANCZOS))
    # Lanczos can ring with 1–3/255 alpha beyond the curved outline. Remove only
    # these effectively invisible exterior pixels, preventing a faint corner halo.
    reduced[reduced[:, :, 3] <= 3] = 0
    return Image.fromarray(reduced)


def preview(frames, destination):
    widths = (80, 80, 80, 96, 112, 160, 288)
    sheet = Image.new("RGB", (sum(widths) + 32, 640))
    draw = ImageDraw.Draw(sheet)
    for row, background in enumerate(("#f4f5f7", "#15191f")):
        y = row * 320
        draw.rectangle((0, y, sheet.width, y + 320), fill=background)
        x = 16
        for size, cell in zip(SIZES, widths):
            color = "#18202a" if row == 0 else "#e8eef5"
            draw.text((x + 4, y + 8), f"{size} x {size}", fill=color)
            sheet.paste(frames[size], (x + (cell - size) // 2, y + 32), frames[size])
            if size <= 32:
                zoom = frames[size].resize((size * 2, size * 2), Image.Resampling.NEAREST)
                sheet.paste(zoom, (x + (cell - size * 2) // 2, y + 160), zoom)
                draw.text((x + 4, y + 240), "2x pixels", fill=color)
            x += cell
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination, optimize=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    master, crop, box = prepare()
    frames = {size: master.resize((size, size), Image.Resampling.LANCZOS) for size in SIZES}
    for size in (16, 24, 32):
        frames[size] = small_variant(master, size)
    output = ROOT / "src/HL7Workbench.Desktop/Assets"
    output.mkdir(parents=True, exist_ok=True)
    master.save(output / "workbench-icon.png", optimize=True, compress_level=9)
    (output / "workbench.ico").write_bytes(ico_bytes(frames))
    assert sha256(SOURCE.read_bytes()).hexdigest().upper() == APPROVED_SHA256
    if args.preview:
        preview(frames, args.preview)
    print(f"Approved source unchanged. Shape bounds={box}; centered square crop={crop}; sizes={SIZES}; optimized small variants=16/24/32.")


if __name__ == "__main__":
    main()
