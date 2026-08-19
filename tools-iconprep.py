"""Clean up icon.png and build a multi-size .ico for prettyeyes."""
import os
import struct
from collections import deque
from io import BytesIO

from PIL import Image, ImageDraw, ImageFilter

SRC = r"D:\claude11\prettyeyes\icon.png"
OUT = r"C:\Users\xd\AppData\Local\Temp\claude\D--claude11\1f958b33-f218-4929-87d2-6667477bccb0\scratchpad"

# Only near-black counts as leftover background. Higher thresholds start eating
# the eye's own shadows and the lid outline.
DARK = 38
PAD_RATIO = 0.04


def drop_dark_outside(img):
    """
    Clear near-black pixels, then put back the ones that turned out to be holes
    inside the artwork. Without the second pass the pupil - which is black -
    would punch straight through the icon.
    """
    w, h = img.size
    px = img.load()

    # Pass 1: mark near-black as gone.
    gone = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a <= 10 or max(r, g, b) <= DARK:
                gone[y * w + x] = 1

    # Pass 2: flood from the border across "gone" pixels - those are outside.
    outside = bytearray(w * h)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if gone[y * w + x]:
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if gone[y * w + x]:
                q.append((x, y))

    while q:
        x, y = q.popleft()
        i = y * w + x
        if outside[i] or not gone[i]:
            continue
        outside[i] = 1
        if x > 0:
            q.append((x - 1, y))
        if x < w - 1:
            q.append((x + 1, y))
        if y > 0:
            q.append((x, y - 1))
        if y < h - 1:
            q.append((x, y + 1))

    cleared = 0
    for y in range(h):
        for x in range(w):
            i = y * w + x
            if gone[i] and outside[i]:
                r, g, b, a = px[x, y]
                if a > 0:
                    px[x, y] = (r, g, b, 0)
                    cleared += 1

    return cleared


def smooth_alpha(img):
    """Feather the hard cut-out edge so downscaling does not shred it."""
    r, g, b, a = img.split()
    blurred = a.filter(ImageFilter.GaussianBlur(1.0))
    # Keep the interior solid; only the transition band softens.
    a = Image.composite(a, blurred, a.point(lambda v: 255 if v > 250 else 0))
    return Image.merge("RGBA", (r, g, b, a))


def fit_square(img, pad_ratio=PAD_RATIO):
    """Crop to content, then centre it in a square with a small margin."""
    box = img.getbbox()
    content = img.crop(box)
    cw, ch = content.size

    side = int(max(cw, ch) * (1 + pad_ratio * 2))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(content, ((side - cw) // 2, (side - ch) // 2), content)
    return canvas


def simplified(size=256):
    """
    Hand-drawn stand-in for tiny sizes. The detailed artwork collapses into a
    purple smudge below ~24px, so this keeps only what survives: lid silhouette,
    iris, pupil, one highlight.
    """
    s = size * 8
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    cx, cy = s / 2, s / 2
    half_w, half_h = s * 0.47, s * 0.31
    steps = 80

    def lens(sign):
        return [
            (cx - half_w + 2 * half_w * t / steps,
             cy + sign * half_h * (1 - (2 * t / steps - 1) ** 2))
            for t in range(steps + 1)
        ]

    outline = lens(-1) + lens(1)[::-1]

    lid = (150, 150, 164, 255)
    stroke = max(2, int(s * 0.05))

    d.polygon(outline, fill=(14, 12, 20, 255))
    d.line(outline + [outline[0]], fill=lid, width=stroke, joint="curve")

    ir = half_h * 0.94
    d.ellipse([cx - ir, cy - ir, cx + ir, cy + ir], fill=(138, 52, 226, 255))
    d.ellipse([cx - ir * 0.78, cy - ir * 0.78, cx + ir * 0.78, cy + ir * 0.78],
              fill=(176, 92, 246, 255))

    pr = ir * 0.44
    d.ellipse([cx - pr, cy - pr, cx + pr, cy + pr], fill=(8, 6, 14, 255))

    hr = ir * 0.19
    hx, hy = cx - ir * 0.33, cy - ir * 0.35
    d.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=(255, 255, 255, 240))

    return img.resize((size, size), Image.LANCZOS)


def write_ico(path, entries):
    """entries: list of (size, PIL image). PNG frames, Vista+ format."""
    blobs = []
    for size, im in entries:
        buf = BytesIO()
        im.resize((size, size), Image.LANCZOS).save(buf, format="PNG")
        blobs.append((size, buf.getvalue()))

    with open(path, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, len(blobs)))
        offset = 6 + 16 * len(blobs)
        for size, data in blobs:
            dim = 0 if size >= 256 else size
            f.write(struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset))
            offset += len(data)
        for _, data in blobs:
            f.write(data)


SIMPLE_UPTO = 24


def contact_sheet(detailed, simple, path):
    """Preview every target size on light and dark, the way Windows shows it."""
    sizes = [256, 128, 64, 48, 32, 24, 20, 16]
    cell = 280
    sheet = Image.new("RGB", (cell * len(sizes), cell * 2 + 40), (245, 245, 247))
    d = ImageDraw.Draw(sheet)
    d.rectangle([0, cell + 20, sheet.width, cell * 2 + 40], fill=(18, 18, 20))

    for i, size in enumerate(sizes):
        src = simple if size <= SIMPLE_UPTO else detailed
        icon = src.resize((size, size), Image.LANCZOS)
        x = i * cell + (cell - size) // 2
        sheet.paste(icon, (x, 20 + (cell - size) // 2), icon)
        sheet.paste(icon, (x, cell + 40 + (cell - size) // 2), icon)
        d.text((i * cell + 10, 4), f"{size}px", fill=(90, 90, 95))

    sheet.save(path)


src = Image.open(SRC).convert("RGBA")
print(f"source: {src.size}")

cleared = drop_dark_outside(src)
print(f"cleared background pixels: {cleared}")

src = smooth_alpha(src)
detailed = fit_square(src)
print(f"squared: {detailed.size}")

detailed_1024 = detailed.resize((1024, 1024), Image.LANCZOS)
detailed_1024.save(os.path.join(OUT, "icon-clean.png"))

simple = simplified(256)
simple.save(os.path.join(OUT, "icon-small.png"))

# 32 uses the simplified art too: the detailed one is already a smudge there,
# and the drawn version reads more sharply.
write_ico(
    os.path.join(OUT, "prettyeyes.ico"),
    [(s, detailed_1024) for s in (256, 128, 64, 48)]
    + [(s, simple) for s in (32, 24, 20, 16)],
)

contact_sheet(detailed_1024, simple, os.path.join(OUT, "icon-preview.png"))

# Same sheet with the simplified art used at 32 as well, to compare.
SIMPLE_UPTO = 32
contact_sheet(detailed_1024, simple, os.path.join(OUT, "icon-preview-32simple.png"))
print("done")
